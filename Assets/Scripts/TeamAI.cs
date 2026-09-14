using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TeamAI : MonoBehaviour
{
    public float moveSpeed = 8f; // زادت السرعة ليكونوا أسرع
    public float kickForce = 15f; // ركلات أقوى
    public Transform defaultPosition; 
    public Transform opponentGoal; 

    private Rigidbody rb;
    private Ball ball;
    private Vector3 initialPos;

    public float chaseRadius = 200f; // الآن سيلاحقون الكرة في كل مكان ولن يقفوا ثابتين!

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Start()
    {
        initialPos = defaultPosition != null ? defaultPosition.position : transform.position;
        ball = FindFirstObjectByType<Ball>();
    }

    void FixedUpdate()
    {
        if (ball == null) return;

        // RULE: If the opponent's goalkeeper is holding the ball, retreat to starting position.
        // Attacking the GK while he has the ball is illegal in football!
        var allStates = FindObjectsByType<Football.Locomotion.PlayerRuntimeState>(FindObjectsSortMode.None);
        foreach (var p in allStates)
        {
            // opponentGoal is used as team-identifier: if our opponentGoal != null and the GK
            // belongs to the opposing team (different opponentGoal direction) and holds the ball
            if (p != null && p.isHoldingBallInHands)
            {
                // Move back to initial position while GK holds ball
                Vector3 toInit = initialPos - transform.position;
                toInit.y = 0;
                if (toInit.magnitude > 0.5f)
                    rb.MovePosition(rb.position + toInit.normalized * moveSpeed * Time.fixedDeltaTime);
                return;
            }
        }

        // Calculate distance ignoring height
        Vector3 ballPosH = new Vector3(ball.transform.position.x, 0, ball.transform.position.z);
        Vector3 myPosH = new Vector3(transform.position.x, 0, transform.position.z);
        
        float distanceToBall = Vector3.Distance(myPosH, ballPosH);
        Vector3 targetPos;

        // Check if this player is the closest to the ball in their team
        bool isClosest = true;
        TeamAI[] allAI = FindObjectsByType<TeamAI>(FindObjectsSortMode.None);
        foreach (var ai in allAI)
        {
            if (ai != this && ai.opponentGoal == this.opponentGoal)
            {
                Vector3 aiPosH = new Vector3(ai.transform.position.x, 0, ai.transform.position.z);
                if (Vector3.Distance(aiPosH, ballPosH) < distanceToBall)
                {
                    isClosest = false;
                    break;
                }
            }
        }

        if (distanceToBall <= chaseRadius && (isClosest || distanceToBall < 3.0f))
        {
            targetPos = ball.transform.position;
        }
        else
        {
            targetPos = initialPos;
        }
        
        targetPos.y = transform.position.y; // Keep target at same height

        Vector3 moveDir = (targetPos - transform.position).normalized;
        
        if (Vector3.Distance(transform.position, targetPos) > 0.5f)
        {
            rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);
            
            // Face movement direction
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 10f * Time.fixedDeltaTime));
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Do NOT kick the ball if any goalkeeper is currently holding it!
        var allStates = FindObjectsByType<Football.Locomotion.PlayerRuntimeState>(FindObjectsSortMode.None);
        foreach (var p in allStates)
        {
            if (p != null && p.isHoldingBallInHands) return;
        }

        if (collision.gameObject.name.Contains("Ball") || collision.gameObject.GetComponent<Football.PhysicsEngine.FootballBall>() != null || collision.gameObject.GetComponent<Ball>() != null)
        {
            if (ball != null)
            {
                Vector3 kickDir;
                if (opponentGoal != null)
                {
                    kickDir = (opponentGoal.position - transform.position).normalized;
                }
                else
                {
                    kickDir = (collision.transform.position - transform.position).normalized;
                }
                
                kickDir.y = 0; // Keep horizontal
                ball.Kick(kickDir.normalized, kickForce);
            }
        }
    }

    public void ResetPosition()
    {
        transform.position = initialPos;
        rb.linearVelocity = Vector3.zero;
    }
}
