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

            var turfNormal = ProceduralTextureFactory.CreateTurfNormalMap();
            Vector2 stripeTiling = new Vector2(8f, 2f);

            if (turfMaterialLight == null)
            {
                turfMaterialLight = StadiumArchitect.CreateMaterial(new Color(0.20f, 0.56f, 0.24f), 0.28f);
                var texLight = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.22f, 0.58f, 0.26f), new Color(0.18f, 0.50f, 0.22f));
                if (turfMaterialLight.HasProperty("_BaseMap")) { turfMaterialLight.SetTexture("_BaseMap", texLight); turfMaterialLight.SetTextureScale("_BaseMap", stripeTiling); }
                else if (turfMaterialLight.HasProperty("_MainTex")) { turfMaterialLight.SetTexture("_MainTex", texLight); turfMaterialLight.SetTextureScale("_MainTex", stripeTiling); }
            }
            if (turfMaterialLight.HasProperty("_BumpMap"))
            {
                turfMaterialLight.SetTexture("_BumpMap", turfNormal);
                turfMaterialLight.SetTextureScale("_BumpMap", stripeTiling);
                turfMaterialLight.EnableKeyword("_NORMALMAP");
            }

            if (turfMaterialDark == null)
            {
                turfMaterialDark = StadiumArchitect.CreateMaterial(new Color(0.15f, 0.46f, 0.18f), 0.28f);
                var texDark = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.17f, 0.48f, 0.20f), new Color(0.13f, 0.40f, 0.16f));
                if (turfMaterialDark.HasProperty("_BaseMap")) { turfMaterialDark.SetTexture("_BaseMap", texDark); turfMaterialDark.SetTextureScale("_BaseMap", stripeTiling); }
                else if (turfMaterialDark.HasProperty("_MainTex")) { turfMaterialDark.SetTexture("_MainTex", texDark); turfMaterialDark.SetTextureScale("_MainTex", stripeTiling); }
            }
            if (turfMaterialDark.HasProperty("_BumpMap"))
            {
                turfMaterialDark.SetTexture("_BumpMap", turfNormal);
                turfMaterialDark.SetTextureScale("_BumpMap", stripeTiling);
                turfMaterialDark.EnableKeyword("_NORMALMAP");
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
                DestroyImmediate(stripe.GetComponent<Collider>());

                var renderer = stripe.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = (i % 2 == 0) ? turfMaterialLight : turfMaterialDark;
            }

            // Single unified, continuous pitch ground collider with 0 internal edge bumps
            var pitchCol = turfRoot.AddComponent<BoxCollider>();
            pitchCol.center = new Vector3(0f, -0.05f, 0f);
            pitchCol.size = new Vector3(PitchConstants.PitchWidth + 16.0f, 0.1f, PitchConstants.PitchLength + 16.0f);
        }

        private void BuildPitchLines()
        {
            GameObject linesRoot = new GameObject("PitchLines");
            linesRoot.transform.SetParent(transform, false);

            float halfW = PitchConstants.HalfWidth;
            float halfL = PitchConstants.HalfLength;
            float lineY = 0.025f;

            // 1. Boundary Perimeter Lines
            // Touchlines run along Z: width is lineWidth along X, length is PitchLength along Z
            CreateLineQuad(linesRoot.transform, "Touchline_Left", new Vector3(-halfW, lineY, 0f), new Vector3(lineWidth, PitchConstants.PitchLength, 1f));
            CreateLineQuad(linesRoot.transform, "Touchline_Right", new Vector3(halfW, lineY, 0f), new Vector3(lineWidth, PitchConstants.PitchLength, 1f));

            // GoalLines run along X: length is PitchWidth along X, thickness is lineWidth along Z
            CreateLineQuad(linesRoot.transform, "GoalLine_Home", new Vector3(0f, lineY, -halfL), new Vector3(PitchConstants.PitchWidth, lineWidth, 1f));
            CreateLineQuad(linesRoot.transform, "GoalLine_Away", new Vector3(0f, lineY, halfL), new Vector3(PitchConstants.PitchWidth, lineWidth, 1f));

            // 2. Halfway Line runs along X: length is PitchWidth along X, thickness is lineWidth along Z
            CreateLineQuad(linesRoot.transform, "HalfwayLine", new Vector3(0f, lineY, 0f), new Vector3(PitchConstants.PitchWidth, lineWidth, 1f));

            // 3. Center Circle (9.15m regulation radius) & Center Kickoff Spot
            CreateCircleLine(linesRoot.transform, "CenterCircle", new Vector3(0f, lineY, 0f), 9.15f, 0f, 360f, 64);
            CreateSpotDisc(linesRoot.transform, "CenterSpot", new Vector3(0f, lineY + 0.002f, 0f), 0.28f);

            // 4. Penalty Boxes
            BuildBoxLines(linesRoot.transform, "HomePenaltyBox", -halfL, PitchConstants.PenaltyBoxLength, PitchConstants.PenaltyBoxWidth, lineY);
            BuildBoxLines(linesRoot.transform, "AwayPenaltyBox", halfL, -PitchConstants.PenaltyBoxLength, PitchConstants.PenaltyBoxWidth, lineY);

            // 5. Six-Yard Goal Boxes
            BuildBoxLines(linesRoot.transform, "HomeSixYardBox", -halfL, PitchConstants.SixYardBoxLength, PitchConstants.SixYardBoxWidth, lineY);
            BuildBoxLines(linesRoot.transform, "AwaySixYardBox", halfL, -PitchConstants.SixYardBoxLength, PitchConstants.SixYardBoxWidth, lineY);

            // 6. Penalty Spots (11m / 12 yards from goal lines)
            float homePenZ = -halfL + 11.0f;
            float awayPenZ = halfL - 11.0f;
            CreateSpotDisc(linesRoot.transform, "HomePenaltySpot", new Vector3(0f, lineY + 0.002f, homePenZ), 0.24f);
            CreateSpotDisc(linesRoot.transform, "AwayPenaltySpot", new Vector3(0f, lineY + 0.002f, awayPenZ), 0.24f);

            // 7. Penalty D-Arcs (9.15m radius curving outside penalty box)
            CreateCircleLine(linesRoot.transform, "HomePenaltyArc", new Vector3(0f, lineY, homePenZ), 9.15f, -53f, 106f, 24);
            CreateCircleLine(linesRoot.transform, "AwayPenaltyArc", new Vector3(0f, lineY, awayPenZ), 9.15f, 127f, 106f, 24);

            // 8. Corner Quadrant Arcs (1m radius quarter-circles at all 4 corners)
            CreateCircleLine(linesRoot.transform, "CornerArc_SW", new Vector3(-halfW, lineY, -halfL), 1.0f, 0f, 90f, 16);
            CreateCircleLine(linesRoot.transform, "CornerArc_SE", new Vector3(halfW, lineY, -halfL), 1.0f, 270f, 90f, 16);
            CreateCircleLine(linesRoot.transform, "CornerArc_NW", new Vector3(-halfW, lineY, halfL), 1.0f, 90f, 90f, 16);
            CreateCircleLine(linesRoot.transform, "CornerArc_NE", new Vector3(halfW, lineY, halfL), 1.0f, 180f, 90f, 16);
        }

        private void BuildBoxLines(Transform parent, string boxName, float goalLineZ, float lengthDirection, float boxWidth, float lineY)
        {
            GameObject boxObj = new GameObject(boxName);
            boxObj.transform.SetParent(parent, false);

            float halfW = boxWidth * 0.5f;
            float farZ = goalLineZ + lengthDirection;
            float sideLen = Mathf.Abs(lengthDirection);

            // Front edge line (runs along X): width along X is boxWidth, thickness along Z is lineWidth
            CreateLineQuad(boxObj.transform, "FrontLine", new Vector3(0f, lineY, farZ), new Vector3(boxWidth, lineWidth, 1f));
            // Left edge line (runs along Z): thickness along X is lineWidth, length along Z is sideLen
            CreateLineQuad(boxObj.transform, "LeftLine", new Vector3(-halfW, lineY, goalLineZ + lengthDirection * 0.5f), new Vector3(lineWidth, sideLen, 1f));
            // Right edge line (runs along Z): thickness along X is lineWidth, length along Z is sideLen
            CreateLineQuad(boxObj.transform, "RightLine", new Vector3(halfW, lineY, goalLineZ + lengthDirection * 0.5f), new Vector3(lineWidth, sideLen, 1f));
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

            // Back Net Wall (equipped with solid non-penetrating BoxCollider and absorbing PhysicMaterial)
            GameObject backNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backNet.name = "BackNet";
            backNet.transform.SetParent(goalObj.transform, false);
            backNet.transform.position = new Vector3(0f, postHeight * 0.5f, rearZ);
            backNet.transform.localScale = new Vector3(PitchConstants.GoalWidth, postHeight, 0.05f);
            var backCol = backNet.GetComponent<BoxCollider>();
            backCol.sharedMaterial = GetNetPhysicMaterial();
            backCol.size = new Vector3(PitchConstants.GoalWidth + 0.4f, postHeight + 0.3f, 0.40f);
            backCol.center = new Vector3(0f, 0f, -facingDir * 0.18f); // Extends outward behind visual mesh
            backNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            // Top Net Roof (sloping from crossbar to rear stanchion)
            GameObject topNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topNet.name = "TopNet";
            topNet.transform.SetParent(goalObj.transform, false);
            topNet.transform.position = new Vector3(0f, postHeight * 0.95f, (goalLineZ + rearZ) * 0.5f);
            topNet.transform.localScale = new Vector3(PitchConstants.GoalWidth, 0.05f, netDepth);
            var topCol = topNet.GetComponent<BoxCollider>();
            topCol.sharedMaterial = GetNetPhysicMaterial();
            topCol.size = new Vector3(PitchConstants.GoalWidth + 0.4f, 0.40f, netDepth);
            topCol.center = new Vector3(0f, 0.18f, 0f); // Extends upwards above visual mesh
            topNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            // Left & Right Net Side Walls
            GameObject leftNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftNet.name = "LeftNet";
            leftNet.transform.SetParent(goalObj.transform, false);
            leftNet.transform.position = new Vector3(-halfW, postHeight * 0.5f, (goalLineZ + rearZ) * 0.5f);
            leftNet.transform.localScale = new Vector3(0.05f, postHeight, netDepth);
            var leftCol = leftNet.GetComponent<BoxCollider>();
            leftCol.sharedMaterial = GetNetPhysicMaterial();
            leftCol.size = new Vector3(0.40f, postHeight + 0.3f, netDepth);
            leftCol.center = new Vector3(-0.18f, 0f, 0f); // Extends outwards to left
            leftNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            GameObject rightNet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightNet.name = "RightNet";
            rightNet.transform.SetParent(goalObj.transform, false);
            rightNet.transform.position = new Vector3(halfW, postHeight * 0.5f, (goalLineZ + rearZ) * 0.5f);
            rightNet.transform.localScale = new Vector3(0.05f, postHeight, netDepth);
            var rightCol = rightNet.GetComponent<BoxCollider>();
            rightCol.sharedMaterial = GetNetPhysicMaterial();
            rightCol.size = new Vector3(0.40f, postHeight + 0.3f, netDepth);
            rightCol.center = new Vector3(0.18f, 0f, 0f); // Extends outwards to right
            rightNet.GetComponent<MeshRenderer>().sharedMaterial = netMat;

            // Goal Net Catchment & Energy Absorption Chamber (absorbs ball momentum & guarantees zero penetration)
            GameObject catchmentObj = new GameObject("GoalNetCatchment");
            catchmentObj.transform.SetParent(goalObj.transform, false);
            catchmentObj.transform.position = new Vector3(0f, postHeight * 0.5f, (goalLineZ + rearZ) * 0.5f);
            var catchCol = catchmentObj.AddComponent<BoxCollider>();
            catchCol.isTrigger = true;
            catchCol.size = new Vector3(PitchConstants.GoalWidth + 0.3f, postHeight + 0.2f, netDepth + 0.3f);

            var netDampener = catchmentObj.AddComponent<GoalNetDampener>();
            netDampener.facingDir = facingDir;
            netDampener.goalLineZ = goalLineZ;
            netDampener.rearZ = rearZ;
            netDampener.halfWidth = halfW;
            netDampener.goalHeight = postHeight;
        }

        private static PhysicsMaterial s_NetPhysMat;
        public static PhysicsMaterial GetNetPhysicMaterial()
        {
            if (s_NetPhysMat == null)
            {
                s_NetPhysMat = new PhysicsMaterial("GoalNetAbsorber")
                {
                    bounciness = 0.01f,
                    dynamicFriction = 0.95f,
                    staticFriction = 0.95f,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                    frictionCombine = PhysicsMaterialCombine.Maximum
                };
            }
            return s_NetPhysMat;
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
        private float lastGoalTime = -10f;

        private void OnTriggerEnter(Collider other)
        {
            // Only detect goals when match is actively in play
            if (GameEvents.CurrentMatchState != MatchState.InPlay)
            {
                return;
            }

            // Cooldown prevents multiple triggers while ball bounces in the net
            if (Time.time - lastGoalTime < 4.0f)
            {
                return;
            }

            if (other.name.Contains("Ball") || (other.attachedRigidbody != null && other.attachedRigidbody.name.Contains("Ball")) || other.CompareTag("Ball"))
            {
                lastGoalTime = Time.time;
                int scoringTeam = (defendingTeamId == 1) ? 2 : 1;
                GameEvents.TriggerGoalScored(scoringTeam, other.transform.position);
            }
        }
    }

    /// <summary>
    /// Authentically absorbs the football's kinetic energy inside the goal net pocket,
    /// preventing the ball from penetrating through the mesh or ricocheting back out of the net,
    /// dropping it realistically onto the turf inside the goal.
    /// </summary>
    public class GoalNetDampener : MonoBehaviour
    {
        public int facingDir = 1; // 1 = Home (faces +Z), -1 = Away (faces -Z)
        public float goalLineZ;
        public float rearZ;
        public float halfWidth = 3.66f;
        public float goalHeight = 2.44f;

        private void OnTriggerEnter(Collider other)
        {
            if (GameEvents.CurrentMatchState != MatchState.InPlay) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || !IsBall(other, rb)) return;

            // 1. Initial Net Impact Absorption:
            // Flexible cord mesh absorbs majority of momentum immediately upon striking net pocket
            Vector3 v = rb.linearVelocity;
            v.x *= 0.30f;
            v.z *= 0.30f;
            v.y = Mathf.Min(v.y, 0.8f) * 0.4f;
            rb.linearVelocity = v;
            rb.angularVelocity *= 0.25f;

            // Trigger net ripple sound event
            GameEvents.TriggerGoalNetHit();
        }

        private void OnTriggerStay(Collider other)
        {
            if (GameEvents.CurrentMatchState != MatchState.InPlay && GameEvents.CurrentMatchState != MatchState.GoalScored) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || !IsBall(other, rb)) return;

            Vector3 v = rb.linearVelocity;
            Vector3 pos = rb.position;
            const float ballRadius = 0.11f;
            const float safetyMargin = 0.04f;

            // 1. Rapidly bleed residual kinetic velocity & settle softly to grass:
            v.x *= 0.80f;
            v.z *= 0.80f;
            if (v.y > 0.05f) v.y *= 0.4f;
            v.y -= 4.0f * Time.fixedDeltaTime; // gentle downward gravitational settling

            // 2. Strict Anti-Penetration Guard (Ball can NEVER cross the net mesh boundaries):
            // Rear net boundary:
            if (facingDir == 1) // Home goal: goal line is -52.5, rear is -54.9
            {
                float minZ = rearZ + ballRadius + safetyMargin;
                if (pos.z < minZ)
                {
                    pos.z = minZ;
                    if (v.z < 0f) v.z = 0f;
                }
            }
            else // Away goal: goal line is +52.5, rear is +54.9
            {
                float maxZ = rearZ - ballRadius - safetyMargin;
                if (pos.z > maxZ)
                {
                    pos.z = maxZ;
                    if (v.z > 0f) v.z = 0f;
                }
            }

            // Left & Right net side walls barrier:
            float maxSide = halfWidth - ballRadius - safetyMargin;
            if (Mathf.Abs(pos.x) > maxSide)
            {
                pos.x = Mathf.Sign(pos.x) * maxSide;
                if (Mathf.Sign(v.x) == Mathf.Sign(pos.x)) v.x = 0f;
            }

            // Top net roof barrier:
            float maxRoof = goalHeight - ballRadius - safetyMargin;
            if (pos.y > maxRoof)
            {
                pos.y = maxRoof;
                if (v.y > 0f) v.y = -0.3f;
            }

            // Prevent ball from bouncing back out of the net into the field:
            if (facingDir == 1)
            {
                if (pos.z > goalLineZ - 0.25f && v.z > 0.8f)
                {
                    v.z = 0.2f;
                }
            }
            else
            {
                if (pos.z < goalLineZ + 0.25f && v.z < -0.8f)
                {
                    v.z = -0.2f;
                }
            }

            rb.position = pos;
            rb.linearVelocity = v;
            rb.angularVelocity *= 0.70f;
        }

        private bool IsBall(Collider other, Rigidbody rb)
        {
            return other.CompareTag("Ball") || 
                   other.name.Contains("Ball") || 
                   (rb != null && rb.name.Contains("Ball"));
        }
    }
}
