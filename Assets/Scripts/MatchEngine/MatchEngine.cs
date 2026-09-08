using System.Collections;
using UnityEngine;
using Football.Core;
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

            yield return new WaitForSeconds(1.5f);

            ChangeState(MatchState.InPlay);
            isClockRunning = true;
        }

        private void HandleGoalScored(int scoringTeamId, Vector3 ballPosition)
        {
            isClockRunning = false;
            ChangeState(MatchState.GoalScored);

            if (scoringTeamId == 1) homeScore++;
            else awayScore++;

            GameEvents.TriggerScoreUpdated(homeScore, awayScore);

            // Restart match from kickoff (conceding team kicks off)
            int nextKickoffTeam = (scoringTeamId == 1) ? 2 : 1;
            StartCoroutine(DelayedKickoffAfterGoal(nextKickoffTeam));
        }

        private IEnumerator DelayedKickoffAfterGoal(int nextKickoffTeam)
        {
            yield return new WaitForSeconds(3.5f);

            // Reset all players to formation anchors
            var players = FindObjectsOfType<FootballPlayerLocomotion>();
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

            StartCoroutine(ResumePlayAfterSetPiece());
        }

        private IEnumerator ResumePlayAfterSetPiece()
        {
            yield return new WaitForSeconds(2.0f);
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

            StartCoroutine(ResumePlayAfterSetPiece());
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
            var players = FindObjectsOfType<FootballPlayerLocomotion>();
            foreach (var p in players)
            {
                p.ResetPosition();
            }

            StartCoroutine(MatchKickoffRoutine(2));
        }
    }
}
