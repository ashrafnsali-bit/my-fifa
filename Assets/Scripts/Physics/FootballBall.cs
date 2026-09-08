using UnityEngine;
using Football.Core;

namespace Football.PhysicsEngine
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class FootballBall : MonoBehaviour
    {
        public static FootballBall Instance { get; private set; }

        [Header("Ball Physical Constants (FIFA Regulation)")]
        [Tooltip("Mass in kg (Size 5 regulation is 410-450g)")]
        public float ballMass = 0.43f;
        [Tooltip("Radius in meters (Size 5 regulation is ~11cm)")]
        public float ballRadius = 0.11f;

        [Header("Aerodynamics (Magnus & Drag)")]
        [Tooltip("Air density at sea level kg/m^3")]
        public float airDensity = 1.225f;
        [Tooltip("Drag coefficient (smooth sphere ~0.47, dimpled/stitched ~0.25-0.30)")]
        public float dragCoefficient = 0.28f;
        [Tooltip("Lift coefficient multiplier for Magnus spin")]
        public float liftCoefficient = 0.22f;
        [Tooltip("Spin angular velocity decay per second")]
        public float spinDamping = 0.85f;

        [Header("Turf Friction & Restitution")]
        [Tooltip("Turf rolling resistance force multiplier")]
        public float rollingResistance = 0.30f;
        [Tooltip("Bounce restitution coefficient on grass (0.6 - 0.7)")]
        public float turfRestitution = 0.68f;

        [Header("State Tracking")]
        public int lastKickingPlayerId = -1;
        public int lastTeamPossession = 1;
        public bool isGrounded { get; private set; }

        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private float crossSectionalArea;

        public Rigidbody BallRigidbody => rb;
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
        public Vector3 AngularVelocity => rb != null ? rb.angularVelocity : Vector3.zero;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();

            // Configure FIFA specs
            rb.mass = ballMass;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Compute frontal area A = pi * r^2
            crossSectionalArea = Mathf.PI * ballRadius * ballRadius;
        }

        private void FixedUpdate()
        {
            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;

            // Check if ball is touching or skimming turf
            isGrounded = transform.position.y <= (ballRadius + 0.05f);

            if (speed > 0.01f)
            {
                // 1. Quadratic Aerodynamic Drag: F_d = -0.5 * rho * Cd * A * |v| * v
                float dragMagnitude = 0.5f * airDensity * dragCoefficient * crossSectionalArea * speed * speed;
                Vector3 dragForce = -v.normalized * dragMagnitude;
                rb.AddForce(dragForce, ForceMode.Force);

                // 2. Magnus Effect Force: F_m = 0.5 * rho * Cl * A * r * (w x v)
                Vector3 w = rb.angularVelocity;
                if (w.sqrMagnitude > 0.1f)
                {
                    Vector3 magnusDir = Vector3.Cross(w, v);
                    float magnusMagnitude = 0.5f * airDensity * liftCoefficient * crossSectionalArea * ballRadius * magnusDir.magnitude;
                    if (magnusDir.sqrMagnitude > 0.0001f)
                    {
                        rb.AddForce(magnusDir.normalized * magnusMagnitude, ForceMode.Force);
                    }
                }

                // 3. Turf Rolling Resistance
                if (isGrounded)
                {
                    Vector3 friction = -new Vector3(v.x, 0f, v.z).normalized * (rollingResistance * ballMass * 9.81f * Time.fixedDeltaTime);
                    rb.AddForce(friction, ForceMode.Impulse);
                }
            }

            // Spin decay
            if (rb.angularVelocity.sqrMagnitude > 0.01f)
            {
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, spinDamping * Time.fixedDeltaTime);
            }

            // Continuous out-of-bounds safety check
            CheckPitchBoundaries();
        }

        public void Kick(Vector3 velocity, Vector3 spinAngularVelocity, int kickingPlayerId, int teamId)
        {
            lastKickingPlayerId = kickingPlayerId;
            lastTeamPossession = teamId;

            rb.linearVelocity = velocity;
            rb.angularVelocity = spinAngularVelocity;
        }

        public void Pass(Vector3 targetPos, float speed, bool lobbed, int kickingPlayerId, int teamId)
        {
            lastKickingPlayerId = kickingPlayerId;
            lastTeamPossession = teamId;

            Vector3 diff = targetPos - transform.position;
            Vector3 horizontalDiff = new Vector3(diff.x, 0f, diff.z);
            float dist = horizontalDiff.magnitude;

            if (lobbed)
            {
                // Ballistic trajectory formula for lofted pass
                float gravity = Mathf.Abs(UnityEngine.Physics.gravity.y);
                float flightTime = Mathf.Clamp(dist / speed, 0.6f, 2.2f);
                float vz = dist / flightTime;
                float vy = (diff.y + 0.5f * gravity * flightTime * flightTime) / flightTime;

                Vector3 initialVelocity = horizontalDiff.normalized * vz + Vector3.up * vy;
                // Add back-spin to lofted ball
                Vector3 backSpin = Vector3.Cross(horizontalDiff.normalized, Vector3.up) * -15f;

                Kick(initialVelocity, backSpin, kickingPlayerId, teamId);
            }
            else
            {
                // Crisp ground pass with subtle forward roll
                Vector3 groundVelocity = horizontalDiff.normalized * speed;
                groundVelocity.y = 0.2f; // Slight elevation to prevent turf snag
                Vector3 topSpin = Vector3.Cross(Vector3.up, horizontalDiff.normalized) * (speed / ballRadius);

                Kick(groundVelocity, topSpin, kickingPlayerId, teamId);
            }

            GameEvents.TriggerPassCompleted(teamId, lobbed ? PassType.Lobbed : PassType.Ground);
        }

        private void OnCollisionEnter(Collision collision)
        {
            string objName = collision.gameObject.name;

            // Play turf or post impact audio based on collided object
            if (objName.Contains("Post") || objName.Contains("Crossbar") || objName.Contains("Woodwork"))
            {
                GameEvents.TriggerWoodworkHit();
            }

            // Custom bounce restitution dampening on turf
            if (objName.Contains("Turf") || objName.Contains("Pitch") || objName.Contains("Ground") || objName.Contains("Stripe"))
            {
                Vector3 normal = collision.contacts[0].normal;
                Vector3 incomingVel = rb.linearVelocity;
                float normalSpeed = Vector3.Dot(incomingVel, normal);

                if (normalSpeed < 0)
                {
                    // Reflect normal component with turf restitution
                    Vector3 reflectedNormal = -normal * normalSpeed * turfRestitution;
                    Vector3 tangentVel = incomingVel - normal * normalSpeed;
                    rb.linearVelocity = tangentVel * 0.9f + reflectedNormal;
                }
            }
        }

        private void CheckPitchBoundaries()
        {
            Vector3 pos = transform.position;

            // Only evaluate if ball is well outside boundary and not inside goal
            if (!PitchConstants.IsInsidePitch(pos, 0.5f))
            {
                bool isPastGoalLine = Mathf.Abs(pos.z) > PitchConstants.HalfLength;
                bool isInsideGoalMouth = Mathf.Abs(pos.x) <= (PitchConstants.GoalWidth * 0.5f) && pos.y <= PitchConstants.GoalHeight;

                if (isPastGoalLine && isInsideGoalMouth)
                {
                    // Goal will be caught by Goal trigger
                    return;
                }

                // If outside touchline (X boundaries)
                if (Mathf.Abs(pos.x) > PitchConstants.HalfWidth)
                {
                    // Throw-in to opponent of last team touching
                    int throwInTeam = (lastTeamPossession == 1) ? 2 : 1;
                    Vector3 throwInPos = PitchConstants.GetThrowInPosition(pos);
                    GameEvents.TriggerSetPieceInitiated(MatchState.ThrowIn, throwInPos, throwInTeam);
                }
                // If outside endline (Z boundaries)
                else if (Mathf.Abs(pos.z) > PitchConstants.HalfLength)
                {
                    bool touchedByAttacker = (pos.z > 0 && lastTeamPossession == 1) || (pos.z < 0 && lastTeamPossession == 2);
                    if (touchedByAttacker)
                    {
                        // Goal Kick to defending team
                        int defendingTeam = (pos.z > 0) ? 2 : 1;
                        Vector3 goalKickPos = new Vector3(0f, 0.1f, (pos.z > 0 ? PitchConstants.HalfLength - 5.5f : -PitchConstants.HalfLength + 5.5f));
                        GameEvents.TriggerSetPieceInitiated(MatchState.GoalKick, goalKickPos, defendingTeam);
                    }
                    else
                    {
                        // Corner Kick to attacking team
                        int attackingTeam = (pos.z > 0) ? 1 : 2;
                        bool homeEnd = pos.z < 0;
                        bool leftSide = pos.x < 0;
                        Vector3 cornerPos = PitchConstants.GetCornerPosition(homeEnd, leftSide);
                        GameEvents.TriggerSetPieceInitiated(MatchState.CornerKick, cornerPos, attackingTeam);
                    }
                }
            }
        }

        public void ResetPosition(Vector3 newPosition)
        {
            transform.position = newPosition;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
