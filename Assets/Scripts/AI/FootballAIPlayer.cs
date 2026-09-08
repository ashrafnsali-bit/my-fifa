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

            // 3. Human team (Team 1) in possession: NEVER auto-pass or auto-shoot outfield players!
            // The outfield player must wait for user button inputs before kicking.
            if (runtimeState.teamId == 1 && runtimeState.hasBall)
            {
                currentAIState = AIState.HoldingAnchor;
                return;
            }

            decisionTimer -= Time.deltaTime;
            if (decisionTimer <= 0f)
            {
                float latency = tacticsController != null && tacticsController.difficultySettings != null
                    ? tacticsController.difficultySettings.reactionLatency
                    : 0.2f;
                decisionTimer = latency;

                EvaluateTacticalDecision();
            }
        }

        private void EvaluateTacticalDecision()
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // 1. Goalkeeper branch (also called every frame in Update)
            if (runtimeState.attributes.position == PlayerPosition.GK)
            {
                ExecuteGoalkeeperBehavior(ball);
                return;
            }

            // 2. In-Possession branch
            if (runtimeState.hasBall)
            {
                ExecuteInPossessionBehavior(ball);
                return;
            }

            // 3. Out-of-Possession branch
            ExecuteOutOfPossessionBehavior(ball);
        }

        private void ExecuteGoalkeeperBehavior(FootballBall ball)
        {
            currentAIState = AIState.Goalkeeping;

            // A. If Goalkeeper is holding the ball in his hands:
            if (runtimeState.isHoldingBallInHands)
            {
                locomotion.SetWorldMovementInput(Vector3.zero, false);
                gkHoldTimer += Time.deltaTime;

                // After brief pause (1.4s) holding the ball in hands, drop and punt with foot to an open teammate!
                if (gkHoldTimer >= 1.4f)
                {
                    gkHoldTimer = 0f;
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

            // B. Immediate clean catch if ball is right within arms reach inside box
            if (insideBox && distToBall <= 2.4f && ballPos.y <= 2.8f)
            {
                if (ballVel.magnitude < 15.0f || distToBall <= 1.8f)
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

            // C. SHOT INCOMING TOWARDS GOAL: predict trajectory and sprint to intercept / dive!
            bool isBallMovingToGoal = isHomeSide ? (ballVel.z < -3.5f) : (ballVel.z > 3.5f);
            if (isBallMovingToGoal)
            {
                float timeToGoalLine = Mathf.Abs((ownGoal.z - ballPos.z) / ballVel.z);
                if (timeToGoalLine > 0f && timeToGoalLine < 2.5f)
                {
                    // Predict shot intersection point along the goal line
                    float predictedX = ballPos.x + ballVel.x * timeToGoalLine;
                    float goalHalfWidth = PitchConstants.GoalWidth * 0.5f + 1.2f;

                    // If shot is directed on target or shaving the posts
                    if (Mathf.Abs(predictedX) <= goalHalfWidth)
                    {
                        float defendZ = ownGoal.z + (isHomeSide ? 2.0f : -2.0f);
                        Vector3 saveInterceptPos = new Vector3(Mathf.Clamp(predictedX, -goalHalfWidth, goalHalfWidth), 0f, defendZ);

                        Vector3 toIntercept = saveInterceptPos - transform.position;
                        toIntercept.y = 0f;

                        // Sprint urgently to cut off the shot line!
                        locomotion.SetWorldMovementInput(toIntercept.normalized, true);

                        // If ball is within diving reach (up to 3.2m): make the catch or diving block!
                        if (distToBall <= 3.2f)
                        {
                            if (distToBall <= 2.3f && ballVel.magnitude < 14.0f)
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

            // D. LOOSE OR FREE BALL IN PENALTY BOX: rush out to smother and catch it!
            bool ballInDefendingThird = isHomeSide ? (ballPos.z < -28.0f) : (ballPos.z > 28.0f);
            if (ballInDefendingThird && distBallToGoal < 20.0f && ballVel.magnitude < 12.0f)
            {
                Vector3 toLooseBall = ballPos - transform.position;
                toLooseBall.y = 0f;

                locomotion.SetWorldMovementInput(toLooseBall.normalized, true);

                if (distToBall <= 2.4f)
                {
                    locomotion.CatchBallInHands(ball);
                }
                return;
            }

            // E. STANDARD ANGLE BISECTOR POSITIONING: cut down shooting angles
            Vector3 toBall = (ballPos - ownGoal).normalized;
            toBall.y = 0f;

            float stepOut = Mathf.Clamp(distBallToGoal * 0.22f, 1.8f, 4.8f);
            Vector3 targetGkPos = ownGoal + toBall * stepOut;
            targetGkPos.x = Mathf.Clamp(targetGkPos.x, -PitchConstants.GoalWidth * 0.45f, PitchConstants.GoalWidth * 0.45f);
            targetGkPos.y = 0f;

            Vector3 moveDir = targetGkPos - transform.position;
            moveDir.y = 0f;

            if (moveDir.magnitude > 0.20f)
            {
                locomotion.SetWorldMovementInput(moveDir.normalized, moveDir.magnitude > 2.5f);
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

            // Shoot if within scoring range (< 26m)
            if (distToGoal < 26.0f)
            {
                currentAIState = AIState.Shooting;
                locomotion.SetMovementInput(Vector2.zero, false);

                Vector3 aim = (targetGoal - transform.position).normalized;
                // Add slight inaccuracy based on difficulty
                float errorAngle = tacticsController != null ? (1f - tacticsController.difficultySettings.shotAccuracy) * 12f : 3f;
                aim = Quaternion.Euler(0f, Random.Range(-errorAngle, errorAngle), 0f) * aim;

                actions.ExecuteShot(distToGoal < 16f ? ShotType.Standard : ShotType.Finesse, aim, Random.Range(0.6f, 0.95f));
                return;
            }

            // Evaluate passing to open teammate of same team
            var bestPassTarget = FindBestPassOption();
            bool underPressure = IsOpponentPressing();
            if (bestPassTarget != null && (underPressure || Random.value < 0.80f))
            {
                currentAIState = AIState.Passing;
                Vector3 passDir = (bestPassTarget.position - transform.position).normalized;
                actions.ExecutePass(PassType.Ground, passDir, 0.65f, bestPassTarget);
                return;
            }

            // Otherwise dribble into open space towards opponent goal
            currentAIState = AIState.DribblingSpace;
            Vector3 forwardDribbleDir = (targetGoal - transform.position).normalized;
            Vector2 dribbleInput = new Vector2(forwardDribbleDir.x, forwardDribbleDir.z);
            locomotion.SetMovementInput(dribbleInput, false);
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

        private void ExecuteOutOfPossessionBehavior(FootballBall ball)
        {
            // If designated as pressing player
            if (tacticsController != null && tacticsController.closestPlayerToBall == this)
            {
                currentAIState = AIState.PressingBall;
                Vector3 toBall = ball.transform.position - transform.position;
                Vector2 pressInput = new Vector2(toBall.x, toBall.z).normalized;

                float distToBall = toBall.magnitude;
                bool shouldSprint = distToBall > 3.0f;
                locomotion.SetMovementInput(pressInput, shouldSprint);

                // If within tackling reach
                if (distToBall < 1.5f)
                {
                    if (Random.value < 0.8f)
                    {
                        actions.ExecuteStandingTackle();
                    }
                    else if (distToBall > 1.2f)
                    {
                        actions.ExecuteSlideTackle();
                    }
                }
            }
            else
            {
                // Hold tactical anchor
                currentAIState = AIState.HoldingAnchor;
                Vector3 toAnchor = tacticalAnchor - transform.position;
                float dist = toAnchor.magnitude;

                if (dist > 1.5f)
                {
                    Vector2 anchorInput = new Vector2(toAnchor.x, toAnchor.z).normalized;
                    locomotion.SetMovementInput(anchorInput, dist > 8.0f);
                }
                else
                {
                    locomotion.SetMovementInput(Vector2.zero, false);
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
