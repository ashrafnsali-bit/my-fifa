using UnityEngine;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int team1Score = 0;
    public int team2Score = 0;

    [Header("UI References")]
    public TextMeshProUGUI scoreText; 

    [Header("Game Elements")]
    public Transform ballSpawn;
    
    public Ball ball;
    public PlayerController[] team1Players;
    public TeamAI[] team2Players;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateScoreUI();
    }

    public void TeamScored(int teamID)
    {
        if (teamID == 1)
        {
            team1Score++;
        }
        else if (teamID == 2)
        {
            team2Score++;
        }
        
        UpdateScoreUI();
        StartCoroutine(ResetPositionsAfterGoal());
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = team1Score + " - " + team2Score;
        }
        else
        {
            Debug.Log("Score: Team 1 (" + team1Score + ") - Team 2 (" + team2Score + ")");
        }
    }

    private IEnumerator ResetPositionsAfterGoal()
    {
        yield return new WaitForSeconds(2f);

        // Reset ball
        if (ball != null)
        {
            ball.ResetPosition(ballSpawn != null ? ballSpawn.position : Vector3.zero);
        }

        // Reset team 1 players
        if (team1Players != null)
        {
            foreach (var player in team1Players)
            {
                if (player != null) player.ResetPosition();
            }
        }

        // Reset team 2 players
        if (team2Players != null)
        {
            foreach (var ai in team2Players)
            {
                if (ai != null) ai.ResetPosition();
            }
        }
    }
}
