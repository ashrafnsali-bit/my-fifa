using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;

namespace Football.Locomotion
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerRuntimeState))]
    public class FootballPlayerLocomotion : MonoBehaviour
    {
        [Header("Locomotion Tuning")]
        public float baseJogSpeed = 5.2f;
        public float baseSprintSpeed = 8.8f;
        public float accelerationRate = 18.0f;
        public float decelerationRate = 22.0f;
        public float turnSpeed = 720.0f;

        [Header("Dribble Magnet")]
        public float dribbleRadius = 1.65f;
        public float dribblePushOffset = 0.65f;
        public float dribbleTouchForce = 5.5f;

        [Header("Goalkeeper Hold Limit")]
        public const float MaxGoalkeeperHoldTime = 2.0f; // Strictly under 3 seconds!
        private float goalkeeperHoldDuration = 0f;
        // After releasing the ball, the GK cannot catch it again for this many seconds
        private float gkReleaseCooldown = 0f;
        private const float GKReleaseCooldownDuration = 6.0f;
        /// <summary>True while the GK is in release cooldown and should NOT re-catch their own ball.</summary>
        public bool IsInReleaseCooldown => gkReleaseCooldown > 0f;

        [Header("Blob Shadow Grounding")]
        [Tooltip("Blob shadow object directly underneath player feet")]
        public Transform blobShadow;
        public float groundY = 0.015f;

        private Rigidbody rb;
        private CapsuleCollider col;
        private PlayerRuntimeState runtimeState;
        private Vector3 targetMoveDirection;
        private bool isSprinting;
        private Vector3 currentVelocity;
        private Vector3 initialSpawnPosition;

        public bool IsSprinting => isSprinting;
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
        public PlayerPosition Position => runtimeState != null && runtimeState.attributes != null ? runtimeState.attributes.position : PlayerPosition.CM;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();
            runtimeState = GetComponent<PlayerRuntimeState>();

            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            initialSpawnPosition = transform.position;
        }

        public void SetMovementInput(Vector2 input, bool sprintRequested)
        {
            SetWorldMovementInput(new Vector3(input.x, 0f, input.y), sprintRequested);
        }

        public void SetWorldMovementInput(Vector3 worldDir, bool sprintRequested)
        {
            if (runtimeState.isSentOff)
            {
                targetMoveDirection = Vector3.zero;
                isSprinting = false;
                return;
            }

            if (worldDir.sqrMagnitude > 1.0f) worldDir.Normalize();

            targetMoveDirection = worldDir;
            isSprinting = sprintRequested && runtimeState.currentStamina > 5.0f && worldDir.sqrMagnitude > 0.05f;
        }

        private void FixedUpdate()
        {
            if (runtimeState.isSentOff) return;

            float dt = Time.fixedDeltaTime;
            UpdateLocomotion(dt);
            UpdateDribbling(dt);
        }

        private void LateUpdate()
        {
            UpdateBlobShadowPosition();
        }

        public void UpdateBlobShadowPosition()
        {
            if (blobShadow != null)
            {
                // Strictly match Player Transform on X and Z with 0 offset.
                // Keep Y fixed at ground level.
                blobShadow.position = new Vector3(transform.position.x, groundY, transform.position.z);
            }
        }

        private void UpdateLocomotion(float dt)
        {
            // Compute attribute-influenced speeds
            float sprintAttr = runtimeState.attributes != null ? runtimeState.attributes.sprintSpeed : 75f;
            float accelAttr = runtimeState.attributes != null ? runtimeState.attributes.acceleration : 75f;
            float speedMult = runtimeState.SpeedMultiplier;

            float maxSpeed = isSprinting
                ? Mathf.Lerp(7.0f, baseSprintSpeed + 1.5f, sprintAttr / 100f) * speedMult
                : baseJogSpeed * speedMult;

            Vector3 targetVel = targetMoveDirection * maxSpeed;

            // Smooth acceleration / deceleration with explosive agility boost for goalkeepers
            bool isGK = runtimeState.attributes != null && runtimeState.attributes.position == PlayerPosition.GK;
            float gkAccelMult = isGK ? 2.8f : 1.0f;
            float currentAccel = targetMoveDirection.sqrMagnitude > 0.01f ? accelerationRate * (accelAttr / 70f) * gkAccelMult : decelerationRate;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVel, currentAccel * dt);

            // Move rigidbody
            Vector3 nextPos = rb.position + currentVelocity * dt;
            nextPos = PitchConstants.ClampToPitch(nextPos, 0.5f);
            rb.MovePosition(nextPos);

            // Rotation towards movement or ball if goalkeeper
            var ballObj = FootballBall.Instance;

            // Goalkeepers lock facing onto ball for instant saving stance; outfield players face motion
            if (isGK && !runtimeState.isHoldingBallInHands && ballObj != null)
            {
                Vector3 toBallDir = ballObj.transform.position - transform.position;
                toBallDir.y = 0f;
                if (toBallDir.sqrMagnitude > 0.05f)
                {
                    Quaternion faceBallRot = Quaternion.LookRotation(toBallDir.normalized, Vector3.up);
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, faceBallRot, turnSpeed * 1.5f * dt));
                }
            }
            else if (targetMoveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetMoveDirection, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, turnSpeed * dt));
            }

            // Stamina update
            if (isSprinting)
            {
                runtimeState.ConsumeStamina(dt);
                runtimeState.currentState = MovementState.Sprint;
            }
            else if (targetMoveDirection.sqrMagnitude > 0.01f)
            {
                runtimeState.RecoverStamina(dt * 0.4f);
                runtimeState.currentState = MovementState.Jog;
            }
            else
            {
                runtimeState.RecoverStamina(dt);
                runtimeState.currentState = MovementState.Idle;
            }
        }

        [Header("Kick Release")]
        private float ballKickedTimer = 0f;

        public void OnBallKicked(float cooldown = 0.6f)
        {
            ballKickedTimer = cooldown;
            if (runtimeState != null)
            {
                runtimeState.hasBall = false;
                if (runtimeState.currentState == MovementState.Dribbling)
                {
                    runtimeState.currentState = MovementState.Jog;
                }
            }
        }

        /// <summary>Call this after any GK punt/throw to block re-catch for GKReleaseCooldownDuration seconds.</summary>
        public void StartGoalkeeperReleaseCooldown()
        {
            gkReleaseCooldown = GKReleaseCooldownDuration;
            ballKickedTimer = Mathf.Max(ballKickedTimer, 1.8f);
        }

        private void UpdateDribbling(float dt)
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // IRONCLAD: Only interact with ball if the match is actively in play!
            // When a goal is scored or during kickoff, no player or goalkeeper can catch, dribble, or claim the ball!
            if (GameEvents.CurrentMatchState != MatchState.InPlay)
            {
                runtimeState.hasBall = false;
                runtimeState.isHoldingBallInHands = false;
                return;
            }

            // IRONCLAD: If the ball has crossed the goal line (inside net or out of bounds),
            // neither goalkeepers nor outfield players can reach inside the net to take it!
            if (Mathf.Abs(ball.transform.position.z) >= PitchConstants.HalfLength - 0.15f)
            {
                runtimeState.hasBall = false;
                runtimeState.isHoldingBallInHands = false;
                return;
            }

            // Tick down the GK re-catch cooldown every frame
            if (gkReleaseCooldown > 0f)
                gkReleaseCooldown -= dt;

            // 0. If ball was recently kicked, do not pull it back!
            if (ballKickedTimer > 0f)
            {
                ballKickedTimer -= dt;
                if (runtimeState != null) runtimeState.hasBall = false;
                return;
            }

            // High speed balls (shots/passes) cannot be clamped by dribble magnet
            if (ball.Velocity.magnitude > 5.0f)
            {
                return;
            }

            // 1. Goalkeeper holding ball in hands (Max 2.0 seconds strictly!)
            if (runtimeState.isHoldingBallInHands)
            {
                runtimeState.hasBall = true;
                runtimeState.currentState = MovementState.HoldingBallHands;

                // Lock ball firmly at chest/hands level
                Vector3 handsPos = transform.position + transform.forward * 0.45f + Vector3.up * 1.15f;
                ball.BallRigidbody.linearVelocity = Vector3.zero;
                ball.BallRigidbody.angularVelocity = Vector3.zero;
                ball.transform.position = handsPos;

                ball.lastTeamPossession = runtimeState.teamId;
                ball.lastKickingPlayerId = runtimeState.jerseyNumber;

                // STRICT RULE: Goalkeeper cannot hold ball for more than 2 seconds (well under 3s limit)
                goalkeeperHoldDuration += dt;
                if (goalkeeperHoldDuration >= MaxGoalkeeperHoldTime)
                {
                    goalkeeperHoldDuration = 0f;
                    // Pre-set the kicked timer BEFORE calling punt so the dribble magnet
                    // does NOT re-catch the ball on the very next FixedUpdate tick!
                    ballKickedTimer = 1.8f;
                    runtimeState.isHoldingBallInHands = false;
                    runtimeState.hasBall = false;
                    // Block re-catch for 6 seconds after auto-punt
                    StartGoalkeeperReleaseCooldown();
                    var actions = GetComponent<FootballPlayerActions>();
                    if (actions != null)
                    {
                        actions.ExecuteGoalkeeperPunt();
                    }
                }
                return;
            }
            else
            {
                goalkeeperHoldDuration = 0f;
            }

            bool isGK = runtimeState.attributes != null && runtimeState.attributes.position == PlayerPosition.GK;
            Vector3 ownGoal = isGK ? PitchConstants.GetDefendingGoalCenter(runtimeState.teamId) : Vector3.zero;
            bool insideBox = isGK && (Vector3.Distance(transform.position, ownGoal) <= 24.0f);

            // 2. Goalkeeper catching or blocking/saving ball inside penalty box
            if (isGK && insideBox && !runtimeState.isHoldingBallInHands)
            {
                float totalDist = Vector3.Distance(transform.position, ball.transform.position);
                float ballHeight = ball.transform.position.y;
                float ballSpeed = ball.Velocity.magnitude;

                // CRITICAL: After a punt/release, block re-catch UNLESS it's a real opponent shot
                bool allowCatch = true;
                if (gkReleaseCooldown > 0f)
                {
                    // During cooldown: only allow catching if ball is heading FAST toward the goal
                    // (i.e., it's a real shot from the opponent, not the GK's own return ball)
                    Vector3 ownGoalCenter = PitchConstants.GetDefendingGoalCenter(runtimeState.teamId);
                    Vector3 toGoal = (ownGoalCenter - ball.transform.position).normalized;
                    float speedTowardsGoal = Vector3.Dot(ball.Velocity.normalized, toGoal);
                    bool isThreatShot = speedTowardsGoal > 0.55f && ballSpeed > 5.0f;
                    allowCatch = isThreatShot;
                }

                if (allowCatch)
                {
                    // Immediate clean catch if ball is close and controllable
                    if (totalDist <= 2.8f && ballHeight <= 2.8f)
                    {
                        if (ballSpeed < 13.0f || totalDist <= 2.0f)
                        {
                            CatchBallInHands(ball);
                            return;
                        }
                        else
                        {
                            // High-speed blast: diving block / parry!
                            ExecuteGoalkeeperBlockSave(ball);
                            return;
                        }
                    }

                    // If shot is passing close through diving reach (up to 4.2m): Parrying / Diving Block!
                    if (totalDist <= 4.2f && (ballSpeed > 5.0f || totalDist <= 3.0f))
                    {
                        ExecuteGoalkeeperBlockSave(ball);
                        return;
                    }
                }
            }

            // 3. Outfield players: only control and dribble with feet!
            if (!isGK)
            {
                runtimeState.isHoldingBallInHands = false;
            }

            Vector3 toBall = ball.transform.position - transform.position;
            toBall.y = 0;
            float dist = toBall.magnitude;

            if (dist <= dribbleRadius)
            {
                runtimeState.hasBall = true;
                runtimeState.currentState = MovementState.Dribbling;

                // Close-control touch: keep ball safely at boots/feet
                Vector3 desiredBallPos = transform.position + transform.forward * dribblePushOffset;
                desiredBallPos.y = ball.ballRadius;

                Vector3 ballCorrection = desiredBallPos - ball.transform.position;
                ballCorrection.y = 0;

                if (targetMoveDirection.sqrMagnitude > 0.05f)
                {
                    // Running with ball: guide smoothly in stride
                    Vector3 touchVel = (transform.forward * currentVelocity.magnitude + ballCorrection * 4.0f);
                    ball.BallRigidbody.linearVelocity = Vector3.Lerp(ball.BallRigidbody.linearVelocity, touchVel, 10.0f * dt);
                }
                else
                {
                    // Stopped / standing still: snug ball right at boots, zero roll-away!
                    ball.BallRigidbody.linearVelocity = Vector3.Lerp(ball.BallRigidbody.linearVelocity, ballCorrection * 2.0f, 15.0f * dt);
                    ball.BallRigidbody.angularVelocity = Vector3.Lerp(ball.BallRigidbody.angularVelocity, Vector3.zero, 15.0f * dt);
                }

                ball.lastTeamPossession = runtimeState.teamId;
                ball.lastKickingPlayerId = runtimeState.jerseyNumber;
            }
            else
            {
                if (runtimeState.hasBall && dist > dribbleRadius * 1.5f)
                {
                    runtimeState.hasBall = false;
                }
            }
        }

        public void CatchBallInHands(FootballBall ball)
        {
            if (runtimeState.attributes == null || runtimeState.attributes.position != PlayerPosition.GK) return;
            if (GameEvents.CurrentMatchState != MatchState.InPlay) return;
            if (Mathf.Abs(ball.transform.position.z) >= PitchConstants.HalfLength - 0.15f) return;

            runtimeState.hasBall = true;
            runtimeState.isHoldingBallInHands = true;
            runtimeState.currentState = MovementState.HoldingBallHands;
            goalkeeperHoldDuration = 0f;

            ball.BallRigidbody.linearVelocity = Vector3.zero;
            ball.BallRigidbody.angularVelocity = Vector3.zero;

            Vector3 handsPos = transform.position + transform.forward * 0.45f + Vector3.up * 1.15f;
            ball.transform.position = handsPos;

            ball.lastTeamPossession = runtimeState.teamId;
            ball.lastKickingPlayerId = runtimeState.jerseyNumber;
        }

        public void ExecuteGoalkeeperBlockSave(FootballBall ball)
        {
            if (runtimeState.attributes == null || runtimeState.attributes.position != PlayerPosition.GK) return;
            if (GameEvents.CurrentMatchState != MatchState.InPlay) return;
            if (Mathf.Abs(ball.transform.position.z) >= PitchConstants.HalfLength - 0.15f) return;

            bool diveRight = (ball.transform.position.x > transform.position.x);
            bool highSave = (ball.transform.position.y > 1.2f);

            var anim = GetComponent<ProceduralRunnerAnimator>();
            if (anim != null)
            {
                anim.TriggerGoalkeeperDive(diveRight, highSave);
            }

            // Goalkeeper explosive physical leap/dive impulse towards the ball
            Vector3 diveImpulse = (diveRight ? transform.right : -transform.right) * 5.2f + transform.forward * 1.5f;
            if (highSave) diveImpulse += Vector3.up * 2.5f;
            if (rb != null)
            {
                rb.linearVelocity = diveImpulse;
            }

            // Deflect shot away from goal (sideways towards wing/corner and slightly forward)
            Vector3 pushDir = transform.forward * 0.45f + (diveRight ? transform.right : -transform.right) * 1.35f + Vector3.up * 0.35f;
            float deflectSpeed = Mathf.Clamp(ball.Velocity.magnitude * 0.75f, 11.0f, 19.0f);

            ball.Kick(pushDir.normalized * deflectSpeed, Vector3.up * (diveRight ? 18f : -18f), runtimeState.jerseyNumber, runtimeState.teamId);

            GameEvents.TriggerGoalkeeperSave(runtimeState.teamId);
            GameEvents.TriggerWoodworkHit(); // Impact audio feedback
        }

        public void ReleaseBallFromHands()
        {
            runtimeState.isHoldingBallInHands = false;
            if (runtimeState.currentState == MovementState.HoldingBallHands)
            {
                runtimeState.currentState = MovementState.Idle;
            }
        }

        public void ResetPosition(Vector3? customPosition = null)
        {
            Vector3 target = customPosition ?? initialSpawnPosition;
            transform.position = target;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            currentVelocity = Vector3.zero;
            targetMoveDirection = Vector3.zero;
            runtimeState.hasBall = false;
            runtimeState.isHoldingBallInHands = false;
        }
    }
}
