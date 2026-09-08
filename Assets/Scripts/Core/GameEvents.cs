using System;
using UnityEngine;

namespace Football.Core
{
    public enum MatchState
    {
        PreMatch,
        KickOff,
        InPlay,
        BallOutOfBounds,
        ThrowIn,
        CornerKick,
        GoalKick,
        FreeKick,
        PenaltyKick,
        FoulStoppage,
        GoalScored,
        HalfTime,
        FullTime,
        ExtraTime,
        PenaltyShootout
    }

    public enum CardType
    {
        None,
        Yellow,
        Red
    }

    public enum ShotType
    {
        Standard,
        Finesse,
        Power,
        Chip,
        Header
    }

    public enum PassType
    {
        Ground,
        Driven,
        Lobbed,
        ThroughBall
    }

    /// <summary>
    /// Central decoupled event broker for the football match simulation.
    /// Follows publish-subscribe pattern to decouple engine, presentation, commentary, and audio.
    /// </summary>
    public static class GameEvents
    {
        // Match flow events
        public static event Action<MatchState> OnMatchStateChanged;
        public static event Action<int, int> OnScoreUpdated; // homeScore, awayScore
        public static event Action<int, Vector3> OnGoalScored; // scoringTeamId, ballPosition
        public static event Action<int> OnHalfCompleted; // halfIndex (1 or 2)
        public static event Action OnMatchEnded;

        // Referee & Discipline events
        public static event Action<int, Vector3, bool> OnFoulCalled; // offendingTeamId, foulPos, isDirectFreeKick
        public static event Action<int, int, CardType> OnCardIssued; // teamId, playerNumber, card
        public static event Action<int> OnOffsideCalled; // offendingTeamId

        // Set pieces
        public static event Action<MatchState, Vector3, int> OnSetPieceInitiated; // state, restartPos, takingTeamId

        // Gameplay actions & Audio/Commentary triggers
        public static event Action<int, ShotType, float> OnShotTaken; // teamId, type, power
        public static event Action<int, PassType> OnPassCompleted; // teamId, passType
        public static event Action<int, Transform> OnPassInitiated; // teamId, targetTeammate
        public static event Action<int, bool> OnTackleExecuted; // teamId, wasSuccessful
        public static event Action OnWoodworkHit;
        public static event Action<int> OnGoalkeeperSave; // savingTeamId
        public static event Action<float> OnCrowdExcitementChanged; // 0.0 to 1.0 intensity

        // Dispatchers
        public static void TriggerMatchStateChanged(MatchState newState) => OnMatchStateChanged?.Invoke(newState);
        public static void TriggerScoreUpdated(int home, int away) => OnScoreUpdated?.Invoke(home, away);
        public static void TriggerGoalScored(int teamId, Vector3 pos) => OnGoalScored?.Invoke(teamId, pos);
        public static void TriggerHalfCompleted(int half) => OnHalfCompleted?.Invoke(half);
        public static void TriggerMatchEnded() => OnMatchEnded?.Invoke();

        public static void TriggerFoulCalled(int teamId, Vector3 pos, bool isDirect) => OnFoulCalled?.Invoke(teamId, pos, isDirect);
        public static void TriggerCardIssued(int teamId, int playerNum, CardType card) => OnCardIssued?.Invoke(teamId, playerNum, card);
        public static void TriggerOffsideCalled(int teamId) => OnOffsideCalled?.Invoke(teamId);

        public static void TriggerSetPieceInitiated(MatchState state, Vector3 pos, int teamId) => OnSetPieceInitiated?.Invoke(state, pos, teamId);

        public static void TriggerShotTaken(int teamId, ShotType type, float power) => OnShotTaken?.Invoke(teamId, type, power);
        public static void TriggerPassCompleted(int teamId, PassType passType) => OnPassCompleted?.Invoke(teamId, passType);
        public static void TriggerPassInitiated(int teamId, Transform targetTeammate) => OnPassInitiated?.Invoke(teamId, targetTeammate);
        public static void TriggerTackleExecuted(int teamId, bool success) => OnTackleExecuted?.Invoke(teamId, success);
        public static void TriggerWoodworkHit() => OnWoodworkHit?.Invoke();
        public static void TriggerGoalkeeperSave(int teamId) => OnGoalkeeperSave?.Invoke(teamId);
        public static void TriggerCrowdExcitement(float intensity) => OnCrowdExcitementChanged?.Invoke(Mathf.Clamp01(intensity));
    }
}
