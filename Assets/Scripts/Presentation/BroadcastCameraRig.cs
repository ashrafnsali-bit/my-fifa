using UnityEngine;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Presentation
{
    /// <summary>
    /// Iconic FIFA / EA Sports FC sideline broadcast camera rig.
    /// Positions the camera on the West sideline gantry, elevated and looking East,
    /// tracking play along the pitch (Z-axis) so pitch touchlines run strictly horizontally
    /// across the screen and the East grandstand with cheering fans majestically frames the top of the view.
    /// </summary>
    public class BroadcastCameraRig : MonoBehaviour
    {
        [Header("Broadcast Tele Perspective")]
        public float sidelineDistance = 33.0f; // Intimate sideline distance (close to the action)
        public float cameraHeight = 10.8f;     // Elevation above pitch
        public float smoothTime = 0.18f;

        [Header("Dynamic Zoom & Player Focus")]
        public float baseFieldOfView = 28f;    // Crisp close-up perspective
        public float maxFieldOfView = 38f;     // Zoom out on high-speed long balls
        public float speedZoomFactor = 0.24f;

        [System.Obsolete("Use sidelineDistance and cameraHeight")]
        public Vector3 cameraOffset
        {
            get => new Vector3(-sidelineDistance, cameraHeight, 0f);
            set
            {
                if (Mathf.Abs(value.x) > 10f) sidelineDistance = Mathf.Abs(value.x);
                if (value.y > 4f) cameraHeight = value.y;
            }
        }

        private Camera cam;
        private Vector3 currentVelocity;
        private Transform cachedClosestPlayer;
        private float searchTimer;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            // Safe defaults
            if (sidelineDistance < 15f) sidelineDistance = 33.0f;
            if (cameraHeight < 4f) cameraHeight = 10.8f;
            if (baseFieldOfView < 15f) baseFieldOfView = 28f;
        }

        private void OnValidate()
        {
            if (sidelineDistance < 15f) sidelineDistance = 33.0f;
            if (cameraHeight < 4f) cameraHeight = 10.8f;
            if (baseFieldOfView < 15f) baseFieldOfView = 28f;
        }

        private void LateUpdate()
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // Search for player closest to the ball
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
            {
                searchTimer = 0.06f;
                cachedClosestPlayer = FindPlayerClosestToBall(ball.transform.position);
            }

            Vector3 ballPos = ball.transform.position;

            // Target Z tracks play along the pitch length (Z axis)
            float targetZ = ballPos.z;
            if (cachedClosestPlayer != null)
            {
                // Weighted average centered between the active dribbler/closest player and the ball
                targetZ = Mathf.Lerp(cachedClosestPlayer.position.z, ballPos.z, 0.45f);
            }

            // Camera sits on the West sideline (negative X) and glides smoothly along Z
            // Matching Z directly keeps touchlines 100% strictly horizontal on screen
            Vector3 desiredCamPos = new Vector3(-sidelineDistance, cameraHeight, targetZ);
            transform.position = Vector3.SmoothDamp(transform.position, desiredCamPos, ref currentVelocity, smoothTime);

            // Look target: looks straight across (+X) at the ball and active player
            float focusX = Mathf.Clamp(ballPos.x * 0.45f, -24f, 24f);
            Vector3 focusPoint = new Vector3(focusX, 1.05f, targetZ);
            Quaternion desiredRot = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 14.0f * Time.deltaTime);

            // Dynamic FOV scaling: close-up for dribbles, slight expansion for long shots/crosses
            if (cam != null)
            {
                float ballSpeed = ball.Velocity.magnitude;
                float targetFOV = Mathf.Clamp(baseFieldOfView + ballSpeed * speedZoomFactor, baseFieldOfView, maxFieldOfView);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, 3.5f * Time.deltaTime);
            }
        }

        private Transform FindPlayerClosestToBall(Vector3 ballPos)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            Transform closest = null;
            float minD = float.MaxValue;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || players[i].isSentOff) continue;
                float d = Vector3.Distance(players[i].transform.position, ballPos);
                if (d < minD)
                {
                    minD = d;
                    closest = players[i].transform;
                }
            }

            return closest;
        }
    }
}

