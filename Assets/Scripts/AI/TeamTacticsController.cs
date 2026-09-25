using System.Collections.Generic;
using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Tactics
{
    public enum TeamMentality
    {
        UltraDefensive,
        Defensive,
        Balanced,
        Attacking,
        UltraAttacking
    }

    public class TeamTacticsController : MonoBehaviour
    {
        [Header("Team Identity")]
        public int teamId = 2; // Default to Team 2 (AI opponent)
        public FormationType formation = FormationType.Formation_4_3_3;
        public TeamMentality mentality = TeamMentality.Attacking;
        public AIDifficultySettings difficultySettings;

        [Header("Tactical Dimensions")]
        [Range(20f, 65f)] public float teamWidth = 48.0f;
        [Range(15f, 50f)] public float defensiveLineDepth = 28.0f;
        [Range(0f, 1f)] public float ballFollowFactor = 0.45f;

        [Header("Roster References")]
        public List<FootballAIPlayer> teamPlayers = new List<FootballAIPlayer>();
        public FootballAIPlayer closestPlayerToBall { get; private set; }
        public FootballAIPlayer secondClosestPlayerToBall { get; private set; }

        private FormationSlot[] formationSlots;

        private void Awake()
        {
            if (difficultySettings == null)
            {
                difficultySettings = AIDifficultySettings.GetPreset(DifficultyLevel.WorldClass);
            }
            formationSlots = FormationData.GetFormationSlots(formation);
        }

        private void Update()
        {
            UpdateClosestPlayerToBall();
            UpdatePlayerTacticalAnchors();
        }

        private void UpdateClosestPlayerToBall()
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            Vector3 ballPos = ball.transform.position;
            float closestSqrDist = float.MaxValue;
            float secondClosestSqrDist = float.MaxValue;
            FootballAIPlayer bestPlayer = null;
            FootballAIPlayer secondBestPlayer = null;

            foreach (var p in teamPlayers)
            {
                if (p == null || p.RuntimeState.isSentOff || p.RuntimeState.attributes.position == PlayerPosition.GK)
                    continue;

                float sqrDist = (p.transform.position - ballPos).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    secondClosestSqrDist = closestSqrDist;
                    secondBestPlayer = bestPlayer;

                    closestSqrDist = sqrDist;
                    bestPlayer = p;
                }
                else if (sqrDist < secondClosestSqrDist)
                {
                    secondClosestSqrDist = sqrDist;
                    secondBestPlayer = p;
                }
            }

            // The two closest outfield players always actively support and challenge around the ball
            closestPlayerToBall = bestPlayer;
            secondClosestPlayerToBall = secondBestPlayer;
        }

        private void UpdatePlayerTacticalAnchors()
        {
            var ball = FootballBall.Instance;
            Vector3 ballPos = ball != null ? ball.transform.position : Vector3.zero;

            // Check if our team has possession
            bool teamHasBall = false;
            for (int i = 0; i < teamPlayers.Count; i++)
            {
                if (teamPlayers[i] != null && teamPlayers[i].RuntimeState != null && teamPlayers[i].RuntimeState.hasBall)
                {
                    teamHasBall = true;
                    break;
                }
            }

            // Mentality shift along Z-axis (push deep into opponent half when attacking)
            float mentalityZShift = 0f;
            switch (mentality)
            {
                case TeamMentality.UltraDefensive: mentalityZShift = -6.0f; break;
                case TeamMentality.Defensive: mentalityZShift = -2.0f; break;
                case TeamMentality.Balanced: mentalityZShift = 4.0f; break;
                case TeamMentality.Attacking: mentalityZShift = 12.0f; break;
                case TeamMentality.UltraAttacking: mentalityZShift = 18.0f; break;
            }

            if (teamHasBall)
            {
                mentalityZShift += 8.0f; // High surge forward when in possession
            }

            for (int i = 0; i < teamPlayers.Count; i++)
            {
                var p = teamPlayers[i];
                if (p == null) continue;

                int slotIdx = p.formationSlotIndex;
                if (formationSlots == null || slotIdx < 0 || slotIdx >= formationSlots.Length) continue;

                var slot = formationSlots[slotIdx];
                Vector3 basePos = FormationData.GetWorldPosition(slot, teamId, mentalityZShift);

                // Outfield players dynamically follow play and shift towards the ball line
                if (slot.position != PlayerPosition.GK)
                {
                    float ballInfluenceZ = ballPos.z * (teamHasBall ? 0.75f : 0.55f);
                    float ballInfluenceX = ballPos.x * 0.25f;

                    basePos.x += ballInfluenceX;
                    basePos.z += ballInfluenceZ;
                }

                p.SetTacticalAnchor(basePos);
            }
        }

        public void SetFormation(FormationType newFormation)
        {
            formation = newFormation;
            formationSlots = FormationData.GetFormationSlots(newFormation);
        }
    }
}
