using UnityEngine;

namespace Football.Procedural
{
    /// <summary>
    /// Animates the UV texture offset of LED perimeter ribbons to create dynamic electronic stadium board motion.
    /// </summary>
    public class ProceduralLEDScroller : MonoBehaviour
    {
        [Header("Scroll Parameters")]
        public float scrollSpeed = 0.045f;
        public Vector2 scrollDirection = new Vector2(1f, 0f);

        private Renderer rend;
        private Material mat;
        private Vector2 currentOffset;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                mat = rend.material;
            }
        }

        private void Update()
        {
            if (mat == null) return;

            currentOffset += scrollDirection * (scrollSpeed * Time.deltaTime);
            currentOffset.x %= 1.0f;
            currentOffset.y %= 1.0f;

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTextureOffset("_BaseMap", currentOffset);
            }
            else if (mat.HasProperty("_MainTex"))
            {
                mat.SetTextureOffset("_MainTex", currentOffset);
            }
        }

        private void OnDestroy()
        {
            if (mat != null)
            {
                Destroy(mat);
            }
        }
    }
}
