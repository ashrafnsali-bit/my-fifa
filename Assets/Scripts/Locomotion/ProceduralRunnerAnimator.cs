using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;

namespace Football.Locomotion
{
    /// <summary>
    /// Professional Athletic Procedural Runner Animator (FIFA / EA Sports FC style).
    /// Implements authentic biokinetic running gait with dynamic heel recovery, high knee drive,
    /// ankle/foot plant & push-off articulation, instep dribble touches, full slide tackle kinematics,
    /// and instep striking follow-through.
    /// </summary>
    public class ProceduralRunnerAnimator : MonoBehaviour
    {
        [Header("Limb Transforms")]
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform leftKnee;
        public Transform rightKnee;
        public Transform leftAnkle;
        public Transform rightAnkle;
        public Transform leftArm;
        public Transform rightArm;
        public Transform torso;
        public Transform head;

        [Header("Professional Biokinetics")]
        public float jogFrequency = 9.5f;
        public float sprintFrequency = 14.5f;
        public float maxLegSwingAngle = 44.0f;
        public float maxKneeBendAngle = 82.0f; // High knee flexion on heel recovery
        public float maxAnkleFlexAngle = 36.0f; // Ankle push-off & dorsiflexion
        public float maxArmSwingAngle = 38.0f;
        public float torsoBounceHeight = 0.055f;
        public float maxBankAngle = 16.0f;
        public float maxSprintLeanAngle = 14.0f;

        private FootballPlayerLocomotion locomotion;
        private FootballPlayerActions actions;
        private PlayerRuntimeState runtimeState;

        private float stridePhase;
        private Vector3 initialTorsoLocalPos;
        private Quaternion initialTorsoLocalRot;
        private Quaternion initialLeftLegRot;
        private Quaternion initialRightLegRot;
        private Quaternion initialLeftKneeRot;
        private Quaternion initialRightKneeRot;
        private Quaternion initialLeftAnkleRot;
        private Quaternion initialRightAnkleRot;
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
            actions = GetComponent<FootballPlayerActions>();
            runtimeState = GetComponent<PlayerRuntimeState>();

            // Auto-locate ankle joints if not explicitly bound
            if (leftAnkle == null && leftKnee != null)
            {
                var ankleT = leftKnee.Find("AnkleJoint");
                if (ankleT != null) leftAnkle = ankleT;
            }
            if (rightAnkle == null && rightKnee != null)
            {
                var ankleT = rightKnee.Find("AnkleJoint");
                if (ankleT != null) rightAnkle = ankleT;
            }

            if (torso != null)
            {
                initialTorsoLocalPos = torso.localPosition;
                initialTorsoLocalRot = torso.localRotation;
            }
            if (leftLeg != null) initialLeftLegRot = leftLeg.localRotation;
            if (rightLeg != null) initialRightLegRot = rightLeg.localRotation;
            if (leftKnee != null) initialLeftKneeRot = leftKnee.localRotation;
            if (rightKnee != null) initialRightKneeRot = rightKnee.localRotation;
            if (leftAnkle != null) initialLeftAnkleRot = leftAnkle.localRotation;
            if (rightAnkle != null) initialRightAnkleRot = rightAnkle.localRotation;
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
                    if (leftAnkle != null) leftAnkle.localRotation = initialLeftAnkleRot * Quaternion.Euler(20f, 0f, 0f);
                    if (rightAnkle != null) rightAnkle.localRotation = initialRightAnkleRot * Quaternion.Euler(20f, 0f, 0f);

                    return;
                }
            }

            // 2. Slide Tackle Kinematics: low grass slide with lead leg outstretched to sweep ball
            bool isSliding = actions != null && actions.IsSlideTackling;
            if (isSliding)
            {
                // Lead leg extends straight along the turf
                Quaternion slideLeadLeg = initialRightLegRot * Quaternion.Euler(-74f, 8f, 15f);
                Quaternion slideLeadKnee = initialRightKneeRot * Quaternion.Euler(4f, 0f, 0f);
                Quaternion slideLeadAnkle = initialRightAnkleRot * Quaternion.Euler(30f, -12f, 0f); // Point toes & hook with instep

                // Trailing leg folds under hip
                Quaternion slideTrailLeg = initialLeftLegRot * Quaternion.Euler(30f, -14f, -20f);
                Quaternion slideTrailKnee = initialLeftKneeRot * Quaternion.Euler(92f, 0f, 0f);
                Quaternion slideTrailAnkle = initialLeftAnkleRot * Quaternion.Euler(20f, 0f, 0f);

                if (rightLeg != null) rightLeg.localRotation = slideLeadLeg;
                if (rightKnee != null) rightKnee.localRotation = slideLeadKnee;
                if (rightAnkle != null) rightAnkle.localRotation = slideLeadAnkle;

                if (leftLeg != null) leftLeg.localRotation = slideTrailLeg;
                if (leftKnee != null) leftKnee.localRotation = slideTrailKnee;
                if (leftAnkle != null) leftAnkle.localRotation = slideTrailAnkle;

                // Arms brace along turf for athletic balance
                if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(32f, 0f, -48f);
                if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-25f, 0f, 48f);

                // Torso drops low and banks sideways into slide
                if (torso != null)
                {
                    torso.localPosition = initialTorsoLocalPos + new Vector3(0f, -0.34f, 0f);
                    torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(-18f, 15f, -32f);
                }
                return;
            }

            // 3. Kick Animation: explosive strike through the ball with full foot instep angle
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
                    if (leftAnkle != null) leftAnkle.localRotation = initialLeftAnkleRot;
                    if (rightAnkle != null) rightAnkle.localRotation = initialRightAnkleRot;
                }
                else
                {
                    float forwardPitch = 0f;
                    float kickKneeBend = 0f;
                    float kickAnkleFlex = 0f;

                    if (progress < 0.22f)
                    {
                        // Windup: leg pulls back -44°, knee flexes 58°, ankle points down
                        float t = progress / 0.22f;
                        forwardPitch = Mathf.Lerp(0f, -44f, t);
                        kickKneeBend = Mathf.Lerp(0f, 58f, t);
                        kickAnkleFlex = Mathf.Lerp(0f, 32f, t);
                    }
                    else if (progress < 0.54f)
                    {
                        // Explosive strike: leg drives forward +70°, knee straightens, ankle locks into instep laces strike (+42°)
                        float t = (progress - 0.22f) / 0.32f;
                        forwardPitch = Mathf.Lerp(-44f, 70f, Mathf.SmoothStep(0f, 1f, t));
                        kickKneeBend = Mathf.Lerp(58f, 0f, t);
                        kickAnkleFlex = 40f;
                    }
                    else
                    {
                        // Follow-through recovery
                        float t = (progress - 0.54f) / 0.46f;
                        forwardPitch = Mathf.Lerp(70f, 0f, t);
                        kickKneeBend = 0f;
                        kickAnkleFlex = Mathf.Lerp(40f, 0f, t);
                    }

                    Transform kickLimb = isRightLegKicking ? rightLeg : leftLeg;
                    Transform plantLimb = isRightLegKicking ? leftLeg : rightLeg;
                    Transform kickKnee = isRightLegKicking ? rightKnee : leftKnee;
                    Transform plantKnee = isRightLegKicking ? leftKnee : rightKnee;
                    Transform kickAnkle = isRightLegKicking ? rightAnkle : leftAnkle;
                    Transform plantAnkle = isRightLegKicking ? leftAnkle : rightAnkle;

                    Quaternion kickInit = isRightLegKicking ? initialRightLegRot : initialLeftLegRot;
                    Quaternion plantInit = isRightLegKicking ? initialLeftLegRot : initialRightLegRot;
                    Quaternion kickKneeInit = isRightLegKicking ? initialRightKneeRot : initialLeftKneeRot;
                    Quaternion plantKneeInit = isRightLegKicking ? initialLeftKneeRot : initialRightKneeRot;
                    Quaternion kickAnkleInit = isRightLegKicking ? initialRightAnkleRot : initialLeftAnkleRot;
                    Quaternion plantAnkleInit = isRightLegKicking ? initialLeftAnkleRot : initialRightAnkleRot;

                    if (kickLimb != null) kickLimb.localRotation = kickInit * Quaternion.Euler(-forwardPitch, 0f, 0f);
                    if (plantLimb != null) plantLimb.localRotation = plantInit * Quaternion.Euler(8f, 0f, 0f);
                    if (kickKnee != null) kickKnee.localRotation = kickKneeInit * Quaternion.Euler(kickKneeBend, 0f, 0f);
                    if (plantKnee != null) plantKnee.localRotation = plantKneeInit * Quaternion.Euler(15f, 0f, 0f); // Plant knee absorbs weight
                    if (kickAnkle != null) kickAnkle.localRotation = kickAnkleInit * Quaternion.Euler(kickAnkleFlex, isRightLegKicking ? -10f : 10f, 0f);
                    if (plantAnkle != null) plantAnkle.localRotation = plantAnkleInit * Quaternion.Euler(-4f, 0f, 0f);

                    // Counter-balance arm swing
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(isRightLegKicking ? -38f : 25f, 0f, 14f);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(isRightLegKicking ? 25f : -38f, 0f, -14f);

                    // Torso athletic lean into strike
                    if (torso != null)
                    {
                        float torsoLean = Mathf.Clamp(forwardPitch * 0.18f, -4f, 14f);
                        torso.localRotation = initialTorsoLocalRot * Quaternion.Euler(torsoLean, isRightLegKicking ? -7f : 7f, 0f);
                    }

                    return;
                }
            }

            // 4. Goalkeeper holding ball in hands posture
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
                if (leftAnkle != null) leftAnkle.localRotation = Quaternion.Slerp(leftAnkle.localRotation, initialLeftAnkleRot, dt * 10f);
                if (rightAnkle != null) rightAnkle.localRotation = Quaternion.Slerp(rightAnkle.localRotation, initialRightAnkleRot, dt * 10f);
                return;
            }

            Vector3 vel = locomotion != null ? locomotion.Velocity : Vector3.zero;
            vel.y = 0;
            float horizontalSpeed = vel.magnitude;

            bool isMoving = horizontalSpeed > 0.25f;
            bool isSprinting = locomotion != null && locomotion.IsSprinting;
            bool isGK = runtimeState != null && runtimeState.attributes != null && runtimeState.attributes.position == PlayerPosition.GK;
            bool hasBall = runtimeState != null && runtimeState.hasBall;

            // 5. Dynamic Turn Banking into Curves
            Vector3 currentFwd = transform.forward;
            if (lastForward != Vector3.zero && dt > 0.0001f)
            {
                float turnAngle = Vector3.SignedAngle(lastForward, currentFwd, Vector3.up);
                float angularSpeed = turnAngle / dt;
                float targetBank = Mathf.Clamp(-angularSpeed * 0.055f * Mathf.Clamp01(horizontalSpeed / 4.5f), -maxBankAngle, maxBankAngle);
                currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, dt * 8f);
            }
            lastForward = currentFwd;

            // 6. Sprint Forward Pitch Lean
            float speedRatio = Mathf.Clamp01(horizontalSpeed / 8.5f);
            float targetPitch = speedRatio * (isSprinting ? maxSprintLeanAngle : maxSprintLeanAngle * 0.55f);
            if (isGK && !isSprinting) targetPitch = Mathf.Max(targetPitch, 13.0f); // Goalkeeper eager forward tilt
            currentPitchAngle = Mathf.Lerp(currentPitchAngle, targetPitch, dt * 7f);

            // 7. Dynamic Head Tracking towards Football
            UpdateHeadTracking(dt);

            if (isMoving)
            {
                float freq = isSprinting ? sprintFrequency : jogFrequency;
                if (hasBall) freq *= 1.15f; // Rapid-touch cadence while dribbling

                stridePhase += dt * freq;

                // Non-linear professional gait cycle
                float phaseL = stridePhase;
                float phaseR = stridePhase + Mathf.PI;

                float sinL = Mathf.Sin(phaseL);
                float sinR = Mathf.Sin(phaseR);

                // 1. Hip Swing (Thigh Pitch & Pelvic Roll)
                float swingAmp = isSprinting ? maxLegSwingAngle : maxLegSwingAngle * 0.78f;
                if (hasBall) swingAmp *= 0.85f; // Compact, agile strides while dribbling

                float hipPitchL = sinL * swingAmp;
                float hipPitchR = sinR * swingAmp;

                float pelvicRollL = -sinL * 3.5f;
                float pelvicRollR = -sinR * 3.5f;

                // 2. High Knee Drive & Dynamic Heel Recovery:
                // When swinging forward (sin < 0): knee flexes sharply (up to 82° in sprint!) for rapid recovery
                // When planting / driving (sin > 0): knee straightens with 8-12° cushion
                float kneeL = (sinL < 0f) 
                    ? Mathf.Lerp(8f, maxKneeBendAngle * (isSprinting ? 1.0f : 0.74f), Mathf.Pow(-sinL, 1.2f))
                    : Mathf.Lerp(8f, 2f, sinL);

                float kneeR = (sinR < 0f) 
                    ? Mathf.Lerp(8f, maxKneeBendAngle * (isSprinting ? 1.0f : 0.74f), Mathf.Pow(-sinR, 1.2f))
                    : Mathf.Lerp(8f, 2f, sinR);

                if (isGK)
                {
                    kneeL = Mathf.Max(kneeL, 18.0f);
                    kneeR = Mathf.Max(kneeR, 18.0f);
                }

                // 3. Ankle & Cleat Articulation:
                // Push-off (sin > 0): Ankle extends downwards (plantarflexion: +32° pointing toes into turf)
                // Forward swing (sin < 0): Ankle flexes upwards (dorsiflexion: -22° lifting cleat clean off grass)
                float ankleL = (sinL > 0f)
                    ? Mathf.Lerp(0f, maxAnkleFlexAngle * (isSprinting ? 1.0f : 0.65f), sinL)
                    : Mathf.Lerp(0f, -maxAnkleFlexAngle * 0.62f, -sinL);

                float ankleR = (sinR > 0f)
                    ? Mathf.Lerp(0f, maxAnkleFlexAngle * (isSprinting ? 1.0f : 0.65f), sinR)
                    : Mathf.Lerp(0f, -maxAnkleFlexAngle * 0.62f, -sinR);

                // Dribble micro-touches: inside-foot instep angles when guiding ball
                float dribbleYawL = 0f;
                float dribbleYawR = 0f;
                if (hasBall)
                {
                    if (sinL < -0.3f) { ankleL += 12f; dribbleYawL = 10f; }
                    if (sinR < -0.3f) { ankleR += 12f; dribbleYawR = -10f; }
                }

                // Apply Rotations to Legs
                if (leftLeg != null) leftLeg.localRotation = initialLeftLegRot * Quaternion.Euler(hipPitchL, 0f, pelvicRollL);
                if (rightLeg != null) rightLeg.localRotation = initialRightLegRot * Quaternion.Euler(hipPitchR, 0f, pelvicRollR);

                if (leftKnee != null) leftKnee.localRotation = initialLeftKneeRot * Quaternion.Euler(kneeL, 0f, 0f);
                if (rightKnee != null) rightKnee.localRotation = initialRightKneeRot * Quaternion.Euler(kneeR, 0f, 0f);

                if (leftAnkle != null) leftAnkle.localRotation = initialLeftAnkleRot * Quaternion.Euler(ankleL, dribbleYawL, 0f);
                if (rightAnkle != null) rightAnkle.localRotation = initialRightAnkleRot * Quaternion.Euler(ankleR, dribbleYawR, 0f);

                // 4. Arm swing in opposition to legs with slight outward flare
                float armAngle = -sinL * (isSprinting ? maxArmSwingAngle : maxArmSwingAngle * 0.65f);
                float armFlare = hasBall ? 16.0f : 8.0f; // Wider arm shielding when dribbling

                if (isGK && !isSprinting)
                {
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(-38f, 18f, 12f);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-38f, -18f, -12f);
                }
                else
                {
                    if (leftArm != null) leftArm.localRotation = initialLeftArmRot * Quaternion.Euler(armAngle, 0f, armFlare);
                    if (rightArm != null) rightArm.localRotation = initialRightArmRot * Quaternion.Euler(-armAngle, 0f, -armFlare);
                }

                // 5. Torso: vertical bounce, sprint forward pitch, centrifugal banking, spinal counter-twist, and lateral weight sway
                float shoulderTwist = -sinL * (isSprinting ? 7.5f : 4.0f);
                float lateralSway = -sinL * (isSprinting ? 0.038f : 0.024f); // Authentic human physical weight transfer
                if (torso != null)
                {
                    float bounce = Mathf.Abs(sinL) * torsoBounceHeight;
                    float heightDrop = (hasBall ? -0.032f : 0f) + (isGK ? -0.055f : 0f);
                    torso.localPosition = initialTorsoLocalPos + new Vector3(lateralSway, bounce + heightDrop, 0f);
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
                    if (leftAnkle != null) leftAnkle.localRotation = Quaternion.Slerp(leftAnkle.localRotation, initialLeftAnkleRot * Quaternion.Euler(12f, 0f, 0f), dt * 8f);
                    if (rightAnkle != null) rightAnkle.localRotation = Quaternion.Slerp(rightAnkle.localRotation, initialRightAnkleRot * Quaternion.Euler(12f, 0f, 0f), dt * 8f);
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
                    Quaternion ballControlAnkle = initialRightAnkleRot * Quaternion.Euler(16f, -10f, 0f); // Foot resting over ball

                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, initialLeftLegRot, dt * 8f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, ballControlLeg, dt * 8f);
                    if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot, dt * 8f);
                    if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, ballControlKnee, dt * 8f);
                    if (leftAnkle != null) leftAnkle.localRotation = Quaternion.Slerp(leftAnkle.localRotation, initialLeftAnkleRot, dt * 8f);
                    if (rightAnkle != null) rightAnkle.localRotation = Quaternion.Slerp(rightAnkle.localRotation, ballControlAnkle, dt * 8f);
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
                    // Regular idle breathing, natural weight shifting between legs
                    float breath = Mathf.Sin(Time.time * 2.2f) * 0.014f;
                    float weightShift = Mathf.Sin(Time.time * 0.8f); // slow 4-second shift
                    float legRelaxL = weightShift > 0.3f ? 4f : 0f;
                    float legRelaxR = weightShift < -0.3f ? 4f : 0f;

                    Quaternion idleLegL = initialLeftLegRot * Quaternion.Euler(legRelaxL, 0f, -legRelaxL * 0.5f);
                    Quaternion idleLegR = initialRightLegRot * Quaternion.Euler(legRelaxR, 0f, legRelaxR * 0.5f);

                    if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, idleLegL, dt * 6f);
                    if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, idleLegR, dt * 6f);
                    if (leftKnee != null) leftKnee.localRotation = Quaternion.Slerp(leftKnee.localRotation, initialLeftKneeRot * Quaternion.Euler(legRelaxL * 1.5f, 0f, 0f), dt * 6f);
                    if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(rightKnee.localRotation, initialRightKneeRot * Quaternion.Euler(legRelaxR * 1.5f, 0f, 0f), dt * 6f);
                    if (leftAnkle != null) leftAnkle.localRotation = Quaternion.Slerp(leftAnkle.localRotation, initialLeftAnkleRot, dt * 8f);
                    if (rightAnkle != null) rightAnkle.localRotation = Quaternion.Slerp(rightAnkle.localRotation, initialRightAnkleRot, dt * 8f);
                    if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, initialLeftArmRot * Quaternion.Euler(0f, 0f, 3f + breath * 35f), dt * 8f);
                    if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, initialRightArmRot * Quaternion.Euler(0f, 0f, -3f - breath * 35f), dt * 8f);

                    if (torso != null)
                    {
                        float idleSway = weightShift * 0.012f;
                        torso.localPosition = Vector3.Lerp(torso.localPosition, initialTorsoLocalPos + new Vector3(idleSway, breath, 0f), dt * 8f);
                        torso.localRotation = Quaternion.Slerp(torso.localRotation, initialTorsoLocalRot * Quaternion.Euler(0f, weightShift * 2.0f, weightShift * 1.5f), dt * 6f);
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
