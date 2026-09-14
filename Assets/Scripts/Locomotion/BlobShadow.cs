using UnityEngine;

namespace Football.Locomotion
{
    /// <summary>
    /// Projects a grounded blob shadow directly beneath the player's feet.
    /// Strictly matches the player's X and Z coordinates with 0 offset,
    /// keeping the Y coordinate fixed at ground level.
    /// </summary>
    public class BlobShadow : MonoBehaviour
    {
        [Tooltip("The player transform to follow")]
        public Transform playerTransform;

        [Tooltip("Fixed ground level (Y-axis)")]
        public float groundY = 0.015f;

        private void Awake()
        {
            if (playerTransform == null && transform.parent != null)
            {
                playerTransform = transform.parent;
            }
        }

        private void LateUpdate()
        {
            if (playerTransform == null) return;

            // Strictly matches the Player Transform on X and Z axes with 0 offset.
            // Y is kept strictly fixed at the ground level.
            transform.position = new Vector3(playerTransform.position.x, groundY, playerTransform.position.z);
        }
    }
}
