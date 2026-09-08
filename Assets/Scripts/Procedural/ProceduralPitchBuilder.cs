using UnityEngine;
using Football.Core;

namespace Football.Procedural
{
    public class ProceduralPitchBuilder : MonoBehaviour
    {
        [Header("Materials & Colors")]
        public Material turfMaterialLight;
        public Material turfMaterialDark;
        public Material lineMaterial;
        public Material goalFrameMaterial;

        [Header("Visual Config")]
        public int mowerStripeCount = 15;
        public float lineWidth = 0.12f;

        private void Start()
        {
            if (transform.childCount == 0)
            {
                BuildCompletePitch();
            }
        }

        [ContextMenu("Rebuild Pitch")]
        public void BuildCompletePitch()
        {
            // Clear existing children
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            BuildTurfSurface();
            BuildPitchLines();
            BuildGoalFrames();
            StadiumArchitect.BuildWorldCupStadium(transform);
        }

        private void BuildTurfSurface()
        {
            GameObject turfRoot = new GameObject("TurfSurface");
            turfRoot.transform.SetParent(transform, false);

            if (turfMaterialLight == null)
            {
                turfMaterialLight = StadiumArchitect.CreateMaterial(new Color(0.20f, 0.54f, 0.24f), 0.35f);
                var texLight = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.22f, 0.58f, 0.26f), new Color(0.18f, 0.50f, 0.22f));
                if (turfMaterialLight.HasProperty("_BaseMap")) turfMaterialLight.SetTexture("_BaseMap", texLight);
                else if (turfMaterialLight.HasProperty("_MainTex")) turfMaterialLight.SetTexture("_MainTex", texLight);
            }
            if (turfMaterialDark == null)
            {
                turfMaterialDark = StadiumArchitect.CreateMaterial(new Color(0.15f, 0.44f, 0.18f), 0.35f);
                var texDark = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.17f, 0.48f, 0.20f), new Color(0.13f, 0.40f, 0.16f));
                if (turfMaterialDark.HasProperty("_BaseMap")) turfMaterialDark.SetTexture("_BaseMap", texDark);
                else if (turfMaterialDark.HasProperty("_MainTex")) turfMaterialDark.SetTexture("_MainTex", texDark);
            }
            if (lineMaterial == null)
            {
                lineMaterial = StadiumArchitect.CreateMaterial(new Color(0.98f, 0.98f, 0.98f), 0.15f);
            }

            float stripeLength = PitchConstants.PitchLength / mowerStripeCount;
            float startZ = -PitchConstants.HalfLength;

            for (int i = 0; i < mowerStripeCount; i++)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = $"TurfStripe_{i}";
                stripe.transform.SetParent(turfRoot.transform, false);

                float centerZ = startZ + (i + 0.5f) * stripeLength;
                stripe.transform.position = new Vector3(0f, -0.05f, centerZ);
                stripe.transform.localScale = new Vector3(PitchConstants.PitchWidth + 8.0f, 0.1f, stripeLength);

                var renderer = stripe.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = (i % 2 == 0) ? turfMaterialLight : turfMaterialDark;
            }
        }

        private void BuildPitchLines()
        {
            GameObject linesRoot = new GameObject("PitchLines");
            linesRoot.transform.SetParent(transform, false);

            float halfW = PitchConstants.HalfWidth;
            float halfL = PitchConstants.HalfLength;

            // 1. Boundary Perimeter Lines
            CreateLineQuad(linesRoot.transform, "Touchline_Left", new Vector3(-halfW, 0.01f, 0f), new Vector3(lineWidth, 1f, PitchConstants.PitchLength));
            CreateLineQuad(linesRoot.transform, "Touchline_Right", new Vector3(halfW, 0.01f, 0f), new Vector3(lineWidth, 1f, PitchConstants.PitchLength));
            CreateLineQuad(linesRoot.transform, "GoalLine_Home", new Vector3(0f, 0.01f, -halfL), new Vector3(PitchConstants.PitchWidth, 1f, lineWidth));
            CreateLineQuad(linesRoot.transform, "GoalLine_Away", new Vector3(0f, 0.01f, halfL), new Vector3(PitchConstants.PitchWidth, 1f, lineWidth));

            // 2. Halfway Line
            CreateLineQuad(linesRoot.transform, "HalfwayLine", new Vector3(0f, 0.01f, 0f), new Vector3(PitchConstants.PitchWidth, 1f, lineWidth));

            // 3. Center Circle (9.15m regulation radius) & Center Kickoff Spot
            CreateCircleLine(linesRoot.transform, "CenterCircle", new Vector3(0f, 0.01f, 0f), 9.15f, 0f, 360f, 48);
            CreateSpotDisc(linesRoot.transform, "CenterSpot", new Vector3(0f, 0.012f, 0f), 0.24f);

            // 4. Penalty Boxes
            BuildBoxLines(linesRoot.transform, "HomePenaltyBox", -halfL, PitchConstants.PenaltyBoxLength, PitchConstants.PenaltyBoxWidth);
            BuildBoxLines(linesRoot.transform, "AwayPenaltyBox", halfL, -PitchConstants.PenaltyBoxLength, PitchConstants.PenaltyBoxWidth);

            // 5. Six-Yard Goal Boxes
            BuildBoxLines(linesRoot.transform, "HomeSixYardBox", -halfL, PitchConstants.SixYardBoxLength, PitchConstants.SixYardBoxWidth);
            BuildBoxLines(linesRoot.transform, "AwaySixYardBox", halfL, -PitchConstants.SixYardBoxLength, PitchConstants.SixYardBoxWidth);

            // 6. Penalty Spots (11m / 12 yards from goal lines)
            float homePenZ = -halfL + 11.0f;
            float awayPenZ = halfL - 11.0f;
            CreateSpotDisc(linesRoot.transform, "HomePenaltySpot", new Vector3(0f, 0.012f, homePenZ), 0.22f);
            CreateSpotDisc(linesRoot.transform, "AwayPenaltySpot", new Vector3(0f, 0.012f, awayPenZ), 0.22f);

            // 7. Penalty D-Arcs (9.15m radius curving outside penalty box)
            CreateCircleLine(linesRoot.transform, "HomePenaltyArc", new Vector3(0f, 0.01f, homePenZ), 9.15f, -53f, 106f, 20);
            CreateCircleLine(linesRoot.transform, "AwayPenaltyArc", new Vector3(0f, 0.01f, awayPenZ), 9.15f, 127f, 106f, 20);

            // 8. Corner Quadrant Arcs (1m radius quarter-circles at all 4 corners)
            CreateCircleLine(linesRoot.transform, "CornerArc_SW", new Vector3(-halfW, 0.01f, -halfL), 1.0f, 0f, 90f, 12);
            CreateCircleLine(linesRoot.transform, "CornerArc_SE", new Vector3(halfW, 0.01f, -halfL), 1.0f, 270f, 90f, 12);
            CreateCircleLine(linesRoot.transform, "CornerArc_NW", new Vector3(-halfW, 0.01f, halfL), 1.0f, 90f, 90f, 12);
            CreateCircleLine(linesRoot.transform, "CornerArc_NE", new Vector3(halfW, 0.01f, halfL), 1.0f, 180f, 90f, 12);
        }

        private void BuildBoxLines(Transform parent, string boxName, float goalLineZ, float lengthDirection, float boxWidth)
        {
            GameObject boxObj = new GameObject(boxName);
            boxObj.transform.SetParent(parent, false);

            float halfW = boxWidth * 0.5f;
            float farZ = goalLineZ + lengthDirection;

            // Front edge line
            CreateLineQuad(boxObj.transform, "FrontLine", new Vector3(0f, 0.01f, farZ), new Vector3(boxWidth, 1f, lineWidth));
            // Left edge line
            CreateLineQuad(boxObj.transform, "LeftLine", new Vector3(-halfW, 0.01f, goalLineZ + lengthDirection * 0.5f), new Vector3(lineWidth, 1f, Mathf.Abs(lengthDirection)));
            // Right edge line
            CreateLineQuad(boxObj.transform, "RightLine", new Vector3(halfW, 0.01f, goalLineZ + lengthDirection * 0.5f), new Vector3(lineWidth, 1f, Mathf.Abs(lengthDirection)));
        }

        private void CreateLineQuad(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = name;
            line.transform.SetParent(parent, false);
            line.transform.position = pos;
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.transform.localScale = scale;

            // Remove collider from visual line
            DestroyImmediate(line.GetComponent<Collider>());

            var mr = line.GetComponent<MeshRenderer>();
            if (lineMaterial != null) mr.sharedMaterial = lineMaterial;
        }

        private void CreateCircleLine(Transform parent, string name, Vector3 center, float radius, float startDeg, float sweepDeg, int segments = 32)
        {
            GameObject circleObj = new GameObject(name);
            circleObj.transform.SetParent(parent, false);
            circleObj.transform.position = center;

            float stepDeg = sweepDeg / segments;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (startDeg + i * stepDeg) * Mathf.Deg2Rad;
                float a2 = (startDeg + (i + 1) * stepDeg) * Mathf.Deg2Rad;

                Vector3 p1 = new Vector3(Mathf.Sin(a1) * radius, 0f, Mathf.Cos(a1) * radius);
                Vector3 p2 = new Vector3(Mathf.Sin(a2) * radius, 0f, Mathf.Cos(a2) * radius);

                Vector3 mid = (p1 + p2) * 0.5f;
                Vector3 dir = p2 - p1;
                float segLen = dir.magnitude;
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                seg.name = $"Seg_{i}";
                seg.transform.SetParent(circleObj.transform, false);
                seg.transform.localPosition = mid;
                seg.transform.rotation = Quaternion.Euler(90f, angle, 0f);
                seg.transform.localScale = new Vector3(lineWidth, segLen, 1f);
                DestroyImmediate(seg.GetComponent<Collider>());

                var mr = seg.GetComponent<MeshRenderer>();
                if (lineMaterial != null) mr.sharedMaterial = lineMaterial;
            }
        }

        private void CreateSpotDisc(Transform parent, string name, Vector3 pos, float radius = 0.22f)
        {
            GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spot.name = name;
            spot.transform.SetParent(parent, false);
            spot.transform.position = pos;
            spot.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
            DestroyImmediate(spot.GetComponent<Collider>());

            var mr = spot.GetComponent<MeshRenderer>();
            if (lineMaterial != null) mr.sharedMaterial = lineMaterial;
        }

        private void BuildGoalFrames()
        {
            GameObject goalsRoot = new GameObject("GoalFrames");
            goalsRoot.transform.SetParent(transform, false);

            CreateGoalPostStructure(goalsRoot.transform, "HomeGoal", -PitchConstants.HalfLength, 1);
            CreateGoalPostStructure(goalsRoot.transform, "AwayGoal", PitchConstants.HalfLength, -1);
        }

        private void CreateGoalPostStructure(Transform parent, string name, float goalLineZ, int facingDir)
        {
            GameObject goalObj = new GameObject(name);
            goalObj.transform.SetParent(parent, false);

            float halfW = PitchConstants.GoalWidth * 0.5f;
            float postRadius = 0.12f;
            float postHeight = PitchConstants.GoalHeight;

            // Left Post
            CreatePostCylinder(goalObj.transform, "LeftPost", new Vector3(-halfW, postHeight * 0.5f, goalLineZ), postRadius, postHeight);
            // Right Post
            CreatePostCylinder(goalObj.transform, "RightPost", new Vector3(halfW, postHeight * 0.5f, goalLineZ), postRadius, postHeight);

            // Crossbar
            GameObject crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crossbar.name = "Crossbar";
            crossbar.transform.SetParent(goalObj.transform, false);
            crossbar.transform.position = new Vector3(0f, postHeight, goalLineZ);
            crossbar.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            crossbar.transform.localScale = new Vector3(postRadius, PitchConstants.GoalWidth * 0.5f, postRadius);

            // Goal trigger collider for score detection
            GameObject triggerObj = new GameObject("GoalScoreTrigger");
            triggerObj.transform.SetParent(goalObj.transform, false);
            triggerObj.transform.position = new Vector3(0f, postHeight * 0.5f, goalLineZ - (facingDir * 0.5f));
            var boxCol = triggerObj.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(PitchConstants.GoalWidth, postHeight, 1.2f);

            var goalDetector = triggerObj.AddComponent<GoalDetector>();
            goalDetector.defendingTeamId = (facingDir == 1) ? 1 : 2;

            // 3D Realistic Goal Net Backing
            float netDepth = 2.4f;
            float rearZ = goalLineZ - (facingDir * netDepth);

            Material netMat = StadiumArchitect.CreateMaterial(new Color(0.92f, 0.92f, 0.92f, 0.45f), 0.1f);
            var netTex = ProceduralTextureFactory.CreateGoalNetTexture();
            if (netMat.HasProperty("_BaseMap")) netMat.SetTexture("_BaseMap", netTex);
            else if (netMat.HasProperty("_MainTex")) netMat.SetTexture("_MainTex", netTex);

            // Back Net Wall
            GameObject backNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backNet.name = "BackNet";
            backNet.transform.SetParent(goalObj.transform, false);
            backNet.transform.position = new Vector3(0f, postHeight * 0.5f, rearZ);
            backNet.transform.localScale = new Vector3(PitchConstants.GoalWidth, postHeight, 0.05f);
            DestroyImmediate(backNet.GetComponent<Collider>());
            backNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            // Top Net Roof (sloping from crossbar to rear stanchion)
            GameObject topNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topNet.name = "TopNet";
            topNet.transform.SetParent(goalObj.transform, false);
            topNet.transform.position = new Vector3(0f, postHeight * 0.95f, (goalLineZ + rearZ) * 0.5f);
            topNet.transform.localScale = new Vector3(PitchConstants.GoalWidth, 0.05f, netDepth);
            DestroyImmediate(topNet.GetComponent<Collider>());
            topNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            // Left & Right Net Side Walls
            GameObject leftNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftNet.name = "LeftNet";
            leftNet.transform.SetParent(goalObj.transform, false);
            leftNet.transform.position = new Vector3(-halfW, postHeight * 0.5f, (goalLineZ + rearZ) * 0.5f);
            leftNet.transform.localScale = new Vector3(0.05f, postHeight, netDepth);
            DestroyImmediate(leftNet.GetComponent<Collider>());
            leftNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            GameObject rightNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightNet.name = "RightNet";
            rightNet.transform.SetParent(goalObj.transform, false);
            rightNet.transform.position = new Vector3(halfW, postHeight * 0.5f, (goalLineZ + rearZ) * 0.5f);
            rightNet.transform.localScale = new Vector3(0.05f, postHeight, netDepth);
            DestroyImmediate(rightNet.GetComponent<Collider>());
            rightNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;
        }

        private void CreatePostCylinder(Transform parent, string name, Vector3 pos, float radius, float height)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = name;
            post.transform.SetParent(parent, false);
            post.transform.position = pos;
            post.transform.localScale = new Vector3(radius, height * 0.5f, radius);
        }
    }

    /// <summary>
    /// Trigger component mounted on the goal mouth to detect ball entry.
    /// </summary>
    public class GoalDetector : MonoBehaviour
    {
        public int defendingTeamId = 1;

        private void OnTriggerEnter(Collider other)
        {
            if (other.name.Contains("Ball") || (other.attachedRigidbody != null && other.attachedRigidbody.name.Contains("Ball")))
            {
                int scoringTeam = (defendingTeamId == 1) ? 2 : 1;
                GameEvents.TriggerGoalScored(scoringTeam, other.transform.position);
            }
        }
    }
}
