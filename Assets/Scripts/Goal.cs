using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Goal : MonoBehaviour
{
    public int teamGoalID; 

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
        if (other.name.Contains("Ball") || other.GetComponent<Football.PhysicsEngine.FootballBall>() != null)
        {
            Debug.Log("Goal! Ball entered goal of team: " + teamGoalID);
            
            int scoringTeam = (teamGoalID == 1) ? 2 : 1;

            Football.Core.GameEvents.TriggerGoalScored(scoringTeam, other.transform.position);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TeamScored(scoringTeam);
            }
        }
    }
}
