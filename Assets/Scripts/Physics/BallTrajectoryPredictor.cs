using System.Collections.Generic;
using UnityEngine;

namespace Football.PhysicsEngine
{
    /// <summary>
    /// Trajectory simulation engine used for AI interception calculations and aiming arc projections.
    /// Uses numerical integration (Euler / Verlet) matching the aerodynamic constants in FootballBall.
    /// </summary>
    public static class BallTrajectoryPredictor
    {
        private const float SimTimeStep = 0.02f;

        /// <summary>
        /// Predicts the trajectory path points given an initial velocity and spin.
        /// </summary>
        public static List<Vector3> PredictTrajectory(Vector3 startPos, Vector3 initialVel, Vector3 spinAngularVel, float maxDuration = 2.5f)
        {
            var points = new List<Vector3>();
            Vector3 pos = startPos;
            Vector3 vel = initialVel;
            Vector3 spin = spinAngularVel;

            float elapsed = 0f;
            float dragCoeff = 0.28f;
            float airDensity = 1.225f;
            float area = Mathf.PI * 0.11f * 0.11f;
            float mass = 0.43f;
            float liftCoeff = 0.22f;
            float radius = 0.11f;

            points.Add(pos);

            while (elapsed < maxDuration && pos.y >= 0.05f)
            {
                float speed = vel.magnitude;
                Vector3 forces = UnityEngine.Physics.gravity * mass;

                // Drag
                if (speed > 0.01f)
                {
                    float dragMag = 0.5f * airDensity * dragCoeff * area * speed * speed;
                    forces += -vel.normalized * dragMag;
                }

                // Magnus
                if (spin.sqrMagnitude > 0.1f)
                {
                    Vector3 magnusDir = Vector3.Cross(spin, vel);
                    float magnusMag = 0.5f * airDensity * liftCoeff * area * radius * magnusDir.magnitude;
                    if (magnusDir.sqrMagnitude > 0.0001f)
                    {
                        forces += magnusDir.normalized * magnusMag;
                    }
                }

                // Integrate acceleration
                Vector3 accel = forces / mass;
                vel += accel * SimTimeStep;
                pos += vel * SimTimeStep;

                points.Add(pos);
                elapsed += SimTimeStep;
            }

            return points;
        }

        /// <summary>
        /// Calculates interception time and position for an intercepting agent moving at maxSpeed.
        /// </summary>
        public static bool TryFindInterceptionPoint(Vector3 agentPos, float agentSpeed, Vector3 ballStartPos, Vector3 ballVel, out Vector3 interceptPos, out float interceptTime)
        {
            interceptPos = ballStartPos;
            interceptTime = 0f;

            Vector3 currentBallPos = ballStartPos;
            Vector3 currentBallVel = ballVel;
            float t = 0f;
            float dt = 0.04f;

            while (t < 3.0f)
            {
                // Ball ballistic update
                currentBallVel += UnityEngine.Physics.gravity * dt;
                currentBallPos += currentBallVel * dt;
                if (currentBallPos.y < 0.11f)
                {
                    currentBallPos.y = 0.11f;
                    currentBallVel.y = 0f;
                    currentBallVel *= 0.95f; // Ground friction damping
                }

                t += dt;

                // Time for agent to run to this projected ball pos
                float dist = Vector3.Distance(new Vector3(agentPos.x, 0, agentPos.z), new Vector3(currentBallPos.x, 0, currentBallPos.z));
                float timeToReach = dist / Mathf.Max(agentSpeed, 1.0f);

                if (timeToReach <= t)
                {
                    interceptPos = currentBallPos;
                    interceptTime = t;
                    return true;
                }
            }

            return false;
        }
    }
}
