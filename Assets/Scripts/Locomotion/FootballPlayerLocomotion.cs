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
        public float baseJogSpeed = 5.4f;
        public float baseSprintSpeed = 9.2f;
        public float accelerationRate = 24.0f;
        public float decelerationRate = 30.0f;
        public float turnSpeed = 920.0f;

        [Header("Dribble Magnet")]
        public float dribbleRadius = 1.85f;
        public float dribblePushOffset = 0.70f;
        public float dribbleTouchForce = 6.0f;

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

            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Vector3 spawnP = transform.position;
            spawnP.y = 0f;
            transform.position = spawnP;
            initialSpawnPosition = spawnP;
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

            // Strict grounding enforcement against PhysX de-penetration bounce
            if (rb != null && Mathf.Abs(rb.position.y) > 0.001f)
            {
                Vector3 fixPos = rb.position;
                fixPos.y = 0f;
                rb.position = fixPos;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            }

            float dt = Time.fixedDeltaTime;
            UpdateLocomotion(dt);
            UpdateDribbling(dt);
        }

        private void LateUpdate()
        {
            if (Mathf.Abs(transform.position.y) > 0.001f)
            {
                Vector3 tp = transform.position;
                tp.y = 0f;
                transform.position = tp;
            }

            UpdateBlobShadowPosition();
        }

        public void UpdateBlobShadowPosition()
        {
            if (blobShadow != null)
            {
                blobShadow.position = new Vector3(transform.position.x, groundY, transform.position.z);
            }
        }

        private void UpdateLocomotion(float dt)
        {
            if (GameEvents.CurrentMatchState == MatchState.HalfTime || GameEvents.CurrentMatchState == MatchState.FullTime)
            {
                currentVelocity = Vector3.zero;
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                return;
            }

            bool isRestartState = (GameEvents.CurrentMatchState == MatchState.KickOff ||
                                   GameEvents.CurrentMatchState == MatchState.ThrowIn ||
                                   GameEvents.CurrentMatchState == MatchState.CornerKick ||
                                   GameEvents.CurrentMatchState == MatchState.GoalKick ||
                                   GameEvents.CurrentMatchState == MatchState.FreeKick ||
                                   GameEvents.CurrentMatchState == MatchState.PenaltyKick);

            // During restart states: if human player starts moving with WASD/stick, start dribbling into active play!
            if (isRestartState)
            {
                var input = GetComponent<FootballInputHandler>();
                if (input != null && input.isHumanControlled && targetMoveDirection.sqrMagnitude > 0.05f)
                {
                    GameEvents.TriggerMatchStateChanged(MatchState.InPlay);
                }
                else
                {
                    currentVelocity = Vector3.zero;
                    if (targetMoveDirection.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(targetMoveDirection, Vector3.up);
                        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, turnSpeed * dt));
                    }
                    return;
                }
            }

            // Compute attribute-influenced speeds & agility
            float sprintAttr = runtimeState.attributes != null ? runtimeState.attributes.sprintSpeed : 75f;
            float accelAttr = runtimeState.attributes != null ? runtimeState.attributes.acceleration : 75f;
            float agilityAttr = runtimeState.attributes != null ? runtimeState.attributes.agility : 75f;
            float strengthAttr = runtimeState.attributes != null ? runtimeState.attributes.strength : 70f;
            float speedMult = runtimeState.SpeedMultiplier;

            float maxSpeed = isSprinting
                ? Mathf.Lerp(7.5f, baseSprintSpeed + 1.8f, sprintAttr / 100f) * speedMult
                : baseJogSpeed * (1f + (sprintAttr - 70f) * 0.004f) * speedMult;

            Vector3 targetVel = targetMoveDirection * maxSpeed;

            // 1. Sharp 180-Degree Cut & Pivot Detection:
            // If player suddenly reverses direction while moving at speed, apply a sharp braking plant
            bool isSharpCut = false;
            if (currentVelocity.sqrMagnitude > 4.0f && targetMoveDirection.sqrMagnitude > 0.1f)
            {
                float dirAlignment = Vector3.Dot(currentVelocity.normalized, targetMoveDirection.normalized);
                if (dirAlignment < -0.40f) // Sharp reverse (> 115 degrees)
                {
                    isSharpCut = true;
                }
            }

            // First-step explosive acceleration burst vs sharp cut plant
            bool isStandingStart = currentVelocity.sqrMagnitude < 0.8f && targetMoveDirection.sqrMagnitude > 0.1f;
            float firstStepMultiplier = isStandingStart ? 1.45f : (isSharpCut ? 0.65f : 1.0f);

            // Goalkeeper explosive reflex multiplier
            bool isGK = runtimeState.attributes != null && runtimeState.attributes.position == PlayerPosition.GK;
            float gkAccelMult = isGK ? 3.0f : 1.0f;

            // 2. Realistic Inertia & Deceleration Curve
            float currentAccel;
            if (targetMoveDirection.sqrMagnitude > 0.01f)
            {
                if (isSharpCut)
                {
                    // Sharp cut: rapid brake first to plant cleats, then accelerate into cut
                    currentAccel = decelerationRate * 1.6f * (agilityAttr / 70f);
                }
                else
                {
                    currentAccel = accelerationRate * (accelAttr / 70f) * gkAccelMult * firstStepMultiplier;
                }
            }
            else
            {
                // Smooth natural coasting deceleration (Inertia glide to stop)
                currentAccel = decelerationRate * (agilityAttr / 70f) * 0.85f;
            }

            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVel, currentAccel * dt);

            // Move rigidbody strictly on ground plane
            Vector3 nextPos = rb.position + currentVelocity * dt;
            nextPos.y = 0.0f;
            nextPos = PitchConstants.ClampToPitch(nextPos, 0.5f);
            nextPos.y = 0.0f;
            rb.MovePosition(nextPos);

            // 3. Dynamic Smooth Rotation with Agility & Slerp
            float dynamicTurnSpeed = turnSpeed * (agilityAttr / 70f);
            var ballObj = FootballBall.Instance;

            // Goalkeepers lock facing onto ball for instant saving stance (or face pitch when ball is in net / kickoff)
            if (isGK && !runtimeState.isHoldingBallInHands && ballObj != null)
            {
                Vector3 faceDir;
                bool ballBehindGoal = Mathf.Abs(ballObj.transform.position.z) >= PitchConstants.HalfLength - 0.25f;
                if (ballBehindGoal || GameEvents.CurrentMatchState != MatchState.InPlay)
                {
                    Vector3 oppGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
                    faceDir = oppGoal - transform.position;
                }
                else
                {
                    faceDir = ballObj.transform.position - transform.position;
                }

                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.05f)
                {
                    Quaternion faceRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, faceRot, dynamicTurnSpeed * 1.5f * dt));
                }
            }
            else if (targetMoveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(targetMoveDirection, Vector3.up);
                // Use Slerp interpolation for authentic athletic weight transfer
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, dynamicTurnSpeed * dt));
            }
            else if (runtimeState.hasBall && !isGK)
            {
                // When receiving or controlling the ball without active manual movement input:
                // Instantly orient body towards the opponent's attacking goal!
                Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
                Vector3 faceGoalDir = targetGoal - transform.position;
                faceGoalDir.y = 0f;
                if (faceGoalDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(faceGoalDir.normalized, Vector3.up);
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, dynamicTurnSpeed * 1.8f * dt));
                }
            }

            // 4. Physical Shoulder-to-Shoulder Jostling when running alongside opponent
            HandleShoulderJostling(strengthAttr, dt);

            // 5. Stamina update & movement state
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

            // Clear ball possession only during goals or halftime/fulltime breaks
            if (GameEvents.CurrentMatchState == MatchState.GoalScored || 
                GameEvents.CurrentMatchState == MatchState.HalfTime || 
                GameEvents.CurrentMatchState == MatchState.FullTime)
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
                if (!runtimeState.hasBall && !isGK)
                {
                    Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
                    Vector3 faceGoalDir = targetGoal - transform.position;
                    faceGoalDir.y = 0f;
                    if (faceGoalDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(faceGoalDir.normalized, Vector3.up);
                        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, dynamicTurnSpeed * 3.5f * dt));
                    }
                }

                runtimeState.hasBall = true;
                runtimeState.currentState = MovementState.Dribbling;

                // Close-control touch: natural stride distance depending on sprint vs jog
                float dynamicPushOffset = isSprinting ? 1.15f : dribblePushOffset;
                Vector3 desiredBallPos = transform.position + transform.forward * dynamicPushOffset;
                desiredBallPos.y = ball.ballRadius;

                Vector3 ballCorrection = desiredBallPos - ball.transform.position;
                ballCorrection.y = 0;

                if (targetMoveDirection.sqrMagnitude > 0.05f)
                {
                    // Touch Dribble Physics: Guide ball smoothly ahead with athletic touches
                    float touchSpeed = Mathf.Max(currentVelocity.magnitude, 2.5f);
                    Vector3 touchVel = (transform.forward * touchSpeed + ballCorrection * 5.0f);
                    ball.BallRigidbody.linearVelocity = Vector3.Lerp(ball.BallRigidbody.linearVelocity, touchVel, (isSprinting ? 7.5f : 12.0f) * dt);
                }
                else
                {
                    // Stopped / standing still: snug ball right at boots, zero roll-away!
                    ball.BallRigidbody.linearVelocity = Vector3.Lerp(ball.BallRigidbody.linearVelocity, ballCorrection * 2.5f, 18.0f * dt);
                    ball.BallRigidbody.angularVelocity = Vector3.Lerp(ball.BallRigidbody.angularVelocity, Vector3.zero, 18.0f * dt);
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

        private void HandleShoulderJostling(float strengthAttr, float dt)
        {
            if (rb == null || currentVelocity.sqrMagnitude < 4.0f) return;

            // Detect opposing players running closely alongside
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                var other = allPlayers[i];
                if (other == null || other == runtimeState || other.teamId == runtimeState.teamId || other.isSentOff) continue;

                Vector3 toOther = other.transform.position - transform.position;
                toOther.y = 0f;
                float d = toOther.magnitude;

                if (d < 1.1f && d > 0.05f)
                {
                    // Players are shoulder-to-shoulder!
                    float otherStrength = other.attributes != null ? other.attributes.strength : 70f;
                    float strengthDiff = (strengthAttr - otherStrength) / 100f;

                    // Apply slight physical displacement impulse based on relative strength
                    Vector3 pushDir = -toOther.normalized;
                    if (strengthDiff > 0.1f)
                    {
                        // Stronger player nudges weaker player slightly off-balance
                        var otherRb = other.GetComponent<Rigidbody>();
                        if (otherRb != null)
                        {
                            otherRb.AddForce(-pushDir * (3.5f + strengthDiff * 6f), ForceMode.Impulse);
                        }
                    }
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
            target.y = 0f;
            transform.position = target;
            if (rb != null)
            {
                rb.position = target;
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
