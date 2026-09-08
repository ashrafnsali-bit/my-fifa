using System.Collections.Generic;
using UnityEngine;
using Football.Core;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Engine
{
    public class SetPieceManager : MonoBehaviour
    {
        public static SetPieceManager Instance { get; private set; }

        [Header("Defensive Wall Parameters")]
        public float wallDistance = 9.15f; // FIFA regulation 10 yards
        public int wallPlayerCount = 4;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        /// <summary>
        /// Positions a defensive wall between the free kick spot and the defending goal.
        /// </summary>
        public void PositionDefensiveWall(Vector3 freeKickSpot, int defendingTeamId, List<FootballPlayerLocomotion> defendingPlayers)
        {
            if (defendingPlayers == null || defendingPlayers.Count == 0) return;

            Vector3 defendingGoal = PitchConstants.GetDefendingGoalCenter(defendingTeamId == 1 ? 2 : 1);
            Vector3 toGoal = (defendingGoal - freeKickSpot).normalized;

            // Center of the wall at 9.15m towards the goal
            Vector3 wallCenter = freeKickSpot + toGoal * wallDistance;
            Vector3 wallPerpendicular = Vector3.Cross(toGoal, Vector3.up).normalized;

            float playerSpacing = 0.65f;
            int count = Mathf.Min(wallPlayerCount, defendingPlayers.Count);

            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * playerSpacing;
                Vector3 wallSlot = wallCenter + wallPerpendicular * offset;
                wallSlot.y = 0f;

                defendingPlayers[i].ResetPosition(wallSlot);
                defendingPlayers[i].transform.rotation = Quaternion.LookRotation(-toGoal, Vector3.up);
            }
        }

        /// <summary>
        /// Formats players for a penalty kick shootout or in-game penalty.
        /// </summary>
        public void SetupPenaltyKick(int takingTeamId, FootballPlayerLocomotion kicker, FootballPlayerLocomotion goalkeeper)
        {
            bool isHomeTarget = takingTeamId == 2;
            Vector3 spot = PitchConstants.GetPenaltySpot(isHomeTarget);
            Vector3 goalCenter = isHomeTarget ? PitchConstants.HomeGoalCenter : PitchConstants.AwayGoalCenter;

            // Position ball
            if (FootballBall.Instance != null)
            {
                FootballBall.Instance.ResetPosition(spot + Vector3.up * 0.11f);
            }

            // Position kicker 2m behind penalty spot
            Vector3 kickApproach = (spot - goalCenter).normalized;
            kicker.ResetPosition(spot + kickApproach * 2.0f);
            kicker.transform.rotation = Quaternion.LookRotation(-kickApproach, Vector3.up);

            // Position GK on goal line
            goalkeeper.ResetPosition(new Vector3(0f, 0f, goalCenter.z));
            goalkeeper.transform.rotation = Quaternion.LookRotation(kickApproach, Vector3.up);
        }
    }
}
