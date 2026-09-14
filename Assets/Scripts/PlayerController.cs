using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float kickForce = 22f;
    public Transform defaultPosition;

    [Header("Kicking")]
    public float kickRange = 3.5f;
    public bool autoKickOnCollision = false;

    [Header("Blob Shadow")]
    public Transform blobShadow;
    public float groundY = 0.015f;
    
    private Rigidbody rb;
    private Vector3 movement;
    private Vector3 initialPos;
    private GameObject ballInContact;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Freeze rotation so the player doesn't tip over
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Start()
    {
        initialPos = defaultPosition != null ? defaultPosition.position : transform.position;
    }

    void Update()
    {
        movement = Vector3.zero;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.z += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.z -= 1f;
        }

        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f)
            {
                movement.x = stick.x;
                movement.z = stick.y;
            }
        }

        movement.y = 0; // Keep movement on horizontal plane

        // Kick the ball when pressing or holding Spacebar (Keyboard) or Action Buttons (Gamepad)
        bool kickRequested = false;

        // New Input System
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.spaceKey.isPressed))
        {
            kickRequested = true;
        }
        if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame))
        {
            kickRequested = true;
        }

        // Legacy Input Manager fallback
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKey(KeyCode.Space))
        {
            kickRequested = true;
        }

        if (kickRequested)
        {
            TryKickBall();
        }
    }

    void FixedUpdate()
    {
        // Move the player
        rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
        
        // Optional: Face the direction of movement
        if (movement != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 10f * Time.fixedDeltaTime));
        }
    }

    void LateUpdate()
    {
        if (blobShadow != null)
        {
            // Strictly match Player Transform on X and Z with 0 offset.
            // Keep Y fixed at ground level.
            blobShadow.position = new Vector3(transform.position.x, groundY, transform.position.z);
        }
    }

    public bool TryKickBall()
    {
        // 1. Check if a ball is currently in physical contact with player
        GameObject targetBall = ballInContact;

        // 2. Check balls within kick range via Physics overlap
        if (targetBall == null)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, kickRange);
            float closestDist = float.MaxValue;
            foreach (var col in colliders)
            {
                if (col.gameObject == gameObject) continue;
                if (IsBallObject(col.gameObject))
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        targetBall = col.gameObject;
                    }
                }
            }
        }

        // 3. Fallback: check global Ball instance or FootballBall instance if nearby
        if (targetBall == null)
        {
            var ballComponent = FindFirstObjectByType<Ball>();
            if (ballComponent != null && Vector3.Distance(transform.position, ballComponent.transform.position) <= kickRange)
            {
                targetBall = ballComponent.gameObject;
            }
            else
            {
                var fbBall = Football.PhysicsEngine.FootballBall.Instance;
                if (fbBall != null && Vector3.Distance(transform.position, fbBall.transform.position) <= kickRange)
                {
                    targetBall = fbBall.gameObject;
                }
            }
        }

        if (targetBall != null)
        {
            KickBall(targetBall);
            return true;
        }

        return false;
    }

    public void KickBall(GameObject ballObj)
    {
        if (ballObj == null) return;

        Vector3 kickDir = ballObj.transform.position - transform.position;
        kickDir.y = 0f;

        if (kickDir.sqrMagnitude < 0.01f)
        {
            kickDir = transform.forward;
        }
        else
        {
            kickDir.Normalize();
        }

        if (movement.sqrMagnitude > 0.01f)
        {
            kickDir = (kickDir + movement.normalized).normalized;
        }

        kickDir.y = 0f;
        kickDir.Normalize();

        // Displace ball slightly forward to prevent getting stuck in player collider
        ballObj.transform.position += kickDir * 0.35f;

        // 1. Ball.cs component
        Ball ballScript = ballObj.GetComponent<Ball>();
        if (ballScript != null)
        {
            ballScript.Kick(kickDir, kickForce);
            return;
        }

        // 2. FootballBall.cs component
        var footballBall = ballObj.GetComponent<Football.PhysicsEngine.FootballBall>();
        if (footballBall != null)
        {
            Vector3 velocity = kickDir * (kickForce * 1.5f) + Vector3.up * 2.0f;
            footballBall.Kick(velocity, Vector3.zero, 0, 1);
            var loco = GetComponent<Football.Locomotion.FootballPlayerLocomotion>();
            if (loco != null) loco.OnBallKicked(0.6f);
            return;
        }

        // 3. Raw Rigidbody fallback
        Rigidbody ballRb = ballObj.GetComponent<Rigidbody>();
        if (ballRb != null)
        {
            ballRb.AddForce(kickDir * kickForce, ForceMode.Impulse);
        }
    }

    private bool IsBallObject(GameObject obj)
    {
        if (obj == null) return false;
        return obj.name.Contains("Ball") ||
               obj.GetComponent<Ball>() != null ||
               obj.GetComponent<Football.PhysicsEngine.FootballBall>() != null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsBallObject(collision.gameObject))
        {
            ballInContact = collision.gameObject;
            if (autoKickOnCollision)
            {
                KickBall(collision.gameObject);
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (IsBallObject(collision.gameObject))
        {
            ballInContact = collision.gameObject;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject == ballInContact)
        {
            ballInContact = null;
        }
    }

    public void ResetPosition()
    {
        transform.position = initialPos;
        rb.linearVelocity = Vector3.zero;
        movement = Vector3.zero;
        ballInContact = null;
    }
}
