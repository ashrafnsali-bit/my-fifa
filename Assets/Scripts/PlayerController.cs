using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float kickForce = 10f;
    public Transform defaultPosition;
    
    private Rigidbody rb;
    private Vector3 movement;
    private Vector3 initialPos;

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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name.Contains("Ball") || collision.gameObject.GetComponent<Football.PhysicsEngine.FootballBall>() != null || collision.gameObject.GetComponent<Ball>() != null)
        {
            Ball ballScript = collision.gameObject.GetComponent<Ball>();
            if (ballScript != null)
            {
                // Kick the ball
                Vector3 kickDir = (collision.transform.position - transform.position).normalized;
                kickDir.y = 0; // Keep kick horizontal
                
                if (movement.magnitude > 0)
                {
                    kickDir = (kickDir + movement.normalized).normalized;
                }

                ballScript.Kick(kickDir, kickForce);
            }
        }
    }

    public void ResetPosition()
    {
        transform.position = initialPos;
        rb.linearVelocity = Vector3.zero;
        movement = Vector3.zero;
    }
}
