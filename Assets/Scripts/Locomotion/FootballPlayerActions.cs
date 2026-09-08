using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;

namespace Football.Locomotion
{
    [RequireComponent(typeof(PlayerRuntimeState), typeof(FootballPlayerLocomotion))]
    public class FootballPlayerActions : MonoBehaviour
    {
        [Header("Tackle Parameters")]
        public float standingTackleRadius = 1.6f;
        public float standingTackleForce = 12.0f;
        public float slideTackleDistance = 4.5f;
        public float slideTackleDuration = 0.8f;

        [Header("Kick Power Range (m/s)")]
        public float minPassSpeed = 10f;
        public float maxPassSpeed = 24f;
        public float minShotSpeed = 16f;
        public float maxShotSpeed = 34f;

        private PlayerRuntimeState runtimeState;
        private FootballPlayerLocomotion locomotion;
        private Rigidbody rb;

        private float currentPowerCharge = 0f;
        private bool isChargingShot = false;
        private bool isChargingPass = false;
        private bool isSlideTackling = false;
        private float slideTackleTimer = 0f;
        private Vector3 slideDirection;

        public float CurrentPowerRatio => Mathf.Clamp01(currentPowerCharge / 1.0f);
        public bool IsSlideTackling => isSlideTackling;

        private void Awake()
        {
            runtimeState = GetComponent<PlayerRuntimeState>();
            locomotion = GetComponent<FootballPlayerLocomotion>();
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (runtimeState.isSentOff) return;

            // Handle power bar charging
            if (isChargingShot || isChargingPass)
            {
                currentPowerCharge = Mathf.Min(1.0f, currentPowerCharge + Time.deltaTime * 1.5f);
            }

            // Slide tackle state duration
            if (isSlideTackling)
            {
                slideTackleTimer -= Time.deltaTime;
                if (slideTackleTimer <= 0f)
                {
                    isSlideTackling = false;
                    runtimeState.currentState = MovementState.Idle;
                }
            }
        }

        private void FixedUpdate()
        {
            if (isSlideTackling)
            {
                // Slide physics forward with deceleration
                float progress = 1.0f - (slideTackleTimer / slideTackleDuration);
                float slideSpeed = Mathf.Lerp(12f, 0f, progress);
                rb.MovePosition(rb.position + slideDirection * slideSpeed * Time.fixedDeltaTime);
            }
        }

        #region Kicking & Shooting API

        public void StartShotCharge()
        {
            var ball = FootballBall.Instance;
            float dist = ball != null ? Vector3.Distance(transform.position, ball.transform.position) : 99f;
            if (!runtimeState.hasBall && dist > 3.0f) return;

            isChargingShot = true;
            currentPowerCharge = 0f;
        }

        public void ReleaseShot(ShotType type, Vector3 aimDirection)
        {
            if (!isChargingShot) return;
            isChargingShot = false;

            float power = currentPowerCharge;
            currentPowerCharge = 0f;

            ExecuteShot(type, aimDirection, power);
        }

        public void ExecuteShot(ShotType type, Vector3 aimDirection, float power01)
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // Goalkeeper punts with foot if holding ball in hands
            if (runtimeState.isHoldingBallInHands)
            {
                ExecuteGoalkeeperPunt();
                return;
            }

            float dist = Vector3.Distance(transform.position, ball.transform.position);
            if (dist > 3.5f) return;

            int shotPowerAttr = runtimeState.attributes != null ? runtimeState.attributes.shotPower : 75;
            int curveAttr = runtimeState.attributes != null ? runtimeState.attributes.curve : 70;
            int finishingAttr = runtimeState.attributes != null ? runtimeState.attributes.finishing : 75;

            // Calculate base speed
            float speed = Mathf.Lerp(minShotSpeed, maxShotSpeed, power01) * (shotPowerAttr / 80f);
            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);

            Vector3 kickDir = aimDirection.sqrMagnitude > 0.1f ? aimDirection : (targetGoal - transform.position).normalized;
            kickDir.y = 0f;

            Vector3 initialVelocity;
            Vector3 spinAngularVelocity = Vector3.zero;

            switch (type)
            {
                case ShotType.Finesse:
                    // Curved shot targeting corner with side-spin
                    float curveSpin = Mathf.Lerp(25f, 50f, curveAttr / 100f);
                    spinAngularVelocity = Vector3.up * curveSpin;
                    kickDir = Quaternion.Euler(0f, -12f, 0f) * kickDir; // Angle initial release
                    initialVelocity = kickDir * (speed * 0.88f) + Vector3.up * (3.5f + power01 * 3.0f);
                    break;

                case ShotType.Chip:
                    // Steep elevation, low velocity, backspin
                    initialVelocity = kickDir * (speed * 0.65f) + Vector3.up * 8.5f;
                    spinAngularVelocity = Vector3.Cross(kickDir, Vector3.up) * -20f;
                    break;

                case ShotType.Power:
                    // Direct blast, low elevation, high speed
                    initialVelocity = kickDir * (speed * 1.15f) + Vector3.up * (1.5f + power01 * 2.0f);
                    spinAngularVelocity = Vector3.Cross(Vector3.up, kickDir) * 10f; // Top-spin dip
                    break;

                case ShotType.Standard:
                default:
                    initialVelocity = kickDir * speed + Vector3.up * (2.0f + power01 * 4.5f);
                    spinAngularVelocity = Vector3.up * Random.Range(-5f, 5f);
                    break;
            }

            // Pivot body towards strike target so foot kicks directly through the ball
            if (kickDir.sqrMagnitude > 0.05f)
            {
                transform.rotation = Quaternion.LookRotation(kickDir.normalized);
            }

            // Trigger visual kicking animation with the foot!
            var anim = GetComponent<ProceduralRunnerAnimator>();
            if (anim != null)
            {
                bool rightFoot = runtimeState.attributes == null || runtimeState.attributes.preferredFoot == Footedness.Right;
                anim.TriggerKickAnimation(rightFoot);
            }

            ball.Kick(initialVelocity, spinAngularVelocity, runtimeState.jerseyNumber, runtimeState.teamId);
            runtimeState.hasBall = false;

            GameEvents.TriggerShotTaken(runtimeState.teamId, type, power01);
        }

        public void StartPassCharge()
        {
            var ball = FootballBall.Instance;
            float dist = ball != null ? Vector3.Distance(transform.position, ball.transform.position) : 99f;
            if (!runtimeState.hasBall && dist > 3.0f) return;

            isChargingPass = true;
            currentPowerCharge = 0f;
        }

        public void ReleasePass(PassType type, Vector3 aimDirection, Transform targetTeammate = null)
        {
            if (!isChargingPass) return;
            isChargingPass = false;

            float power = currentPowerCharge;
            currentPowerCharge = 0f;

            ExecutePass(type, aimDirection, power, targetTeammate);
        }

        public void ExecutePass(PassType type, Vector3 aimDirection, float power01, Transform targetTeammate = null)
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // Goalkeeper drops ball from hands and punts with foot to teammate
            if (runtimeState.isHoldingBallInHands)
            {
                ExecuteGoalkeeperPunt(targetTeammate);
                return;
            }

            float dist = Vector3.Distance(transform.position, ball.transform.position);
            if (dist > 3.5f) return;

            // Ensure we always have a teammate of the same team to pass to!
            if (targetTeammate == null)
            {
                targetTeammate = FindTeammateTarget(aimDirection);
            }

            Vector3 targetPos;
            if (targetTeammate != null)
            {
                targetPos = targetTeammate.position;
                if (type == PassType.ThroughBall)
                {
                    // Lead the runner into space
                    targetPos += targetTeammate.forward * (3.5f + power01 * 6.0f);
                }
            }
            else
            {
                targetPos = transform.position + (aimDirection.sqrMagnitude > 0.05f ? aimDirection.normalized : transform.forward) * 12.0f;
            }

            // Pivot body towards teammate so foot strike aims directly at teammate!
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.05f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }

            // Trigger visual kicking animation with the foot!
            var animator = GetComponent<ProceduralRunnerAnimator>();
            if (animator != null)
            {
                bool rightFoot = runtimeState.attributes == null || runtimeState.attributes.preferredFoot == Footedness.Right;
                animator.TriggerKickAnimation(rightFoot);
            }

            float passDist = toTarget.magnitude;
            // Dynamic pass velocity: ensures ball reaches teammate's feet without getting stuck in turf
            float passSpeed = Mathf.Clamp(passDist * 1.15f + 7.5f, 13.0f, 26.0f);
            bool isLobbed = (type == PassType.Lobbed);

            ball.Pass(targetPos, passSpeed, isLobbed, runtimeState.jerseyNumber, runtimeState.teamId);
            runtimeState.hasBall = false;

            if (targetTeammate != null)
            {
                GameEvents.TriggerPassInitiated(runtimeState.teamId, targetTeammate);
            }
        }

        private Transform FindTeammateTarget(Vector3 aimDir)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            Transform best = null;
            float bestScore = float.MinValue;
            Transform nearest = null;
            float minD = float.MaxValue;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p == runtimeState || p.teamId != runtimeState.teamId || p.isSentOff) continue;

                Vector3 to = p.transform.position - transform.position;
                to.y = 0;
                float d = to.magnitude;
                if (d < 1.0f || d > 45f) continue;

                if (d < minD)
                {
                    minD = d;
                    nearest = p.transform;
                }

                float dot = Vector3.Dot(to.normalized, aimDir.sqrMagnitude > 0.05f ? aimDir.normalized : transform.forward);
                if (dot > 0.05f)
                {
                    float score = (dot * 50f) - d * 0.45f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = p.transform;
                    }
                }
            }

            return best ?? nearest;
        }

        public void ExecuteGoalkeeperPunt(Transform targetTeammate = null)
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // Release ball from goalkeeper's hands
            runtimeState.isHoldingBallInHands = false;

            if (targetTeammate == null)
            {
                targetTeammate = FindOutfieldTeammateTarget();
            }

            Vector3 targetPos;
            if (targetTeammate != null)
            {
                targetPos = targetTeammate.position;
            }
            else
            {
                Vector3 forwardGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
                targetPos = transform.position + (forwardGoal - transform.position).normalized * 32.0f;
            }

            // Pivot Goalkeeper towards target so kicking foot aims directly at teammate
            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.05f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }

            // Drop ball from hands down towards foot striking height (0.35m)
            ball.transform.position = transform.position + transform.forward * 0.65f + Vector3.up * 0.35f;

            // Trigger visual kicking animation with the foot!
            var anim = GetComponent<ProceduralRunnerAnimator>();
            if (anim != null)
            {
                bool rightFoot = runtimeState.attributes == null || runtimeState.attributes.preferredFoot == Footedness.Right;
                anim.TriggerKickAnimation(rightFoot);
            }

            float passDist = toTarget.magnitude;
            float puntSpeed = Mathf.Clamp(passDist * 1.1f + 9.5f, 18.0f, 32.0f);

            // Execute high lofted punt kick with foot to outfield teammate
            ball.Pass(targetPos, puntSpeed, lobbed: true, runtimeState.jerseyNumber, runtimeState.teamId);
            runtimeState.hasBall = false;

            if (targetTeammate != null)
            {
                GameEvents.TriggerPassInitiated(runtimeState.teamId, targetTeammate);
            }
        }

        private Transform FindOutfieldTeammateTarget()
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            Transform bestTeammate = null;
            float bestScore = float.MinValue;
            Transform fallbackTeammate = null;
            float minFallbackDist = float.MaxValue;

            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p == runtimeState || p.teamId != runtimeState.teamId || p.isSentOff) continue;

                // Only outfield players (exclude goalkeepers)
                if (p.attributes != null && p.attributes.position == PlayerPosition.GK) continue;

                Vector3 toTeammate = p.transform.position - transform.position;
                toTeammate.y = 0f;
                float dist = toTeammate.magnitude;

                if (dist < 6.0f || dist > 65.0f) continue;

                if (dist < minFallbackDist)
                {
                    minFallbackDist = dist;
                    fallbackTeammate = p.transform;
                }

                // Prefer midfielders and forwards positioned downfield towards opponent goal
                float goalCloseness = -Vector3.Distance(p.transform.position, targetGoal);
                float distBonus = (dist > 18f && dist < 45f) ? 25f : 0f;
                float score = goalCloseness + distBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTeammate = p.transform;
                }
            }

            return bestTeammate ?? fallbackTeammate;
        }

        #endregion

        #region Tackling API

        public void ExecuteStandingTackle()
        {
            if (isSlideTackling || runtimeState.hasBall) return;

            // Cannot tackle if any goalkeeper is holding ball with hands (foul)
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i].isHoldingBallInHands) return;
            }

            runtimeState.currentState = MovementState.Tackling;
            var ball = FootballBall.Instance;
            if (ball == null) return;

            float distToBall = Vector3.Distance(transform.position, ball.transform.position);

            if (distToBall <= standingTackleRadius)
            {
                // Successfully won ball
                Vector3 tacklePokeDir = transform.forward + Vector3.up * 0.1f;
                ball.Kick(tacklePokeDir * standingTackleForce, Vector3.zero, runtimeState.jerseyNumber, runtimeState.teamId);
                GameEvents.TriggerTackleExecuted(runtimeState.teamId, true);
            }
            else
            {
                GameEvents.TriggerTackleExecuted(runtimeState.teamId, false);
            }
        }

        public void ExecuteSlideTackle()
        {
            if (isSlideTackling || runtimeState.hasBall) return;

            // Cannot tackle if any goalkeeper is holding ball with hands (foul)
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i].isHoldingBallInHands) return;
            }

            isSlideTackling = true;
            slideTackleTimer = slideTackleDuration;
            slideDirection = transform.forward;
            runtimeState.currentState = MovementState.Tackling;

            // Check contact with ball vs opponent player
            Collider[] hits = UnityEngine.Physics.OverlapSphere(transform.position + slideDirection * 1.5f, 1.4f);
            bool hitBall = false;
            PlayerRuntimeState trippedOpponent = null;

            foreach (var hit in hits)
            {
                var ball = hit.GetComponent<FootballBall>() ?? hit.GetComponentInParent<FootballBall>();
                if (ball != null || hit.name.Contains("Ball"))
                {
                    hitBall = true;
                    if (ball != null)
                    {
                        Vector3 kickDir = (slideDirection + Vector3.up * 0.2f).normalized;
                        ball.Kick(kickDir * 18.0f, Vector3.zero, runtimeState.jerseyNumber, runtimeState.teamId);
                    }
                }
                else
                {
                    var otherPlayer = hit.GetComponent<PlayerRuntimeState>();
                    if (otherPlayer != null && otherPlayer.teamId != runtimeState.teamId)
                    {
                        trippedOpponent = otherPlayer;
                    }
                }
            }

            // Referee evaluation: if player tripped without getting ball first, it's a foul!
            if (trippedOpponent != null && !hitBall)
            {
                bool isInsidePenaltyBox = PitchConstants.IsInsidePenaltyBox(transform.position, runtimeState.teamId == 1);
                GameEvents.TriggerFoulCalled(runtimeState.teamId, transform.position, isInsidePenaltyBox);
            }
        }

        #endregion
    }
}
