using UnityEngine;
using Football.Core;
using Football.Locomotion;

[RequireComponent(typeof(BoxCollider))]
public class Goal : MonoBehaviour
{
    public int teamGoalID; 
    private float lastTriggerTime = -10f;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameEvents.CurrentMatchState != MatchState.InPlay) return;
        if (Time.time - lastTriggerTime < 4.0f) return;

        if (other.name.Contains("Ball") || other.GetComponent<Football.PhysicsEngine.FootballBall>() != null)
        {
            // CRITICAL: If any goalkeeper is currently holding the ball in their hands,
            // the ball is NOT in active play - do NOT count a goal!
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p != null && p.isHoldingBallInHands)
                {
                    Debug.Log("Goal trigger ignored: Goalkeeper is holding the ball in hands.");
                    return;
                }
            }

            lastTriggerTime = Time.time;
            Debug.Log("Goal! Ball entered goal of team: " + teamGoalID);
            
            int scoringTeam = (teamGoalID == 1) ? 2 : 1;

            GameEvents.TriggerGoalScored(scoringTeam, other.transform.position);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TeamScored(scoringTeam);
            }
        }
    }
}
