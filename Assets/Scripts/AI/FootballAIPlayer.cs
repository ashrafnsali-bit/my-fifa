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

            // 1. RELENTLESS CLINICAL SHOOTING (Within 38m of opponent goal):
            // AI actively hunts goals with unstoppable strikes!
            if (distToGoal < 38.0f)
            {
                currentAIState = AIState.Shooting;

                // Target post corners (aiming left post or right post for unstoppable finishes)
                Vector3 leftPost = targetGoal + Vector3.right * 3.1f;
                Vector3 rightPost = targetGoal - Vector3.right * 3.1f;
                Vector3 chosenPost = (Random.value > 0.5f) ? leftPost : rightPost;

                Vector3 aim = (chosenPost - transform.position).normalized;
                aim.y = 0f;

                // High lethal shot power to beat the goalkeeper
                float power = Mathf.Clamp(0.75f + (distToGoal / 38f) * 0.24f, 0.70f, 0.99f);
                ShotType sType = distToGoal < 16f ? ShotType.Finesse : (distToGoal > 22f ? ShotType.Power : ShotType.Standard);

                actions.ExecuteShot(sType, aim, power);
                return;
            }

            // 2. FORWARD ATTACKING RUN & THROUGH BALLS:
            // Check if a teammate striker/winger is in front with a clearer path to goal
            var forwardPassTarget = FindBestPassOption();
            if (forwardPassTarget != null)
            {
                float teammateDistToGoal = Vector3.Distance(forwardPassTarget.position, targetGoal);
                if (teammateDistToGoal < distToGoal - 4.0f)
                {
                    currentAIState = AIState.Passing;
                    Vector3 passDir = (forwardPassTarget.position - transform.position).normalized;
                    passDir.y = 0f;
                    PassType pType = (distToGoal > 45.0f && Mathf.Abs(transform.position.x) > 18.0f) ? PassType.Lobbed : PassType.ThroughBall;
                    actions.ExecutePass(pType, passDir, 0.85f, forwardPassTarget);
                    return;
                }
            }

            // 3. EXPLOSIVE SPRINT DRIBBLE DIRECTLY ON GOAL:
            Vector3 forwardDribbleDir = (targetGoal - transform.position).normalized;
            forwardDribbleDir.y = 0f;

            var pressingOpponent = GetNearestOpponent();
            if (pressingOpponent != null && Vector3.Distance(transform.position, pressingOpponent.transform.position) < 2.0f)
            {
                Vector3 awayFromOpp = (transform.position - pressingOpponent.transform.position).normalized;
                awayFromOpp.y = 0f;
                forwardDribbleDir = (forwardDribbleDir * 2.0f + awayFromOpp).normalized;
            }

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

            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
            float distToBall = Vector3.Distance(transform.position, ball.transform.position);

            // 1. DYNAMIC ATTACKING SURGE: If a teammate has possession, charge towards opponent goal!
            if (DoesTeammateHaveBall())
            {
                currentAIState = AIState.DribblingSpace;
                var pos = runtimeState.attributes != null ? runtimeState.attributes.position : PlayerPosition.CM;

                Vector3 runTarget;
                if (pos == PlayerPosition.ST)
                {
                    // Striker sprints straight into the penalty spot to score!
                    runTarget = targetGoal + (transform.position - targetGoal).normalized * 12.0f;
                }
                else if (pos == PlayerPosition.LW || pos == PlayerPosition.RW || pos == PlayerPosition.LM || pos == PlayerPosition.RM)
                {
                    // Wingers sprint into dangerous crossing / cutback positions inside 18m
                    runTarget = targetGoal + (transform.position - targetGoal).normalized * 16.0f + (pos == PlayerPosition.LW || pos == PlayerPosition.LM ? Vector3.left : Vector3.right) * 8.0f;
                }
                else if (pos == PlayerPosition.CAM || pos == PlayerPosition.CM)
                {
                    // Midfielders arrive at the edge of the box (22m) for long shots & rebounds
                    runTarget = targetGoal + (transform.position - targetGoal).normalized * 22.0f;
                }
                else
                {
                    // Defenders push up high to midfield (tactical anchor)
                    runTarget = tacticalAnchor;
                }

                runTarget.y = 0f;
                Vector3 toRun = runTarget - transform.position;
                toRun.y = 0f;

                if (toRun.magnitude > 1.0f)
                {
                    locomotion.SetWorldMovementInput(toRun.normalized, true);
                }
                else
                {
                    locomotion.SetWorldMovementInput(Vector3.zero, false);
                }
                return;
            }

            // 2. RELENTLESS HIGH PRESS & TACKLING:
            bool isFirstPresser = (tacticsController != null && tacticsController.closestPlayerToBall == this) || distToBall < 6.0f;
            bool isSecondPresser = (tacticsController != null && tacticsController.secondClosestPlayerToBall == this) || distToBall < 10.0f;

            if (isFirstPresser || distToBall < 3.5f)
            {
                currentAIState = AIState.PressingBall;
                Vector3 toBall = ball.transform.position - transform.position;
                toBall.y = 0f;

                // Full sprint to win the ball!
                locomotion.SetWorldMovementInput(toBall.normalized, true);

                // Tackle with aggression
                if (distToBall < 1.8f)
                {
                    if (Random.value < 0.80f)
                    {
                        actions.ExecuteStandingTackle();
                    }
                    else if (distToBall > 1.0f && distToBall < 2.2f)
                    {
                        actions.ExecuteSlideTackle();
                    }
                }
            }
            else if (isSecondPresser)
            {
                // Secondary press: close down carrier and cut passing angles
                currentAIState = AIState.PressingBall;
                Vector3 ballPos = ball.transform.position;
                Vector3 pressPos = ballPos + (transform.position - ballPos).normalized * 2.5f;
                pressPos.y = 0f;

                Vector3 toPress = pressPos - transform.position;
                toPress.y = 0f;
                locomotion.SetWorldMovementInput(toPress.normalized, true);
            }
            else
            {
                // Compact high defensive line
                currentAIState = AIState.HoldingAnchor;
                Vector3 toAnchor = tacticalAnchor - transform.position;
                toAnchor.y = 0f;
                float dist = toAnchor.magnitude;

                if (dist > 1.2f)
                {
                    locomotion.SetWorldMovementInput(toAnchor.normalized, dist > 4.0f);
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
            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(runtimeState.teamId);
            float myDistToGoal = Vector3.Distance(myPos, targetGoal);
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);

            for (int i = 0; i < tacticsController.teamPlayers.Count; i++)
            {
                var teammate = tacticsController.teamPlayers[i];
                if (teammate == null || teammate == this || teammate.RuntimeState == null || teammate.RuntimeState.isSentOff) continue;

                Vector3 teammatePos = teammate.transform.position;
                Vector3 toTeammate = teammatePos - myPos;
                toTeammate.y = 0;
                float dist = toTeammate.magnitude;

                if (dist < 2.0f || dist > 48.0f) continue;

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

                // Tactical score: forward progression closer to opponent goal, distance preference, and lane clearance
                float teammateDistToGoal = Vector3.Distance(teammatePos, targetGoal);
                float progressScore = myDistToGoal - teammateDistToGoal; // Positive if teammate is closer to goal
                float score = progressScore * 2.0f + (40f - dist) * 0.4f + (laneClear ? 30f : -25f);

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
