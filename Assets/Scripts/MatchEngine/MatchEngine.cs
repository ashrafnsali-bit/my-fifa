using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Engine
{
    public class MatchEngine : MonoBehaviour
    {
        public static MatchEngine Instance { get; private set; }

        [Header("Match Setup")]
        public string homeTeamName = "Argentina";
        public string awayTeamName = "France";
        public int homeScore = 0;
        public int awayScore = 0;

        [Header("Match Clock")]
        [Tooltip("Real-world duration of each 45-minute half in seconds (e.g. 180s = 3 mins)")]
        public float halfDurationSeconds = 180.0f;
        public int currentHalf = 1;
        public float matchClockMinutes = 0f;
        public int addedTimeMinutes = 2;
        public bool isClockRunning = false;

        [Header("Engine State")]
        public MatchState currentState = MatchState.PreMatch;

        [Header("Spawn Points")]
        public Transform centerSpot;
        public Transform homeGoal;
        public Transform awayGoal;

        private float elapsedHalfSeconds = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void OnEnable()
        {
            GameEvents.OnGoalScored += HandleGoalScored;
            GameEvents.OnSetPieceInitiated += HandleSetPiece;
            GameEvents.OnFoulCalled += HandleFoul;
        }

        private void OnDisable()
        {
            GameEvents.OnGoalScored -= HandleGoalScored;
            GameEvents.OnSetPieceInitiated -= HandleSetPiece;
            GameEvents.OnFoulCalled -= HandleFoul;
        }

        private void Start()
        {
            StartCoroutine(MatchKickoffRoutine(1));
        }

        private void Update()
        {
            if (!isClockRunning) return;

            elapsedHalfSeconds += Time.deltaTime;
            // Map real time elapsed to 45 football minutes per half
            float baseMinutes = (currentHalf - 1) * 45f;
            matchClockMinutes = baseMinutes + (elapsedHalfSeconds / halfDurationSeconds) * 45f;

            float targetHalfMinutes = (currentHalf * 45f) + addedTimeMinutes;

            if (matchClockMinutes >= targetHalfMinutes)
            {
                EndCurrentHalf();
            }
        }

        public void ChangeState(MatchState newState)
        {
            currentState = newState;
            GameEvents.TriggerMatchStateChanged(newState);
        }

        public IEnumerator MatchKickoffRoutine(int kickingTeamId)
        {
            ChangeState(MatchState.KickOff);
            isClockRunning = false;

            // Reposition ball to center spot
            Vector3 kickoffPos = centerSpot != null ? centerSpot.position : Vector3.zero;
            if (FootballBall.Instance != null)
            {
                FootballBall.Instance.ResetPosition(kickoffPos + Vector3.up * 0.11f);
            }

            // Position at least 2 players for the kicking team (kickoff taker and partner)
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            PlayerRuntimeState kickoffTaker = null;
            PlayerRuntimeState kickoffPartner = null;
            float closestDist1 = 999f;
            float closestDist2 = 999f;

            foreach (var p in players)
            {
                if (p.teamId == kickingTeamId && !p.isSentOff && p.attributes != null && p.attributes.position != PlayerPosition.GK)
                {
                    float d = Vector3.Distance(p.transform.position, kickoffPos);
                    if (d < closestDist1)
                    {
                        closestDist2 = closestDist1;
                        kickoffPartner = kickoffTaker;

                        closestDist1 = d;
                        kickoffTaker = p;
                    }
                    else if (d < closestDist2)
                    {
                        closestDist2 = d;
                        kickoffPartner = p;
                    }
                }
            }

            if (kickoffTaker != null)
            {
                Vector3 takerOffset = (kickingTeamId == 1) ? Vector3.back * 0.95f : Vector3.forward * 0.95f;
                kickoffTaker.transform.position = kickoffPos + takerOffset;
                kickoffTaker.transform.rotation = Quaternion.LookRotation(-takerOffset, Vector3.up);
            }

            if (kickoffPartner != null)
            {
                Vector3 partnerOffset = (kickingTeamId == 1) ? (Vector3.back * 1.3f + Vector3.right * 1.9f) : (Vector3.forward * 1.3f + Vector3.left * 1.9f);
                kickoffPartner.transform.position = kickoffPos + partnerOffset;
                Vector3 toBall = (kickoffPos - kickoffPartner.transform.position).normalized;
                kickoffPartner.transform.rotation = Quaternion.LookRotation(toBall, Vector3.up);
            }

            // Position at least 2 players for the defending team at the edge of the center circle
            int defendingTeamId = (kickingTeamId == 1) ? 2 : 1;
            PlayerRuntimeState defPlayer1 = null;
            PlayerRuntimeState defPlayer2 = null;
            float defDist1 = 999f;
            float defDist2 = 999f;

            foreach (var p in players)
            {
                if (p.teamId == defendingTeamId && !p.isSentOff && p.attributes != null && p.attributes.position != PlayerPosition.GK)
                {
                    float d = Vector3.Distance(p.transform.position, kickoffPos);
                    if (d < defDist1)
                    {
                        defDist2 = defDist1;
                        defPlayer2 = defPlayer1;

                        defDist1 = d;
                        defPlayer1 = p;
                    }
                    else if (d < defDist2)
                    {
                        defDist2 = d;
                        defPlayer2 = p;
                    }
                }
            }

            if (defPlayer1 != null)
            {
                Vector3 defOffset = (defendingTeamId == 1) ? (Vector3.back * 9.5f + Vector3.left * 2.8f) : (Vector3.forward * 9.5f + Vector3.right * 2.8f);
                defPlayer1.transform.position = kickoffPos + defOffset;
                Vector3 toCenter = (kickoffPos - defPlayer1.transform.position).normalized;
                defPlayer1.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }

            if (defPlayer2 != null)
            {
                Vector3 defOffset = (defendingTeamId == 1) ? (Vector3.back * 9.5f + Vector3.right * 2.8f) : (Vector3.forward * 9.5f + Vector3.left * 2.8f);
                defPlayer2.transform.position = kickoffPos + defOffset;
                Vector3 toCenter = (kickoffPos - defPlayer2.transform.position).normalized;
                defPlayer2.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }

            if (kickingTeamId == 2)
            {
                // AI opponent kickoff: wait briefly then pass backwards
                yield return new WaitForSeconds(1.6f);
                if (FootballBall.Instance != null)
                {
                    Vector3 passDir = (PitchConstants.GetDefendingGoalCenter(2) - kickoffPos).normalized;
                    passDir.y = 0f;
                    FootballBall.Instance.Kick(passDir * 5.5f, Vector3.zero, 9, 2);
                }
            }
            else
            {
                // Human team kickoff: wait for user order / input, or auto-start after 2.5 seconds
                float waitTimer = 0f;
                bool userTriggered = false;
                while (!userTriggered && waitTimer < 2.5f)
                {
                    waitTimer += Time.deltaTime;
                    var keyboard = Keyboard.current;
                    var gamepad = Gamepad.current;

                    if (keyboard != null)
                    {
                        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                            keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ||
                            keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
                            keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                            keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                        {
                            userTriggered = true;
                        }
                    }

                    if (Input.anyKeyDown || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f)
                    {
                        userTriggered = true;
                    }

                    if (gamepad != null)
                    {
                        if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame ||
                            gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame ||
                            gamepad.leftStick.ReadValue().sqrMagnitude > 0.15f)
                        {
                            userTriggered = true;
                        }
                    }

                    yield return null;
                }

                if (FootballBall.Instance != null && FootballBall.Instance.Velocity.magnitude < 0.2f)
                {
                    FootballBall.Instance.Kick(Vector3.forward * 5.0f, Vector3.zero, 10, 1);
                }
            }

            ChangeState(MatchState.InPlay);
            isClockRunning = true;
        }

        private float lastGoalScoredTime = -10f;

        private void HandleGoalScored(int scoringTeamId, Vector3 ballPosition)
        {
            // Safeguard 1: Ignore if already in GoalScored state (prevents multi-counting while ball is in net)
            if (currentState == MatchState.GoalScored)
            {
                return;
            }

            // Safeguard 2: Cooldown check (at least 4.0 seconds between any goal events)
            if (Time.time - lastGoalScoredTime < 4.0f)
            {
                return;
            }

            lastGoalScoredTime = Time.time;
            isClockRunning = false;
            ChangeState(MatchState.GoalScored);

            // Exactly 1 goal counted per valid score
            if (scoringTeamId == 1) homeScore++;
            else awayScore++;

            Debug.Log($"<color=yellow><b>[GOAL AWARDED]</b> Team {scoringTeamId} scored! Official score: {homeScore} - {awayScore}</color>");

            GameEvents.TriggerScoreUpdated(homeScore, awayScore);

            // Settle ball inside the net
            if (FootballBall.Instance != null && FootballBall.Instance.BallRigidbody != null)
            {
                FootballBall.Instance.BallRigidbody.linearVelocity = FootballBall.Instance.BallRigidbody.linearVelocity * 0.2f;
                FootballBall.Instance.BallRigidbody.angularVelocity = Vector3.zero;
            }

            // Restart match from kickoff (conceding team kicks off)
            int nextKickoffTeam = (scoringTeamId == 1) ? 2 : 1;
            StartCoroutine(DelayedKickoffAfterGoal(nextKickoffTeam));
        }

        private IEnumerator DelayedKickoffAfterGoal(int nextKickoffTeam)
        {
            yield return new WaitForSeconds(3.5f);

            // Reset all players to formation anchors
            var players = FindObjectsByType<FootballPlayerLocomotion>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                p.ResetPosition();
            }

            StartCoroutine(MatchKickoffRoutine(nextKickoffTeam));
        }

        private void HandleSetPiece(MatchState pieceType, Vector3 restartPos, int teamId)
        {
            isClockRunning = false;
            ChangeState(pieceType);

            if (FootballBall.Instance != null)
            {
                FootballBall.Instance.ResetPosition(restartPos + Vector3.up * 0.11f);
            }

            StartCoroutine(ResumePlayAfterSetPiece(pieceType, restartPos, teamId));
        }

        private IEnumerator ResumePlayAfterSetPiece(MatchState pieceType, Vector3 restartPos, int teamId)
        {
            yield return new WaitForSeconds(1.5f);

            // Re-kick ball into play automatically towards pitch center / teammates so game NEVER stops!
            if (FootballBall.Instance != null)
            {
                Vector3 targetDir = (Vector3.zero - restartPos).normalized;
                targetDir.y = 0f;
                Vector3 attackDir = PitchConstants.GetTargetGoalCenter(teamId);
                Vector3 kickDir = Vector3.Lerp(targetDir, (attackDir - restartPos).normalized, 0.40f).normalized;
                kickDir.y = 0.08f;
                FootballBall.Instance.Kick(kickDir.normalized * 8.5f, Vector3.zero, 10, teamId);
            }

            ChangeState(MatchState.InPlay);
            isClockRunning = true;
        }

        private void HandleFoul(int offendingTeamId, Vector3 foulPos, bool isPenalty)
        {
            isClockRunning = false;
            ChangeState(isPenalty ? MatchState.PenaltyKick : MatchState.FreeKick);

            int takingTeam = (offendingTeamId == 1) ? 2 : 1;
            Vector3 restartPos = isPenalty ? PitchConstants.GetPenaltySpot(offendingTeamId == 1) : foulPos;

            if (FootballBall.Instance != null)
            {
                FootballBall.Instance.ResetPosition(restartPos + Vector3.up * 0.11f);
            }

            StartCoroutine(ResumePlayAfterSetPiece(isPenalty ? MatchState.PenaltyKick : MatchState.FreeKick, restartPos, takingTeam));
        }

        private void EndCurrentHalf()
        {
            isClockRunning = false;

            if (currentHalf == 1)
            {
                ChangeState(MatchState.HalfTime);
                GameEvents.TriggerHalfCompleted(1);
                StartCoroutine(HalfTimeBreakRoutine());
            }
            else
            {
                ChangeState(MatchState.FullTime);
                GameEvents.TriggerHalfCompleted(2);
                GameEvents.TriggerMatchEnded();
            }
        }

        private IEnumerator HalfTimeBreakRoutine()
        {
            yield return new WaitForSeconds(4.0f);

            currentHalf = 2;
            elapsedHalfSeconds = 0f;

            // Reset players & kickoff for 2nd half (Away team kicks off)
            var players = FindObjectsByType<FootballPlayerLocomotion>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                p.ResetPosition();
            }

            StartCoroutine(MatchKickoffRoutine(2));
        }
    }
}
