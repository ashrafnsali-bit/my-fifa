using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;

namespace Football.Locomotion
{
    /// <summary>
    /// Lightweight procedural runner animator that breathes life into player limbs.
    /// Drives synchronized hip swings, opposing arm swings, and vertical torso bobbing
    /// dynamically matching player velocity and state without requiring heavy skinned meshes.
    /// </summary>
    public class ProceduralRunnerAnimator : MonoBehaviour
    {
        [Header("Limb Transforms")]
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform leftKnee;
        public Transform rightKnee;
        public Transform leftArm;
        public Transform rightArm;
        public Transform torso;
        public Transform head;

        [Header("Tuning Parameters")]
        public float jogFrequency = 9.0f;
        public float sprintFrequency = 14.0f;
        public float maxLegSwingAngle = 38.0f;
        public float maxKneeBendAngle = 68.0f;
        public float maxArmSwingAngle = 34.0f;
        public float torsoBounceHeight = 0.045f;
        public float maxBankAngle = 14.0f;
        public float maxSprintLeanAngle = 12.0f;

        private FootballPlayerLocomotion locomotion;
        private PlayerRuntimeState runtimeState;

        private float stridePhase;
        private Vector3 initialTorsoLocalPos;
        private Quaternion initialTorsoLocalRot;
        private Quaternion initialLeftLegRot;
        private Quaternion initialRightLegRot;
        private Quaternion initialLeftKneeRot;
        private Quaternion initialRightKneeRot;
        private Quaternion initialLeftArmRot;
        private Quaternion initialRightArmRot;
        private Quaternion initialHeadRot;

        private float currentBankAngle;
        private float currentPitchAngle;
        private Vector3 lastForward;

        private bool isKicking;
        private float kickTimer;
        private const float KickDuration = 0.28f;
        private bool isRightLegKicking = true;

        private bool isDiving;
        private float diveTimer;
        private const float DiveDuration = 0.60f;
        private bool isDivingRight = true;
        private bool isHighSave = false;

        public void TriggerKickAnimation(bool rightFoot = true)
        {
            isKicking = true;
            kickTimer = 0f;
            isRightLegKicking = rightFoot;
        }

        public void TriggerGoalkeeperDive(bool diveRight, bool highSave = false)
        {
            isDiving = true;
            diveTimer = 0f;
            isDivingRight = diveRight;
            isHighSave = highSave;
        }

        private void Awake()
        {
            locomotion = GetComponent<FootballPlayerLocomotion>();
            runtimeState = GetComponent<PlayerRuntimeState>();

            if (torso != null)
            {
                initialTorsoLocalPos = torso.localPosition;
                initialTorsoLocalRot = torso.localRotation;
            }
            if (leftLeg != null) initialLeftLegRot = leftLeg.localRotation;
            if (rightLeg != null) initialRightLegRot = rightLeg.localRotation;
            if (leftKnee != null) initialLeftKneeRot = leftKnee.localRotation;
            if (rightKnee != null) initialRightKneeRot = rightKnee.localRotation;
            if (leftArm != null) initialLeftArmRot = leftArm.localRotation;
            if (rightArm != null) initialRightArmRot = rightArm.localRotation;
            if (head != null) initialHeadRot = head.localRotation;

            lastForward = transform.forward;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. Goalkeeper Dive Save Override: athletic leap & glove extension
            if (isDiving)
            {
                diveTimer += dt;
                float progress = diveTimer / DiveDuration;

                if (progress >= 1.0f)
                {
                    isDiving = false;
                    if (torso != null)
                    {
                        torso.localRotation = initialTorsoLocalRot;
                        torso.localPosition = initialTorsoLocalPos;
                    }
                }
                else
                {
                    float rollAngle = isDivingRight ? -65f : 65f;
                    float pitchAngle = isHighSave ? -20f : 15f;

                    float stretchT;
                    if (progress < 0.35f)
                    {
                        stretchT = Mathf.SmoothStep(0f, 1f, progress / 0.35f);
                    }
                    else if (progress < 0.70f)
                    {
                        stretchT = 1f;
                    }
                    else
                    {
                        stretchT = Mathf.SmoothStep(1f, 0f, (progress - 0.70f) / 0.30f);
                    }

                    if (torso != null)
                    {
                        torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(pitchAngle * stretchT, 0f, rollAngle * stretchT);
                        float heightOffset = isHighSave ? 0.35f * stretchT : -0.22f * stretchT;
                        torso.localPosition = initialTorsoLocalPos + new Vector3(0f, heightOffset, 0f);
                    }

                    // Arms extend fully with gloves reaching to parry / block
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(isDivingRight ? -80f : -30f, 20f, isDivingRight ? -50f : 30f);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(isDivingRight ? -30f : -80f, -20f, isDivingRight ? -30f : 50f);

                    // Dynamic diving leg & knee extension
                    if (leftLeg != null) leftLeg.localRotation = initialLeftLegRot * Quaternion.Euler(isDivingRight ? 20f : -45f, 0f, isDivingRight ? 25f : -45f);
                    if (rightLeg != null) rightLeg.localRotation = initialRightLegRot * Quaternion.Euler(isDivingRight ? -45f : 20f, 0f, isDivingRight ? 45f : -25f);
                    if (leftKnee != null) leftKnee.localRotation = initialLeftKneeRot * Quaternion.Euler(isDivingRight ? 35f : 15f, 0f, 0f);
                    if (rightKnee != null) rightKnee.localRotation = initialRightKneeRot * Quaternion.Euler(isDivingRight ? 15f : 35f, 0f, 0f);

                    return;
                }
            }

            // 2. Kick Animation Override: athletic foot strike through the ball
            if (isKicking)
            {
                kickTimer += dt;
                float progress = kickTimer / KickDuration;

                if (progress >= 1.0f)
                {
                    isKicking = false;
                    if (torso != null) torso.localRotation = initialTorsoLocalRot;
                    if (leftKnee != null) leftKnee.localRotation = initialLeftKneeRot;
                    if (rightKnee != null) rightKnee.localRotation = initialRightKneeRot;
                }
                else
                {
                    float forwardPitch = 0f;
                    float kickKneeBend = 0f;

                    if (progress < 0.22f)
                    {
                        // Windup: leg pulls back -42°, knee flexes 50°
                        float t = progress / 0.22f;
                        forwardPitch = Mathf.Lerp(0f, -42f, t);
                        kickKneeBend = Mathf.Lerp(0f, 50f, t);
                    }
                    else if (progress < 0.54f)
                    {
                        // Explosive forward strike: leg kicks forward +68°, knee extends straight to 0°
                        float t = (progress - 0.22f) / 0.32f;
                        forwardPitch = Mathf.Lerp(-42f, 68f, Mathf.SmoothStep(0f, 1f, t));
                        kickKneeBend = Mathf.Lerp(50f, 0f, t);
                    }
                    else
                    {
                        // Recovery
                        float t = (progress - 0.54f) / 0.46f;
                        forwardPitch = Mathf.Lerp(68f, 0f, t);
                        kickKneeBend = 0f;
                    }

                    Transform kickLimb = isRightLegKicking ? rightLeg : leftLeg;
                    Transform plantLimb = isRightLegKicking ? leftLeg : rightLeg;
                    Transform kickKnee = isRightLegKicking ? rightKnee : leftKnee;
                    Transform plantKnee = isRightLegKicking ? leftKnee : rightKnee;

                    Quaternion kickInit = isRightLegKicking ? initialRightLegRot : initialLeftLegRot;
                    Quaternion plantInit = isRightLegKicking ? initialLeftLegRot : initialRightLegRot;
                    Quaternion kickKneeInit = isRightLegKicking ? initialRightKneeRot : initialLeftKneeRot;
                    Quaternion plantKneeInit = isRightLegKicking ? initialLeftKneeRot : initialRightKneeRot;

                    if (kickLimb != null) kickLimb.localRotation = kickInit * Quaternion.Euler(-forwardPitch, 0f, 0f);
                    if (plantLimb != null) plantLimb.localRotation = plantInit * Quaternion.Euler(8f, 0f, 0f);
                    if (kickKnee != null) kickKnee.localRotation = kickKneeInit * Quaternion.Euler(kickKneeBend, 0f, 0f);
                    if (plantKnee != null) plantKnee.localRotation = plantKneeInit * Quaternion.Euler(14f, 0f, 0f); // Supporting plant knee absorbs shock

                    // Counter-balance athletic arm swing
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(isRightLegKicking ? -38f : 25f, 0f, 12f);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(isRightLegKicking ? 25f : -38f, 0f, -12f);

                    // Torso athletic lean
                    if (torso != null)
                    {
                        float torsoLean = Mathf.Clamp(forwardPitch * 0.18f, -4f, 12f);
                        torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(torsoLean, isRightLegKicking ? -6f : 6f, 0f);
                    }

                    return;
                }
            }

            // 3. Goalkeeper holding ball in hands posture
            if (runtimeState != null && runtimeState.isHoldingBallInHands)
            {
                if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(-62f, 22f, 15f);
                if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-62f, -22f, -15f);
                if (torso != null) torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(4f, 0f, 0f);

                stridePhase = 0f;
                if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, initialLeftLegRot, dt * 10f);
                if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, initialRightLegRot, dt * 10f);
                if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot, dt * 10f);
                if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, initialRightKneeRot, dt * 10f);
                return;
            }

            Vector3 vel = locomotion != null ? locomotion.Velocity : Vector3.zero;
            vel.y = 0;
            float horizontalSpeed = vel.magnitude;

            bool isMoving = horizontalSpeed > 0.25f;
            bool isSprinting = locomotion != null && locomotion.IsSprinting;
            bool isGK = locomotion != null && locomotion.Position == PlayerPosition.GK;
            bool hasBall = runtimeState != null && runtimeState.hasBall;

            // 4. Dynamic Turn Banking into Curves
            Vector3 currentFwd = transform.forward;
            if (lastForward != Vector3.zero && dt > 0.0001f)
            {
                float turnAngle = Vector3.SignedAngle(lastForward, currentFwd, Vector3.up);
                float angularSpeed = turnAngle / dt;
                float targetBank = Mathf.Clamp(-angularSpeed * 0.045f * Mathf.Clamp01(horizontalSpeed / 4.5f), -maxBankAngle, maxBankAngle);
                currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, dt * 8f);
            }
            lastForward = currentFwd;

            // 5. Sprint & Acceleration Forward Pitch Lean
            float speedRatio = Mathf.Clamp01(horizontalSpeed / 8.5f);
            float targetPitch = speedRatio * (isSprinting ? maxSprintLeanAngle : maxSprintLeanAngle * 0.55f);
            if (isGK && !isSprinting) targetPitch = Mathf.Max(targetPitch, 13.0f); // Goalkeeper eager forward tilt
            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitch, dt * 7f);

            // 6. Dynamic Head Tracking towards Football
            UpdateHeadTracking(dt);

            if (isMoving)
            {
                float freq = isSprinting ? sprintFrequency : jogFrequency;
                if (hasBall) freq *= 1.16f; // Quick-touch agile dribbling cadence

                stridePhase += dt * freq;
                float sin = Mathf.Sin(stridePhase);

                // Hip Swings (X-axis pitch)
                float legAngle = sin * (isSprinting ? maxLegSwingAngle : maxLegSwingAngle * 0.75f);
                if (hasBall) legAngle *= 0.82f; // Tighter, controlled strides while dribbling

                // Ball-touch micro-nudge on the forward swing foot
                float leftDribbleNudge = (hasBall && sin < -0.35f) ? -9.0f : 0f;
                float rightDribbleNudge = (hasBall && sin > 0.35f) ? -9.0f : 0f;

                if (leftLeg != null) leftLeg.localRotation = initialLeftLegRot * Quaternion.Euler(legAngle + leftDribbleNudge, 0f, 0f);
                if (rightLeg != null) rightLeg.localRotation = initialRightLegRot * Quaternion.Euler(-legAngle + rightDribbleNudge, 0f, 0f);

                // Knee Joint Flexion (Bends backwards when swinging back, straightens on forward stride)
                float leftKneeBend = Mathf.Clamp(-sin * (isSprinting ? maxKneeBendAngle : maxKneeBendAngle * 0.70f), 0f, maxKneeBendAngle);
                float rightKneeBend = Mathf.Clamp(sin * (isSprinting ? maxKneeBendAngle : maxKneeBendAngle * 0.70f), 0f, maxKneeBendAngle);
                if (isGK)
                {
                    leftKneeBend = Mathf.Max(leftKneeBend, 18.0f);
                    rightKneeBend = Mathf.Max(rightKneeBend, 18.0f);
                }

                if (leftKnee != null) leftKnee.localRotation = initialLeftKneeRot * Quaternion.Euler(leftKneeBend, 0f, 0f);
                if (rightKnee != null) rightKnee.localRotation = initialRightKneeRot * Quaternion.Euler(rightKneeBend, 0f, 0f);

                // Arm swing in opposition to legs with slight outward flare
                float armAngle = -sin * (isSprinting ? maxArmSwingAngle : maxArmSwingAngle * 0.65f);
                float armFlare = hasBall ? 16.0f : 8.0f; // Wider arm shielding when dribbling

                if (isGK && !isSprinting)
                {
                    // Goalkeeper keeps parry-ready gloves raised even while shuffling
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(-38f, 18f, 12f);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-38f, -18f, -12f);
                }
                else
                {
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(armAngle, 0f, armFlare);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-armAngle, 0f, -armFlare);
                }

                // Torso: vertical bounce, sprint forward pitch, centrifugal banking, and spinal counter-twist
                float shoulderTwist = -sin * (isSprinting ? 6.5f : 3.5f);
                if (torso != null)
                {
                    float bounce = Mathf.Abs(sin) * torsoBounceHeight;
                    float heightDrop = (hasBall ? -0.032f : 0f) + (isGK ? -0.055f : 0f);
                    torso.localPosition = initialTorsoLocalPos + new Vector3(0f, bounce + heightDrop, 0f);
                    torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(currentPitchAngle, shoulderTwist, currentBankAngle);
                }
            }
            else
            {
                // Idle breathing, stance balance recovery, or goalkeeper athletic low crouch
                stridePhase = 0f;
                currentBankAngle = Mathf.Lerp(currentBankAngle, 0f, dt * 8f);
                currentPitchAngle = Mathf.Lerp(currentPitchAngle, isGK ? 14.0f : 0f, dt * 8f);

                if (isGK)
                {
                    // Goalkeeper low athletic crouch stance: bent knees, forward torso, parry-ready gloves, ready foot bounce
                    float gkBounce = Mathf.Sin(Time.time * 4.2f) * 0.015f;
                    Quaternion gkLeftArm = initialLeftArmRot * Quaternion.Euler(-42f, 20f, 14f);
                    Quaternion gkRightArm = initialRightArmRot * Quaternion.Euler(-42f, -20f, -14f);
                    Quaternion gkLeftLeg = initialLeftLegRot * Quaternion.Euler(14f, 0f, -4f);
                    Quaternion gkRightLeg = initialRightLegRot * Quaternion.Euler(14f, 0f, 4f);
                    Quaternion gkKnee = Quaternion.Euler(26f, 0f, 0f);

                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, gkLeftLeg, dt * 8f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, gkRightLeg, dt * 8f);
                    if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot * gkKnee, dt * 8f);
                    if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, initialRightKneeRot * gkKnee, dt * 8f);
                    if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, gkLeftArm, dt * 8f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, gkRightArm, dt * 8f);

                    if (torso != null)
                    {
                        torso.localPosition = Vector3.Lerp(torso.localPosition, initialTorsoLocalPos + new Vector3(0f, -0.065f + gkBounce, 0f), dt * 8f);
                        torso.localRotation = Quaternion.Slerp(torso.localRotation, initialTorsoLocalRot * Quaternion.Euler(14.0f, 0f, 0f), dt * 8f);
                    }
                }
                else if (hasBall)
                {
                    // Outfield player stationary over the ball: control stance
                    Quaternion ballControlLeg = initialRightLegRot * Quaternion.Euler(-10f, 0f, 0f);
                    Quaternion ballControlKnee = initialRightKneeRot * Quaternion.Euler(12f, 0f, 0f);

                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, initialLeftLegRot, dt * 8f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, ballControlLeg, dt * 8f);
                    if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot, dt * 8f);
                    if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, ballControlKnee, dt * 8f);
                    if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, initialLeftArmRot * Quaternion.Euler(5f, 0f, 12f), dt * 8f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, initialRightArmRot * Quaternion.Euler(-5f, 0f, -12f), dt * 8f);

                    if (torso != null)
                    {
                        torso.localPosition = Vector3.Lerp(torso.localPosition, initialTorsoLocalPos + new Vector3(0f, -0.025f, 0f), dt * 8f);
                        torso.localRotation = Quaternion.Slerp(torso.localRotation, initialTorsoLocalRot * Quaternion.Euler(6.0f, 0f, 0f), dt * 8f);
                    }
                }
                else
                {
                    // Regular idle breathing, stance balance recovery
                    float breath = Mathf.Sin(Time.time * 2.2f) * 0.012f;

                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, initialLeftLegRot, dt * 10f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, initialRightLegRot, dt * 10f);
                    if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot, dt * 10f);
                    if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, initialRightKneeRot, dt * 10f);
                    if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, initialLeftArmRot, dt * 10f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, initialRightArmRot, dt * 10f);

                    if (torso != null)
                    {
                        torso.localPosition = Vector3.Lerp(torso.localPosition, initialTorsoLocalPos + new Vector3(0f, breath, 0f), dt * 10f);
                        torso.localRotation = Quaternion.Slerp(torso.localRotation, initialTorsoLocalRot, dt * 8f);
                    }
                }
            }
        }

        private void UpdateHeadTracking(float dt)
        {
            if (head == null) return;

            var ball = Football.PhysicsEngine.FootballBall.Instance;
            if (ball != null)
            {
                Vector3 toBall = ball.transform.position - head.position;
                Vector3 localDir = transform.InverseTransformDirection(toBall);

                if (localDir.sqrMagnitude > 0.05f)
                {
                    float targetYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    float horizDist = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
                    float targetPitch = -Mathf.Atan2(localDir.y, horizDist) * Mathf.Rad2Deg;

                    // Clamp to natural human neck rotation limits
                    targetYaw = Mathf.Clamp(targetYaw, -52f, 52f);
                    targetPitch = Mathf.Clamp(targetPitch, -22f, 20f);

                    Quaternion targetHeadRot = initialHeadRot * Quaternion.Euler(targetPitch, targetYaw, 0f);
                    head.localRotation = Quaternion.Slerp(head.localRotation, targetHeadRot, dt * 6.5f);
                    return;
                }
            }

            head.localRotation = Quaternion.Slerp(head.localRotation, initialHeadRot, dt * 5f);
        }
    }
}
