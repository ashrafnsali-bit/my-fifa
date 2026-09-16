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
        [Tooltip("Real-world duration of each 45-minute half in seconds (60s = 1 minute per half)")]
        public float halfDurationSeconds = 60.0f;
        public int currentHalf = 1;
        public float matchClockMinutes = 0f;
        public int addedTimeMinutes = 0;
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

            halfDurationSeconds = 60.0f;
            addedTimeMinutes = 0;
            currentHalf = 1;
            matchClockMinutes = 0f;
            elapsedHalfSeconds = 0f;
            isClockRunning = false;
        }

        private void OnEnable()
        {
            GameEvents.OnMatchStateChanged += HandleMatchStateChanged;
            GameEvents.OnGoalScored += HandleGoalScored;
            GameEvents.OnSetPieceInitiated += HandleSetPiece;
            GameEvents.OnFoulCalled += HandleFoul;
        }

        private void OnDisable()
        {
            GameEvents.OnMatchStateChanged -= HandleMatchStateChanged;
            GameEvents.OnGoalScored -= HandleGoalScored;
            GameEvents.OnSetPieceInitiated -= HandleSetPiece;
            GameEvents.OnFoulCalled -= HandleFoul;
        }

        private void HandleMatchStateChanged(MatchState newState)
        {
            currentState = newState;
            if (newState == MatchState.InPlay)
            {
                isClockRunning = true;
            }
            else if (newState == MatchState.GoalScored || newState == MatchState.KickOff || newState == MatchState.HalfTime || newState == MatchState.FullTime)
            {
                isClockRunning = false;
            }
        }

        private void Start()
        {
            PitchConstants.CurrentHalf = 1;
            StartCoroutine(MatchKickoffRoutine(1));
        }

        private void Update()
        {
            // Failsafe listener for all Set Pieces & Kickoff: any user key/click resumes active play
            bool isRestartState = (currentState == MatchState.KickOff ||
                                   currentState == MatchState.ThrowIn ||
                                   currentState == MatchState.CornerKick ||
                                   currentState == MatchState.GoalKick ||
                                   currentState == MatchState.FreeKick ||
                                   currentState == MatchState.PenaltyKick);

            if (isRestartState)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                var gamepad = UnityEngine.InputSystem.Gamepad.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;

                bool anyRestartInput = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)) ||
                                       (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                                       (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame)) ||
                                       Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.L) || Input.GetMouseButtonDown(0);

                if (anyRestartInput)
                {
                    ResumePlayFromCurrentSetPiece();
                }
            }

            if (!isClockRunning) return;

            elapsedHalfSeconds += Time.deltaTime;
            // Map real time elapsed to 45 football minutes per half (0-45m in Half 1, 45-90m in Half 2)
            float baseMinutes = (currentHalf - 1) * 45f;
            float ratio = Mathf.Clamp01(elapsedHalfSeconds / halfDurationSeconds);
            matchClockMinutes = baseMinutes + (ratio * 45f);

            if (elapsedHalfSeconds >= halfDurationSeconds)
            {
                matchClockMinutes = currentHalf * 45f;
                EndCurrentHalf();
            }
        }

        public void ResumePlayFromCurrentSetPiece()
        {
            var ball = FootballBall.Instance;
            if (ball != null)
            {
                Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(CurrentSetPieceTeam);
                Vector3 kickDir = (targetGoal - ball.transform.position).normalized;
                kickDir.y = 0f;
                if (kickDir.sqrMagnitude < 0.01f) kickDir = (CurrentSetPieceTeam == 1) ? Vector3.forward : Vector3.back;

                ball.Kick(kickDir * 9.5f + Vector3.up * 0.5f, Vector3.zero, 0, CurrentSetPieceTeam);
            }

            ChangeState(MatchState.InPlay);
            isClockRunning = true;
        }

        public void ChangeState(MatchState newState)
        {
            if (currentState == newState && GameEvents.CurrentMatchState == newState) return;
            currentState = newState;
            GameEvents.TriggerMatchStateChanged(newState);
        }

        public int CurrentKickoffTeam { get; private set; } = 1;

        public void ResetAllPlayersToKickoffFormation(int kickingTeamId)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            Vector3 center = centerSpot != null ? centerSpot.position : Vector3.zero;

            foreach (var p in players)
            {
                if (p == null || p.isSentOff) continue;

                p.hasBall = false;
                p.isHoldingBallInHands = false;

                var loc = p.GetComponent<FootballPlayerLocomotion>();
                if (loc != null)
                {
                    loc.ResetPosition();
                }

                Vector3 pos = p.transform.position;
                pos.y = 0f;

                bool isTeamDefendingNegativeZ = (p.teamId == 1 && PitchConstants.Team1DefendsNegativeZ) || (p.teamId == 2 && !PitchConstants.Team1DefendsNegativeZ);

                // Goalkeeper is strictly positioned right between the posts with AI active
                bool isGK = p.attributes != null && p.attributes.position == PlayerPosition.GK;
                if (isGK)
                {
                    float gkZ = isTeamDefendingNegativeZ ? (-PitchConstants.HalfLength + 1.8f) : (PitchConstants.HalfLength - 1.8f);
                    pos = new Vector3(0f, 0f, gkZ);
                    p.transform.position = pos;
                    if (loc != null) loc.ResetPosition(pos);

                    var ai = p.GetComponent("FootballAIPlayer") as MonoBehaviour;
                    if (ai != null) ai.enabled = true;
                    var input = p.GetComponent<FootballInputHandler>();
                    if (input != null) input.isHumanControlled = false;

                    p.transform.rotation = isTeamDefendingNegativeZ 
                        ? Quaternion.LookRotation(Vector3.forward, Vector3.up) 
                        : Quaternion.LookRotation(Vector3.back, Vector3.up);
                    continue;
                }

                // Soccer Rule: At kickoff, EVERY outfield player must be in their own half of the pitch!
                if (isTeamDefendingNegativeZ)
                {
                    if (pos.z > -1.5f) pos.z = -1.5f;

                    // If defending team (not kicking off), stay outside the center circle (9.15m)
                    if (kickingTeamId != p.teamId && Vector3.Distance(new Vector3(pos.x, 0f, pos.z), center) < 9.5f)
                    {
                        pos.z = -9.5f;
                    }
                    p.transform.position = pos;
                    p.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                }
                else
                {
                    if (pos.z < 1.5f) pos.z = 1.5f;

                    // If defending team (not kicking off), stay outside the center circle (9.15m)
                    if (kickingTeamId != p.teamId && Vector3.Distance(new Vector3(pos.x, 0f, pos.z), center) < 9.5f)
                    {
                        pos.z = 9.5f;
                    }
                    p.transform.position = pos;
                    p.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                }
            }
        }

        public void StartPlayFromKickoff()
        {
            if (currentState == MatchState.KickOff)
            {
                ChangeState(MatchState.InPlay);
                isClockRunning = true;
            }
        }

        public IEnumerator MatchKickoffRoutine(int kickingTeamId)
        {
            CurrentKickoffTeam = kickingTeamId;
            ChangeState(MatchState.KickOff);
            isClockRunning = false;

            // Reposition ball to center spot, unfreeze kinematics and cancel any lingering velocity
            Vector3 kickoffPos = centerSpot != null ? centerSpot.position : Vector3.zero;
            kickoffPos.y = 0f;
            if (FootballBall.Instance != null)
            {
                if (FootballBall.Instance.BallRigidbody != null)
                {
                    FootballBall.Instance.BallRigidbody.isKinematic = false;
                    FootballBall.Instance.BallRigidbody.linearVelocity = Vector3.zero;
                    FootballBall.Instance.BallRigidbody.angularVelocity = Vector3.zero;
                }
                FootballBall.Instance.ResetPosition(kickoffPos + Vector3.up * 0.11f);
            }

            // Immediately snap camera to center kickoff via decoupled GameEvents
            GameEvents.TriggerCameraSnapRequested();

            // Reposition all 22 players strictly inside their own half
            ResetAllPlayersToKickoffFormation(kickingTeamId);

            // Select kickoff taker and partner for the kicking team
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

            bool isKickingTeamDefendingNegZ = (kickingTeamId == 1 && PitchConstants.Team1DefendsNegativeZ) || (kickingTeamId == 2 && !PitchConstants.Team1DefendsNegativeZ);

            if (kickoffTaker != null)
            {
                Vector3 takerOffset = isKickingTeamDefendingNegZ ? Vector3.back * 0.95f : Vector3.forward * 0.95f;
                kickoffTaker.transform.position = kickoffPos + takerOffset;
                kickoffTaker.transform.rotation = Quaternion.LookRotation(-takerOffset, Vector3.up);
                kickoffTaker.hasBall = true;
            }

            if (kickoffPartner != null)
            {
                Vector3 partnerOffset = isKickingTeamDefendingNegZ ? (Vector3.back * 1.3f + Vector3.right * 1.8f) : (Vector3.forward * 1.3f + Vector3.left * 1.8f);
                kickoffPartner.transform.position = kickoffPos + partnerOffset;
                Vector3 toBall = (kickoffPos - kickoffPartner.transform.position).normalized;
                kickoffPartner.transform.rotation = Quaternion.LookRotation(toBall, Vector3.up);
            }

            // Position at least 2 players for the defending team at the edge of the center circle
            int defendingTeamId = (kickingTeamId == 1) ? 2 : 1;
            bool isDefendingTeamDefendingNegZ = (defendingTeamId == 1 && PitchConstants.Team1DefendsNegativeZ) || (defendingTeamId == 2 && !PitchConstants.Team1DefendsNegativeZ);
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
                Vector3 defOffset = isDefendingTeamDefendingNegZ ? (Vector3.back * 9.5f + Vector3.left * 2.8f) : (Vector3.forward * 9.5f + Vector3.right * 2.8f);
                defPlayer1.transform.position = kickoffPos + defOffset;
                Vector3 toCenter = (kickoffPos - defPlayer1.transform.position).normalized;
                defPlayer1.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }

            if (defPlayer2 != null)
            {
                Vector3 defOffset = isDefendingTeamDefendingNegZ ? (Vector3.back * 9.5f + Vector3.right * 2.8f) : (Vector3.forward * 9.5f + Vector3.left * 2.8f);
                defPlayer2.transform.position = kickoffPos + defOffset;
                Vector3 toCenter = (kickoffPos - defPlayer2.transform.position).normalized;
                defPlayer2.transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
            }

            if (kickingTeamId == 2)
            {
                // AI opponent kickoff: wait briefly (1.5s) for formation, then execute pass to partner
                yield return new WaitForSeconds(1.5f);
                if (kickoffTaker != null)
                {
                    var takerActions = kickoffTaker.GetComponent<FootballPlayerActions>();
                    Vector3 passTarget = kickoffPartner != null ? kickoffPartner.transform.position : (kickoffPos + Vector3.forward * 4.0f);
                    Vector3 passDir = (passTarget - kickoffPos).normalized;
                    passDir.y = 0f;

                    if (takerActions != null)
                    {
                        takerActions.ExecutePass(PassType.Ground, passDir, 0.6f, kickoffPartner != null ? kickoffPartner.transform : null);
                    }
                    else if (FootballBall.Instance != null)
                    {
                        FootballBall.Instance.Kick(passDir * 5.2f, Vector3.zero, kickoffTaker.jerseyNumber, 2);
                        StartPlayFromKickoff();
                    }
                }
            }
            else
            {
                // Human team kickoff: assign active human control to kickoff taker
                if (kickoffTaker != null)
                {
                    var inputHandler = kickoffTaker.GetComponent<FootballInputHandler>();
                    if (inputHandler != null) inputHandler.isHumanControlled = true;

                    GameEvents.TriggerRequestPlayerSwitch(kickoffTaker.transform);
                }

                // Strict rule: Game and clock DO NOT start until a player kicks/passes the ball!
                // Loop while waiting for user kick action (which will call StartPlayFromKickoff)
                while (currentState == MatchState.KickOff)
                {
                    yield return null;
                }
            }
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

            // Settle and lock ball completely inside the net
            if (FootballBall.Instance != null && FootballBall.Instance.BallRigidbody != null)
            {
                FootballBall.Instance.BallRigidbody.linearVelocity = Vector3.zero;
                FootballBall.Instance.BallRigidbody.angularVelocity = Vector3.zero;
                FootballBall.Instance.BallRigidbody.isKinematic = true;
            }

            // Immediately clear ball possession and movement on all players across the pitch
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p != null)
                {
                    p.hasBall = false;
                    p.isHoldingBallInHands = false;
                    var loc = p.GetComponent<FootballPlayerLocomotion>();
                    if (loc != null)
                    {
                        loc.OnBallKicked(0.5f);
                        loc.SetWorldMovementInput(Vector3.zero, false);
                    }
                }
            }

            // CRITICAL RULE: When one team scores, the CONCEDING (other) team restarts from center of the pitch!
            // Team 1 scored -> Team 2 restarts from center
            // Team 2 scored -> Team 1 restarts from center
            int nextKickoffTeam = (scoringTeamId == 1) ? 2 : 1;
            CurrentKickoffTeam = nextKickoffTeam;
            StartCoroutine(DelayedKickoffAfterGoal(nextKickoffTeam));
        }

        private IEnumerator DelayedKickoffAfterGoal(int nextKickoffTeam)
        {
            yield return new WaitForSeconds(2.5f);

            StartCoroutine(MatchKickoffRoutine(nextKickoffTeam));
        }

        public int CurrentSetPieceTeam { get; private set; } = 1;

        private void HandleSetPiece(MatchState pieceType, Vector3 restartPos, int teamId)
        {
            CurrentSetPieceTeam = teamId;
            isClockRunning = false;
            ChangeState(pieceType);

            // Reposition ball to restart spot, cancel velocity
            if (FootballBall.Instance != null)
            {
                if (FootballBall.Instance.BallRigidbody != null)
                {
                    FootballBall.Instance.BallRigidbody.isKinematic = false;
                    FootballBall.Instance.BallRigidbody.linearVelocity = Vector3.zero;
                    FootballBall.Instance.BallRigidbody.angularVelocity = Vector3.zero;
                }
                FootballBall.Instance.ResetPosition(restartPos + Vector3.up * 0.11f);
            }

            StartCoroutine(SetPieceRoutine(pieceType, restartPos, teamId));
        }

        private IEnumerator SetPieceRoutine(MatchState pieceType, Vector3 restartPos, int teamId)
        {
            // 1. Select the closest appropriate taker from taking team
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            PlayerRuntimeState taker = null;
            float minDist = float.MaxValue;

            foreach (var p in allPlayers)
            {
                if (p == null || p.isSentOff || p.teamId != teamId) continue;
                bool isGK = p.attributes != null && p.attributes.position == PlayerPosition.GK;
                // For GoalKick prefer GK, for others avoid GK
                if (pieceType == MatchState.GoalKick)
                {
                    if (isGK) { taker = p; break; }
                }
                else if (isGK) continue;

                float d = Vector3.Distance(p.transform.position, restartPos);
                if (d < minDist)
                {
                    minDist = d;
                    taker = p;
                }
            }

            // 2. Position taker at restart spot facing towards target goal
            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(teamId);
            Vector3 aimDir = (targetGoal - restartPos).normalized;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = (teamId == 1) ? Vector3.forward : Vector3.back;

            if (taker != null)
            {
                Vector3 takerPos = restartPos - aimDir * 0.85f;
                takerPos.y = 0f;
                taker.transform.position = takerPos;
                taker.transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
                taker.hasBall = true;

                var loc = taker.GetComponent<FootballPlayerLocomotion>();
                if (loc != null) loc.ResetPosition(takerPos);
            }

            // 3. If AI team: wait 1.5 seconds, then execute a real pass/cross to a teammate
            if (teamId == 2)
            {
                yield return new WaitForSeconds(1.5f);
                if (taker != null)
                {
                    var takerActions = taker.GetComponent<FootballPlayerActions>();
                    if (takerActions != null)
                    {
                        bool isLongCross = (pieceType == MatchState.CornerKick || pieceType == MatchState.GoalKick);
                        takerActions.ExecutePass(isLongCross ? PassType.Lobbed : PassType.Ground, aimDir, 0.7f);
                    }
                    else if (FootballBall.Instance != null)
                    {
                        FootballBall.Instance.Kick(aimDir * 9.0f + Vector3.up * (pieceType == MatchState.CornerKick ? 5.0f : 0.5f), Vector3.zero, taker.jerseyNumber, teamId);
                        ChangeState(MatchState.InPlay);
                        isClockRunning = true;
                    }
                }
            }
            else
            {
                // Human team set piece: assign active human control to taker and wait indefinitely for user kick
                if (taker != null)
                {
                    var inputHandler = taker.GetComponent<FootballInputHandler>();
                    if (inputHandler != null) inputHandler.isHumanControlled = true;

                    GameEvents.TriggerRequestPlayerSwitch(taker.transform);
                }

                // Strict rule: Game does NOT start until player actually plays the ball
                while (currentState == pieceType)
                {
                    yield return null;
                }
            }
        }

        private void HandleFoul(int offendingTeamId, Vector3 foulPos, bool isPenalty)
        {
            int takingTeam = (offendingTeamId == 1) ? 2 : 1;
            Vector3 restartPos = isPenalty ? PitchConstants.GetPenaltySpot(offendingTeamId == 1) : foulPos;
            HandleSetPiece(isPenalty ? MatchState.PenaltyKick : MatchState.FreeKick, restartPos, takingTeam);
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
                StartCoroutine(FullTimeBreakRoutine());
            }
        }

        private bool CheckAnyUserInteractInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;

            bool keyHit = false;
            if (keyboard != null)
            {
                keyHit = keyboard.spaceKey.wasPressedThisFrame ||
                         keyboard.enterKey.wasPressedThisFrame ||
                         keyboard.numpadEnterKey.wasPressedThisFrame ||
                         keyboard.jKey.wasPressedThisFrame ||
                         keyboard.kKey.wasPressedThisFrame ||
                         keyboard.lKey.wasPressedThisFrame ||
                         keyboard.wKey.wasPressedThisFrame ||
                         keyboard.aKey.wasPressedThisFrame ||
                         keyboard.sKey.wasPressedThisFrame ||
                         keyboard.dKey.wasPressedThisFrame ||
                         keyboard.escapeKey.wasPressedThisFrame ||
                         keyboard.anyKey.wasPressedThisFrame;
            }

            bool mouseHit = false;
            if (mouse != null)
            {
                mouseHit = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
            }

            bool padHit = false;
            if (gamepad != null)
            {
                padHit = gamepad.buttonSouth.wasPressedThisFrame ||
                         gamepad.buttonEast.wasPressedThisFrame ||
                         gamepad.buttonWest.wasPressedThisFrame ||
                         gamepad.buttonNorth.wasPressedThisFrame ||
                         gamepad.startButton.wasPressedThisFrame;
            }

            return keyHit || mouseHit || padHit || Input.anyKeyDown || Input.GetMouseButtonDown(0);
        }

        private IEnumerator HalfTimeBreakRoutine()
        {
            // Delay 1.0s before accepting input so whistle audio plays cleanly
            yield return new WaitForSeconds(1.0f);

            bool proceed = false;
            while (!proceed)
            {
                if (CheckAnyUserInteractInput())
                {
                    proceed = true;
                }
                yield return null;
            }

            currentHalf = 2;
            PitchConstants.CurrentHalf = 2;
            elapsedHalfSeconds = 0f;

            // Reset players & kickoff for 2nd half (Away team kicks off with swapped sides)
            var players = FindObjectsByType<FootballPlayerLocomotion>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                p.ResetPosition();
            }

            StartCoroutine(MatchKickoffRoutine(2));
        }

        private IEnumerator FullTimeBreakRoutine()
        {
            // Give 2.0 seconds after final whistle so the user can clearly see the score and stop pressing match keys
            yield return new WaitForSeconds(2.0f);

            bool proceed = false;
            while (!proceed)
            {
                if (CheckAnyUserInteractInput())
                {
                    proceed = true;
                }
                yield return null;
            }

            // End / Quit the game strictly on user input
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
