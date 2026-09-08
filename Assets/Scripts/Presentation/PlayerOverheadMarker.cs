using UnityEngine;
using TMPro;

namespace Football.Presentation
{
    /// <summary>
    /// Iconic EA Sports FC / FIFA style overhead player indicator.
    /// Hovers an inverted neon-green chevron above the controlled player's head
    /// with gentle hovering oscillation and a broadcast-quality player name/number tag.
    /// </summary>
    public class PlayerOverheadMarker : MonoBehaviour
    {
        [Header("Floating Offset")]
        public Vector3 hoverOffset = new Vector3(0f, 2.35f, 0f);
        public float bobAmplitude = 0.08f;
        public float bobFrequency = 3.5f;

        [Header("Visual Colors")]
        public Color neonColor = new Color(0.0f, 1.0f, 0.45f); // Vibrant EA FC Neon Mint
        public string playerName = "10 MESSI";

        private Transform targetPlayer;
        private GameObject chevronObj;
        private TextMeshPro nameTextMesh;
        private Camera mainCamera;

        public void Initialize(Transform player, string displayName, Color? markerColor = null)
        {
            targetPlayer = player;
            if (!string.IsNullOrEmpty(displayName)) playerName = displayName;
            if (markerColor.HasValue) neonColor = markerColor.Value;

            BuildMarkerVisuals();
        }

        public void SwitchTarget(Transform newTarget, string displayName = null)
        {
            targetPlayer = newTarget;
            if (!string.IsNullOrEmpty(displayName))
            {
                playerName = displayName;
                if (nameTextMesh != null) nameTextMesh.text = displayName;
            }
        }

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void BuildMarkerVisuals()
        {
            // 1. Inverted 3D Neon Chevron Triangle
            chevronObj = new GameObject("NeonChevron");
            chevronObj.transform.SetParent(transform, false);

            var meshFilter = chevronObj.AddComponent<MeshFilter>();
            var meshRenderer = chevronObj.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = CreateChevronMesh();

            // Unlit glowing neon material with HDR intensity for Bloom
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material markerMat = new Material(shader);
            markerMat.color = neonColor;
            if (markerMat.HasProperty("_BaseColor")) markerMat.SetColor("_BaseColor", neonColor);
            if (markerMat.HasProperty("_EmissionColor"))
            {
                markerMat.EnableKeyword("_EMISSION");
                markerMat.SetColor("_EmissionColor", neonColor * 2.5f);
            }
            meshRenderer.sharedMaterial = markerMat;

            // 2. Floating Name/Number Badge
            GameObject textObj = new GameObject("NameTag");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0.38f, 0f);

            nameTextMesh = textObj.AddComponent<TextMeshPro>();
            nameTextMesh.text = playerName;
            nameTextMesh.fontSize = 2.4f;
            nameTextMesh.alignment = TextAlignmentOptions.Center;
            nameTextMesh.color = Color.white;
            nameTextMesh.fontStyle = FontStyles.Bold;

            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null && fonts.Length > 0) nameTextMesh.font = fonts[0];
            }
            catch { }
        }

        private Mesh CreateChevronMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "InvertedChevron";

            // Inverted triangle pointing down at the player's head
            float w = 0.28f;
            float h = 0.32f;
            float thickness = 0.04f;

            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-w, h, -thickness * 0.5f),  // Top Left
                new Vector3(w, h, -thickness * 0.5f),   // Top Right
                new Vector3(0f, 0f, -thickness * 0.5f),  // Bottom Tip
                // Back face
                new Vector3(-w, h, thickness * 0.5f),
                new Vector3(w, h, thickness * 0.5f),
                new Vector3(0f, 0f, thickness * 0.5f)
            };

            int[] triangles = new int[]
            {
                // Front
                0, 1, 2,
                // Back
                5, 4, 3,
                // Top
                0, 3, 4,
                0, 4, 1,
                // Left slope
                2, 5, 3,
                2, 3, 0,
                // Right slope
                1, 4, 5,
                1, 5, 2
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void LateUpdate()
        {
            if (targetPlayer == null) return;
            if (mainCamera == null) mainCamera = Camera.main;

            // Follow player position with gentle bob
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            transform.position = targetPlayer.position + hoverOffset + new Vector3(0f, bob, 0f);

            // Always face camera
            if (mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position, Vector3.up);
            }
        }
    }
}
