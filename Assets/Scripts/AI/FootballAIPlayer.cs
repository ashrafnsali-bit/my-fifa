using System.Collections.Generic;
using UnityEngine;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Tactics
{
    public enum AIState
    {
        HoldingAnchor,
        PressingBall,
        DribblingSpace,
        Passing,
        Shooting,
        Goalkeeping
    }

    [RequireComponent(typeof(FootballPlayerLocomotion), typeof(FootballPlayerActions))]
    public class FootballAIPlayer : MonoBehaviour
    {
        public TeamTacticsController tacticsController;
        public AIState currentAIState = AIState.HoldingAnchor;
        public int formationSlotIndex = 0;

        private FootballPlayerLocomotion locomotion;
        private FootballPlayerActions actions;
        private PlayerRuntimeState runtimeState;

        private Vector3 tacticalAnchor;
        private float decisionTimer = 0f;
        private float gkHoldTimer = 0f;

        public PlayerRuntimeState RuntimeState => runtimeState;
        public FootballPlayerLocomotion Locomotion => locomotion;

        private void Awake()
        {
            locomotion = GetComponent<FootballPlayerLocomotion>();
            actions = GetComponent<FootballPlayerActions>();
            runtimeState = GetComponent<PlayerRuntimeState>();
        }

        public void SetTacticalAnchor(Vector3 anchor)
        {
            tacticalAnchor = anchor;
        }

        private void Update()
        {
            if (runtimeState.isSentOff) return;

            // 0. Hold tactical formation anchor and freeze completely unless actively in play!
            if (GameEvents.CurrentMatchState != MatchState.InPlay)
            {
                currentAIState = AIState.HoldingAnchor;
                locomotion.SetWorldMovementInput(Vector3.zero, false);
                return;
            }

            // 1. If this player is controlled by the human user, NEVER execute AI actions!
            var inputHandler = GetComponent<FootballInputHandler>();
            if (inputHandler != null && inputHandler.isHumanControlled)
            {
                return;
            }

            // 2. Goalkeepers must react with instantaneous reflex every frame!
            if (runtimeState.attributes != null && runtimeState.attributes.position == PlayerPosition.GK)
            {
                var b = FootballBall.Instance;
                if (b != null)
                {
                    ExecuteGoalkeeperBehavior(b);
                }
                return;
            }

            var ball = FootballBall.Instance;
            if (ball == null) return;

            // 3. Continuous Tactical Execution every frame for both attacking and defending!
            if (runtimeState.hasBall)
            {
                ExecuteInPossessionBehavior(ball);
            }
            else
            {
                ExecuteOutOfPossessionBehavior(ball);
            }
        }

        private void ExecuteGoalkeeperBehavior(FootballBall ball)
        {
            if (GameEvents.CurrentMatchState != MatchState.InPlay)
            {
                currentAIState = AIState.HoldingAnchor;
                locomotion.SetWorldMovementInput(Vector3.zero, false);
                return;
            }

            currentAIState = AIState.Goalkeeping;

            // A. If Goalkeeper is holding the ball in his hands:
            if (runtimeState.isHoldingBallInHands)
            {
                locomotion.SetWorldMovementInput(Vector3.zero, false);
                gkHoldTimer += Time.deltaTime;

                // After brief pause (2.5s) holding the ball in hands, drop and punt with foot to an open teammate!
                if (gkHoldTimer >= 2.5f)
                {
                    gkHoldTimer = 0f;
                    // Pre-clear state BEFORE punt so locomotion doesn't re-catch on the next frame
                    runtimeState.isHoldingBallInHands = false;
                    runtimeState.hasBall = false;
                    // Start 6-second cooldown so GK doesn't immediately re-catch his own punt
                    locomotion.StartGoalkeeperReleaseCooldown();
                    actions.ExecuteGoalkeeperPunt();
                }
                return;
            }

            gkHoldTimer = 0f;
            Vector3 ownGoal = PitchConstants.GetDefendingGoalCenter(runtimeState.teamId);
            Vector3 ballPos = ball.transform.position;
            Vector3 ballVel = ball.Velocity;
            float distToBall = Vector3.Distance(transform.position, ballPos);
            float distBallToGoal = Vector3.Distance(ballPos, ownGoal);
            bool isHomeSide = (runtimeState.teamId == 1);
            bool insideBox = Vector3.Distance(transform.position, ownGoal) <= 24.0f;

            // Guard: If ball has already crossed the goal line (inside net or out of bounds):
            if (Mathf.Abs(ballPos.z) >= PitchConstants.HalfLength - 0.25f)
            {
                // Goalkeeper stands firmly in the center of the goal line, facing the pitch
                Vector3 gkHomePos = ownGoal + (isHomeSide ? Vector3.forward : Vector3.back) * 1.8f;
                Vector3 toHome = gkHomePos - transform.position;
                toHome.y = 0f;
                if (toHome.magnitude > 0.25f)
                {
                    locomotion.SetWorldMovementInput(toHome.normalized, false);
                }
                else
                {
                    locomotion.SetWorldMovementInput(Vector3.zero, false);
                }
                return;
            }

            // B. Immediate clean catch or save if ball is within arms reach
            if (insideBox && distToBall <= 2.8f && ballPos.y <= 2.8f)
            {
                // Do NOT re-catch if the GK just punted the ball (6-second cooldown)
                if (!locomotion.IsInReleaseCooldown)
                {
                    if (ballVel.magnitude < 13.0f || distToBall <= 2.0f)
                    {
                        locomotion.CatchBallInHands(ball);
                        return;
                    }
                    else
                    {
                        locomotion.ExecuteGoalkeeperBlockSave(ball);
                        return;
                    }
                }
            }

            // C. SHOT INCOMING TOWARDS GOAL: predict trajectory and sprint aggressively to intercept / dive!
            Vector3 toGoalVector = ownGoal - ballPos;
            float speedTowardsGoal = Vector3.Dot(ballVel, toGoalVector.normalized);
            bool isBallMovingToGoal = isHomeSide ? (ballVel.z < -1.5f) : (ballVel.z > 1.5f);

            if ((isBallMovingToGoal || (speedTowardsGoal > 2.5f && distBallToGoal < 38.0f)) && Mathf.Abs(ballVel.z) > 0.8f)
            {
                float timeToGoalLine = Mathf.Abs((ownGoal.z - ballPos.z) / ballVel.z);
                if (timeToGoalLine > 0f && timeToGoalLine < 4.2f)
                {
                    // Predict shot intersection point along the goal line
                    float predictedX = ballPos.x + ballVel.x * timeToGoalLine;
                    float goalHalfWidth = PitchConstants.GoalWidth * 0.5f + 1.6f;

                    // If shot is directed on target or shaving the posts
                    if (Mathf.Abs(predictedX) <= goalHalfWidth)
                    {
                        float stepOut = Mathf.Clamp(timeToGoalLine * 1.8f, 1.2f, 4.5f);
                        float defendZ = ownGoal.z + (isHomeSide ? stepOut : -stepOut);
                        Vector3 saveInterceptPos = new Vector3(Mathf.Clamp(predictedX, -PitchConstants.GoalWidth * 0.48f, PitchConstants.GoalWidth * 0.48f), 0f, defendZ);

                        Vector3 toIntercept = saveInterceptPos - transform.position;
                        toIntercept.y = 0f;

                        // Sprint with explosive urgency to cut off the shot line!
                        locomotion.SetWorldMovementInput(toIntercept.normalized, true);

                        // If ball is within diving reach (up to 4.5m) or about to pass into goal: DIVE!
                        if (distToBall <= 4.5f || (distToBall <= 5.5f && timeToGoalLine < 0.45f))
                        {
                            if (distToBall <= 2.5f && ballVel.magnitude < 12.0f && ballPos.y <= 2.6f)
                            {
                                locomotion.CatchBallInHands(ball);
                            }
                            else
                            {
                                locomotion.ExecuteGoalkeeperBlockSave(ball);
                            }
                        }
                        return;
                    }
                }
            }

            // D. 1-on-1 ATTACKER RUSH / LOOSE BALL IN PENALTY BOX: charge forward to smother!
            bool ballInDefendingThird = isHomeSide ? (ballPos.z < -26.0f) : (ballPos.z > 26.0f);
            if (ballInDefendingThird && distBallToGoal < 18.0f)
            {
                Vector3 toLooseBall = ballPos - transform.position;
                toLooseBall.y = 0f;

                // Charge out aggressively to smother and close angle
                locomotion.SetWorldMovementInput(toLooseBall.normalized, true);

                if (distToBall <= 2.8f)
                {
                    if (ballVel.magnitude < 13.0f && ballPos.y <= 2.6f)
                    {
                        locomotion.CatchBallInHands(ball);
                    }
                    else
                    {
                        locomotion.ExecuteGoalkeeperBlockSave(ball);
                    }
                }
                return;
            }

            // E. STANDARD ANGLE BISECTOR POSITIONING: cut down shooting angles
            Vector3 toBall = (ballPos - ownGoal).normalized;
            toBall.y = 0f;

            float angleStepOut = Mathf.Clamp(distBallToGoal * 0.24f, 1.8f, 5.0f);
            Vector3 targetGkPos = ownGoal + toBall * angleStepOut;
            targetGkPos.x = Mathf.Clamp(targetGkPos.x, -PitchConstants.GoalWidth * 0.46f, PitchConstants.GoalWidth * 0.46f);
            targetGkPos.y = 0f;

            Vector3 moveDir = targetGkPos - transform.position;
            moveDir.y = 0f;

            if (moveDir.magnitude > 0.15f)
            {
                locomotion.SetWorldMovementInput(moveDir.normalized, moveDir.magnitude > 1.8f);
            }
            else
            {
                locomotion.SetWorldMovementInput(Vector3.zero, false);
            }
        }

        private void ExecuteInPossessionBehavior(FootballBall ball)
        {
            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
            float distToGoal = Vector3.Distance(transform.position, targetGoal);

            // 1. RELENTLESS CLINICAL SHOOTING (Within 34m of opponent goal):
            // AI plays aggressively to score goals!
            if (distToGoal < 34.0f)
            {
                currentAIState = AIState.Shooting;

                // Target post corners (aiming left post or right post for unstoppable finishes)
                Vector3 leftPost = targetGoal + Vector3.right * 3.0f;
                Vector3 rightPost = targetGoal - Vector3.right * 3.0f;
                Vector3 chosenPost = (Random.value > 0.5f) ? leftPost : rightPost;

                Vector3 aim = (chosenPost - transform.position).normalized;
                aim.y = 0f;

                // High lethal shot power to beat the goalkeeper
                float power = Mathf.Clamp(0.72f + (distToGoal / 34f) * 0.25f, 0.65f, 0.98f);
                ShotType sType = distToGoal < 16f ? ShotType.Finesse : (distToGoal > 24f ? ShotType.Power : ShotType.Standard);

                actions.ExecuteShot(sType, aim, power);
                return;
            }

            // 2. FORWARD ATTACKING RUN (Sprint directly towards opponent goal):
            Vector3 forwardDribbleDir = (targetGoal - transform.position).normalized;
            forwardDribbleDir.y = 0f;

            // Evade nearby tackling defenders or slip a forward through pass
            var pressingOpponent = GetNearestOpponent();
            if (pressingOpponent != null && Vector3.Distance(transform.position, pressingOpponent.transform.position) < 2.2f)
            {
                var forwardPassTarget = FindBestPassOption();
                if (forwardPassTarget != null && Vector3.Distance(forwardPassTarget.position, targetGoal) < distToGoal)
                {
                    currentAIState = AIState.Passing;
                    Vector3 passDir = (forwardPassTarget.position - transform.position).normalized;
                    passDir.y = 0f;
                    actions.ExecutePass(PassType.Ground, passDir, 0.78f, forwardPassTarget);
                    return;
                }

                Vector3 awayFromOpp = (transform.position - pressingOpponent.transform.position).normalized;
                awayFromOpp.y = 0f;
                forwardDribbleDir = (forwardDribbleDir * 1.5f + awayFromOpp).normalized;
            }

            // Always drive and sprint directly on goal!
            currentAIState = AIState.DribblingSpace;
            locomotion.SetWorldMovementInput(forwardDribbleDir, true);
        }

        private PlayerRuntimeState GetNearestOpponent()
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            PlayerRuntimeState nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p.teamId == runtimeState.teamId || p.isSentOff) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = p;
                }
            }
            return nearest;
        }

        private bool IsOpponentPressing()
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p.teamId == runtimeState.teamId || p.isSentOff) continue;
                if (Vector3.Distance(transform.position, p.transform.position) < 3.2f)
                {
                    return true;
                }
            }
            return false;
        }

        private bool DoesTeammateHaveBall()
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p != null && p.teamId == runtimeState.teamId && p != runtimeState && p.hasBall)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOpponentGoalkeeperHoldingBall(int myTeamId)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p != null && p.teamId != myTeamId && p.isHoldingBallInHands)
                    return true;
            }
            return false;
        }

        private void ExecuteOutOfPossessionBehavior(FootballBall ball)
        {
            // Goalkeeper immunity: return to anchor if opposing GK holds ball
            if (IsOpponentGoalkeeperHoldingBall(runtimeState.teamId))
            {
                currentAIState = AIState.HoldingAnchor;
                Vector3 toAnchor = tacticalAnchor - transform.position;
                toAnchor.y = 0f;
                float dist = toAnchor.magnitude;
                if (dist > 1.2f)
                {
                    locomotion.SetWorldMovementInput(toAnchor.normalized, dist > 6.0f);
                }
                else
                {
                    locomotion.SetWorldMovementInput(Vector3.zero, false);
                }
                return;
            }

            float distToBall = Vector3.Distance(transform.position, ball.transform.position);

            // 1. OFFENSIVE SUPPORT: If a teammate has possession, advance into open space to support
            if (DoesTeammateHaveBall())
            {
                bool isPrimarySupport = (tacticsController != null && tacticsController.closestPlayerToBall == this);
                bool isSecondarySupport = (tacticsController != null && tacticsController.secondClosestPlayerToBall == this);

                if (isPrimarySupport)
                {
                    currentAIState = AIState.HoldingAnchor;
                    Vector3 ballPos = ball.transform.position;
                    Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
                    Vector3 supportPos = ballPos + (targetGoal - ballPos).normalized * 6.5f + (transform.position - ballPos).normalized * 4.5f;
                    supportPos.y = 0f;

                    Vector3 toSupport = supportPos - transform.position;
                    toSupport.y = 0f;
                    if (toSupport.magnitude > 0.8f)
                    {
                        locomotion.SetWorldMovementInput(toSupport.normalized, toSupport.magnitude > 4.0f);
                    }
                    else
                    {
                        locomotion.SetWorldMovementInput(Vector3.zero, false);
                    }
                    return;
                }
                else if (isSecondarySupport)
                {
                    currentAIState = AIState.HoldingAnchor;
                    Vector3 ballPos = ball.transform.position;
                    Vector3 supportPos = ballPos + (transform.position - ballPos).normalized * 9.0f;
                    supportPos.y = 0f;

                    Vector3 toSupport = supportPos - transform.position;
                    toSupport.y = 0f;
                    if (toSupport.magnitude > 1.0f)
                    {
                        locomotion.SetWorldMovementInput(toSupport.normalized, false);
                    }
                    else
                    {
                        locomotion.SetWorldMovementInput(Vector3.zero, false);
                    }
                    return;
                }
            }

            // 2. DEFENSIVE PRESSING & TACKLING: Relentlessly chase the ball carrier!
            bool isFirstPresser = (tacticsController != null && tacticsController.closestPlayerToBall == this) || distToBall < 5.0f;
            bool isSecondPresser = (tacticsController != null && tacticsController.secondClosestPlayerToBall == this);

            if (isFirstPresser)
            {
                currentAIState = AIState.PressingBall;
                Vector3 toBall = ball.transform.position - transform.position;
                toBall.y = 0f;

                // Full sprint to win the ball!
                locomotion.SetWorldMovementInput(toBall.normalized, distToBall > 1.8f);

                // Tackle when within range
                if (distToBall < 1.6f)
                {
                    if (Random.value < 0.75f)
                    {
                        actions.ExecuteStandingTackle();
                    }
                    else if (distToBall > 1.1f && distToBall < 1.8f)
                    {
                        actions.ExecuteSlideTackle();
                    }
                }
            }
            else if (isSecondPresser)
            {
                // Secondary cover: cut passing lane towards goal
                currentAIState = AIState.PressingBall;
                Vector3 ballPos = ball.transform.position;
                Vector3 ownGoal = PitchConstants.GetDefendingGoalCenter(runtimeState.teamId);
                Vector3 coverPos = ballPos + (ownGoal - ballPos).normalized * 4.5f;
                coverPos.y = 0f;

                Vector3 toCover = coverPos - transform.position;
                toCover.y = 0f;
                if (toCover.magnitude > 0.8f)
                {
                    locomotion.SetWorldMovementInput(toCover.normalized, toCover.magnitude > 3.5f);
                }
                else
                {
                    locomotion.SetWorldMovementInput(Vector3.zero, false);
                }
            }
            else
            {
                // Hold tactical anchor
                currentAIState = AIState.HoldingAnchor;
                Vector3 toAnchor = tacticalAnchor - transform.position;
                toAnchor.y = 0f;
                float dist = toAnchor.magnitude;

                if (dist > 1.2f)
                {
                    locomotion.SetWorldMovementInput(toAnchor.normalized, dist > 6.0f);
                }
                else
                {
                    locomotion.SetWorldMovementInput(Vector3.zero, false);
                }
            }
        }

        private Transform FindBestPassOption()
        {
            if (tacticsController == null || tacticsController.teamPlayers == null) return null;

            Transform bestTeammate = null;
            float bestScore = float.MinValue;
            Transform fallbackTeammate = null;
            float minFallbackDist = float.MaxValue;

            Vector3 myPos = transform.position;
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);

            for (int i = 0; i < tacticsController.teamPlayers.Count; i++)
            {
                var teammate = tacticsController.teamPlayers[i];
                if (teammate == null || teammate == this || teammate.RuntimeState == null || teammate.RuntimeState.isSentOff) continue;

                Vector3 teammatePos = teammate.transform.position;
                Vector3 toTeammate = teammatePos - myPos;
                toTeammate.y = 0;
                float dist = toTeammate.magnitude;

                if (dist < 2.0f || dist > 44.0f) continue;

                if (dist < minFallbackDist)
                {
                    minFallbackDist = dist;
                    fallbackTeammate = teammate.transform;
                }

                // Geometric passing lane clearance: check if any opponent intercepts line
                bool laneClear = true;
                Vector3 passDir = toTeammate.normalized;

                for (int j = 0; j < allPlayers.Length; j++)
                {
                    var opp = allPlayers[j];
                    if (opp == null || opp.teamId == runtimeState.teamId || opp.isSentOff) continue;

                    Vector3 toOpp = opp.transform.position - myPos;
                    toOpp.y = 0;
                    float dot = Vector3.Dot(toOpp, passDir);

                    if (dot > 1.2f && dot < dist - 1.0f)
                    {
                        Vector3 proj = myPos + passDir * dot;
                        if (Vector3.Distance(opp.transform.position, proj) < 1.4f)
                        {
                            laneClear = false;
                            break;
                        }
                    }
                }

                // Tactical score: forward progression towards opponent goal, distance preference, and lane clearance
                float progressScore = (tacticsController.teamId == 1) ? toTeammate.z : -toTeammate.z;
                float score = progressScore * 1.5f + (36f - dist) * 0.4f + (laneClear ? 25f : -30f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTeammate = teammate.transform;
                }
            }

            return bestTeammate ?? fallbackTeammate;
        }
    }
}
