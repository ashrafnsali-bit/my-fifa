using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    private Rigidbody rb;
    public float deceleration = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Apply custom drag/deceleration if needed
        if (rb.linearVelocity.magnitude > 0)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, deceleration * Time.fixedDeltaTime);
        }
    }

    public void Kick(Vector3 direction, float force)
    {
        // Keep the kick roughly on the horizontal plane
        direction.y = 0;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    public void KickWithSpin(Vector3 direction, float force, Vector3 spinAngularVel)
    {
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        rb.angularVelocity = spinAngularVel;
    }

    public void ResetPosition(Vector3 position)
    {
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
