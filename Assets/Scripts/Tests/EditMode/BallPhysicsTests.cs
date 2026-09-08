using NUnit.Framework;
using UnityEngine;
using Football.PhysicsEngine;

namespace Football.Tests
{
    public class BallPhysicsTests
    {
        [Test]
        public void PredictTrajectory_GravityDropsBall()
        {
            Vector3 start = new Vector3(0f, 5f, 0f);
            Vector3 vel = new Vector3(0f, 0f, 10f);
            Vector3 spin = Vector3.zero;

            var points = BallTrajectoryPredictor.PredictTrajectory(start, vel, spin, 1.0f);

            Assert.Greater(points.Count, 1, "Trajectory must contain multiple points.");
            Assert.Less(points[points.Count - 1].y, start.y, "End position Y must be lower than start position Y due to gravity.");
        }

        [Test]
        public void PredictTrajectory_MagnusEffectCurvesBall()
        {
            Vector3 start = new Vector3(0f, 1f, 0f);
            Vector3 vel = new Vector3(0f, 0f, 20f); // Moving straight forward along Z

            // Spin around Y axis (side spin)
            Vector3 spin = new Vector3(0f, 40f, 0f);

            var pointsWithSpin = BallTrajectoryPredictor.PredictTrajectory(start, vel, spin, 1.0f);
            var pointsNoSpin = BallTrajectoryPredictor.PredictTrajectory(start, vel, Vector3.zero, 1.0f);

            Vector3 endWithSpin = pointsWithSpin[pointsWithSpin.Count - 1];
            Vector3 endNoSpin = pointsNoSpin[pointsNoSpin.Count - 1];

            // Magnus force F = w x v: (0, 40, 0) x (0, 0, 20) = (800, 0, 0) -> Curves along +X
            Assert.Greater(endWithSpin.x, endNoSpin.x + 0.5f, "Spin around Y axis must deflect ball along X axis due to Magnus effect.");
        }

        [Test]
        public void InterceptionPoint_FindsValidTarget()
        {
            Vector3 agentPos = new Vector3(5f, 0f, 10f);
            float agentSpeed = 8.0f;
            Vector3 ballStart = new Vector3(0f, 0.11f, 0f);
            Vector3 ballVel = new Vector3(0f, 0f, 15f);

            bool found = BallTrajectoryPredictor.TryFindInterceptionPoint(agentPos, agentSpeed, ballStart, ballVel, out Vector3 interceptPos, out float t);

            Assert.IsTrue(found, "Interception point should be successfully found.");
            Assert.Greater(t, 0f, "Interception time must be greater than zero.");
        }
    }
}
