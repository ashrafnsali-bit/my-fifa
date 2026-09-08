using System.Collections.Generic;
using UnityEngine;
using Football.Core;
using Football.Locomotion;

namespace Football.Engine
{
    public class RefereeSystem : MonoBehaviour
    {
        public static RefereeSystem Instance { get; private set; }

        [Header("Discipline Severity")]
        [Range(0f, 1f)] public float foulStrictness = 0.6f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void OnEnable()
        {
            GameEvents.OnFoulCalled += EvaluateFoulDiscipline;
        }

        private void OnDisable()
        {
            GameEvents.OnFoulCalled -= EvaluateFoulDiscipline;
        }

        public void EvaluateFoulDiscipline(int offendingTeamId, Vector3 foulPos, bool isPenalty)
        {
            // Find offending player closest to foul position
            var players = FindObjectsOfType<PlayerRuntimeState>();
            PlayerRuntimeState culprit = null;
            float closestDist = float.MaxValue;

            foreach (var p in players)
            {
                if (p.teamId == offendingTeamId)
                {
                    float dist = Vector3.Distance(p.transform.position, foulPos);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        culprit = p;
                    }
                }
            }

            if (culprit == null) return;

            // Roll card probability based on strictness and whether tackle occurred in penalty box
            float roll = Random.value * (1.0f + (isPenalty ? 0.3f : 0f)) * foulStrictness;

            if (roll > 0.85f)
            {
                culprit.ApplyCard(CardType.Red);
            }
            else if (roll > 0.45f)
            {
                culprit.ApplyCard(CardType.Yellow);
            }
        }

        /// <summary>
        /// Checks whether the intended pass receiver is in an offside position at the moment of the pass.
        /// A player is in an offside position if they are nearer to the opponents' goal line than both the ball and the second-last opponent.
        /// </summary>
        public bool IsPlayerOffside(Vector3 receiverPos, int attackingTeamId, List<Vector3> defendingPlayerPositions)
        {
            // Cannot be offside in own half
            if (attackingTeamId == 1 && receiverPos.z < 0) return false;
            if (attackingTeamId == 2 && receiverPos.z > 0) return false;

            if (defendingPlayerPositions == null || defendingPlayerPositions.Count < 2) return false;

            // Sort defending players by proximity to their defending goal line
            // Team 1 attacks +Z, Team 2 defends +Z (goal line is at +HalfLength)
            // Team 2 attacks -Z, Team 1 defends -Z (goal line is at -HalfLength)
            if (attackingTeamId == 1)
            {
                defendingPlayerPositions.Sort((a, b) => b.z.CompareTo(a.z)); // Most forward (closest to +52.5) first
                float secondLastDefenderZ = defendingPlayerPositions[1].z;
                return receiverPos.z > secondLastDefenderZ;
            }
            else
            {
                defendingPlayerPositions.Sort((a, b) => a.z.CompareTo(b.z)); // Most forward (closest to -52.5) first
                float secondLastDefenderZ = defendingPlayerPositions[1].z;
                return receiverPos.z < secondLastDefenderZ;
            }
        }
    }
}
