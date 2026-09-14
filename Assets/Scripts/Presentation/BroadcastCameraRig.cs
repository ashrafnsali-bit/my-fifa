using UnityEngine;
using Football.Core;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Presentation
{
    /// <summary>
    /// Broadcast Camera Rig for FIFA World Cup match presentation.
    /// Positions the camera close to the ball for intense, immersive match action
    /// while locking the ball directly in the center of the viewport at all times.
    /// </summary>
    public class BroadcastCameraRig : MonoBehaviour
    {
        [Header("Ball Centering")]
        [Tooltip("When enabled, the camera's optical center points directly at the ball at all times")]
        public bool lockBallInCenter = true;

        [Header("Close-Action Proximity & Perspective")]
        [Tooltip("Horizontal distance from camera to the ball (lower = closer to the ball)")]
        public float distanceToBall = 10.5f;

        [Tooltip("Elevation of the camera above the pitch")]
        public float cameraHeight = 4.8f;

        [Tooltip("Smooth transition time for position tracking")]
        public float smoothTime = 0.05f;

        [Header("Dynamic Field of View")]
        [Tooltip("Base field of view (lower = tighter closer zoom)")]
        public float baseFieldOfView = 24f;
        public float maxFieldOfView = 29f;
        public float speedZoomFactor = 0.12f;

        [Header("Interactive Zoom Controls")]
        [Tooltip("Enable mouse scrollwheel and keys (+/-, [/], 8/9/0) to zoom closer or further in real-time")]
        public bool enableInteractiveZoom = true;
        public float minDistance = 5.0f;
        public float maxDistance = 24.0f;

        public float sidelineDistance
        {
            get => distanceToBall;
            set => distanceToBall = value;
        }

        [System.Obsolete("Use distanceToBall and cameraHeight")]
        public Vector3 cameraOffset
        {
            get => new Vector3(-distanceToBall, cameraHeight, 0f);
            set
            {
                if (Mathf.Abs(value.x) > 3f) distanceToBall = Mathf.Abs(value.x);
                if (value.y > 1.5f) cameraHeight = value.y;
            }
        }

        private Camera cam;
        private Vector3 currentVelocity;
        private Transform cachedBallTransform;
        private Rigidbody cachedBallRb;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (distanceToBall < 4f) distanceToBall = 10.5f;
            if (cameraHeight < 1.5f) cameraHeight = 4.8f;
            if (baseFieldOfView < 10f) baseFieldOfView = 24f;
        }

        private void Start()
        {
            FindBall();
            SnapToBall();
        }

        [Header("Cinematic Impact Shake")]
        private float cameraTrauma = 0f;
        private const float ShakeFrequency = 24f;

        public void AddTrauma(float amount)
        {
            cameraTrauma = Mathf.Clamp01(cameraTrauma + amount);
        }

        private void OnEnable()
        {
            FindBall();
            SnapToBall();
            Football.Core.GameEvents.OnWoodworkHit += HandleWoodworkShake;
            Football.Core.GameEvents.OnGoalScored += HandleGoalShake;
        }

        private void OnDisable()
        {
            Football.Core.GameEvents.OnWoodworkHit -= HandleWoodworkShake;
            Football.Core.GameEvents.OnGoalScored -= HandleGoalShake;
        }

        private void HandleWoodworkShake()
        {
            AddTrauma(0.55f);
        }

        private void HandleGoalShake(int teamId, Vector3 pos)
        {
            AddTrauma(0.65f);
        }

        private void OnValidate()
        {
            if (distanceToBall < 4f) distanceToBall = 10.5f;
            if (cameraHeight < 1.5f) cameraHeight = 4.8f;
            if (baseFieldOfView < 10f) baseFieldOfView = 24f;
        }

        private void Update()
        {
            if (!enableInteractiveZoom) return;

            // 1. Mouse ScrollWheel smooth zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.005f)
            {
                distanceToBall = Mathf.Clamp(distanceToBall - scroll * 6.0f, minDistance, maxDistance);
                cameraHeight = Mathf.Clamp(distanceToBall * 0.46f, 2.5f, 11.0f);
            }

            // 2. Keyboard keys (+ / - or [ / ])
            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.RightBracket))
            {
                distanceToBall = Mathf.Clamp(distanceToBall - 6.0f * Time.deltaTime, minDistance, maxDistance);
                cameraHeight = Mathf.Clamp(distanceToBall * 0.46f, 2.5f, 11.0f);
            }
            else if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.LeftBracket))
            {
                distanceToBall = Mathf.Clamp(distanceToBall + 6.0f * Time.deltaTime, minDistance, maxDistance);
                cameraHeight = Mathf.Clamp(distanceToBall * 0.46f, 2.5f, 11.0f);
            }

            // 3. Quick camera distance presets:
            // 9: Ultra-Close Action view
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
            {
                distanceToBall = 7.5f;
                cameraHeight = 3.6f;
            }
            // 0: Standard Close Dynamic view
            else if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                distanceToBall = 10.5f;
                cameraHeight = 4.8f;
            }
            // 8: Medium Broadcast view
            else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
            {
                distanceToBall = 15.0f;
                cameraHeight = 7.0f;
            }
        }

        [Header("Team Framing (At least 2 players per team)")]
        [Tooltip("Automatically adjusts camera distance so at least 2 players per team are always visible in the scene")]
        public bool ensureTwoPlayersPerTeamInView = true;
        public int minPlayersPerTeam = 2;
        public float framingPadding = 1.18f;

        private float currentFramingDistance = 11.5f;
        private float framingDistVelocity;

        public void SnapToBall()
        {
            Transform ball = GetBallTransform();
            if (ball == null) return;

            Vector3 ballPos = ball.position;
            float targetX = ballPos.x - distanceToBall;
            transform.position = new Vector3(targetX, cameraHeight, ballPos.z);

            Vector3 lookTarget = ballPos;
            Vector3 lookDir = lookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion directRot = Quaternion.LookRotation(lookDir, Vector3.up);
                Vector3 euler = directRot.eulerAngles;
                float yawDiff = Mathf.DeltaAngle(90f, euler.y);
                float clampedYaw = 90f + Mathf.Clamp(yawDiff, -6f, 6f);
                transform.rotation = Quaternion.Euler(euler.x, clampedYaw, 0f);
            }
            currentVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            Transform ball = GetBallTransform();
            if (ball == null) return;

            Vector3 ballPos = ball.position;
            // Safety clamp: ball should never be below ground
            ballPos.y = Mathf.Max(ballPos.y, 0f);

            // 1. Calculate how far back the camera needs to be to frame the action
            float activeDistance = distanceToBall;
            if (ensureTwoPlayersPerTeamInView)
            {
                float targetFramingDist = CalculateFramingDistanceForTeams(ballPos);
                // CRITICAL: Clamp framing distance so camera never gets pushed extreme far
                targetFramingDist = Mathf.Clamp(targetFramingDist, minDistance, maxDistance);
                currentFramingDistance = Mathf.SmoothDamp(currentFramingDistance, targetFramingDist, ref framingDistVelocity, 0.25f);
                activeDistance = Mathf.Clamp(Mathf.Max(distanceToBall, currentFramingDistance), minDistance, maxDistance);
            }
            else
            {
                currentFramingDistance = distanceToBall;
            }

            // CRITICAL: Camera must always be on the SIDELINE (negative X side) and above the pitch
            // Height scales with distance but is ALWAYS clamped to a safe minimum above the ground
            float heightRatio = (distanceToBall > 0.1f) ? (cameraHeight / distanceToBall) : 0.46f;
            float activeHeight = Mathf.Clamp(activeDistance * heightRatio, 2.5f, 22f);

            // Camera sits on the sideline (negative X), centred on ball's Z position
            float targetX = ballPos.x - activeDistance;
            float targetZ = ballPos.z;
            Vector3 desiredCamPos = new Vector3(targetX, activeHeight, targetZ);

            // Clamp camera so it never goes below 2.0m above the ground
            desiredCamPos.y = Mathf.Max(desiredCamPos.y, 2.0f);

            // Responsive position dampening so tracking is smooth and rapid
            transform.position = Vector3.SmoothDamp(transform.position, desiredCamPos, ref currentVelocity, smoothTime);

            // Safety: force camera above ground in case SmoothDamp overshoots
            Vector3 safePos = transform.position;
            safePos.y = Mathf.Max(safePos.y, 2.0f);
            transform.position = safePos;

            // 2. Aim camera directly at the ball:
            // Pitch tilts up/down so ball is 100% vertically centered at all times
            // Yaw is strictly locked to East (90° ± 6°) so touchlines stay strictly horizontal
            Vector3 lookTarget = ballPos;
            Vector3 lookDir = lookTarget - transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                Quaternion directRot = Quaternion.LookRotation(lookDir, Vector3.up);
                Vector3 euler = directRot.eulerAngles;
                float yawDiff = Mathf.DeltaAngle(90f, euler.y);
                float clampedYaw = 90f + Mathf.Clamp(yawDiff, -6f, 6f);

                transform.rotation = Quaternion.Euler(euler.x, clampedYaw, 0f);
            }

            // 3. Dynamic FOV scaling based on ball speed
            if (cam != null)
            {
                float ballSpeed = GetBallSpeed();
                float targetFOV = Mathf.Clamp(baseFieldOfView + ballSpeed * speedZoomFactor, baseFieldOfView, maxFieldOfView);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, 4.0f * Time.deltaTime);
            }

            // 4. Cinematic Trauma Shake application
            if (cameraTrauma > 0.001f)
            {
                float shakeIntensity = cameraTrauma * cameraTrauma * 0.32f;
                float t = Time.time * ShakeFrequency;
                Vector3 shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * shakeIntensity,
                    (Mathf.PerlinNoise(0f, t) * 2f - 1f) * shakeIntensity * 0.7f,
                    (Mathf.PerlinNoise(t, t) * 2f - 1f) * shakeIntensity
                );
                transform.position += shakeOffset;
                // Re-clamp after shake offset
                safePos = transform.position;
                safePos.y = Mathf.Max(safePos.y, 2.0f);
                transform.position = safePos;
                cameraTrauma = Mathf.MoveTowards(cameraTrauma, 0f, 1.8f * Time.deltaTime);
            }
        }


        private float CalculateFramingDistanceForTeams(Vector3 ballPos)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            if (players == null || players.Length < 4) return distanceToBall;

            // Find closest players for team 1 and team 2
            float t1Dist1 = float.MaxValue, t1Dist2 = float.MaxValue;
            Vector3 t1Pos1 = ballPos, t1Pos2 = ballPos;

            float t2Dist1 = float.MaxValue, t2Dist2 = float.MaxValue;
            Vector3 t2Pos1 = ballPos, t2Pos2 = ballPos;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p.isSentOff) continue;

                Vector3 pos = p.transform.position;
                float d = (pos - ballPos).sqrMagnitude;

                if (p.teamId == 1)
                {
                    if (d < t1Dist1)
                    {
                        t1Dist2 = t1Dist1;
                        t1Pos2 = t1Pos1;

                        t1Dist1 = d;
                        t1Pos1 = pos;
                    }
                    else if (d < t1Dist2)
                    {
                        t1Dist2 = d;
                        t1Pos2 = pos;
                    }
                }
                else if (p.teamId == 2)
                {
                    if (d < t2Dist1)
                    {
                        t2Dist2 = t2Dist1;
                        t2Pos2 = t2Pos1;

                        t2Dist1 = d;
                        t2Pos1 = pos;
                    }
                    else if (d < t2Dist2)
                    {
                        t2Dist2 = d;
                        t2Pos2 = pos;
                    }
                }
            }

            // Calculate required distance so that all 4 players are framed
            float fovRad = (cam != null ? cam.fieldOfView : baseFieldOfView) * 0.5f * Mathf.Deg2Rad;
            float tanFov = Mathf.Tan(fovRad);
            float aspect = (cam != null ? cam.aspect : (16f / 9f));
            float tanFovHoriz = tanFov * aspect;

            Vector3[] keyPositions = { t1Pos1, t1Pos2, t2Pos1, t2Pos2 };
            float maxRequiredDist = minDistance;

            for (int k = 0; k < keyPositions.Length; k++)
            {
                float dz = Mathf.Abs(keyPositions[k].z - ballPos.z) * framingPadding;
                float reqDistZ = (tanFovHoriz > 0.001f) ? (dz / tanFovHoriz) : minDistance;

                float dx = Mathf.Abs(keyPositions[k].x - ballPos.x) * framingPadding;
                float reqDistX = (tanFov > 0.001f) ? (dx / tanFov) : minDistance;

                maxRequiredDist = Mathf.Max(maxRequiredDist, reqDistZ, reqDistX);
            }

            return Mathf.Clamp(maxRequiredDist, minDistance, maxDistance);
        }

        private Transform GetBallTransform()
        {
            if (cachedBallTransform != null && cachedBallTransform.gameObject.activeInHierarchy)
            {
                return cachedBallTransform;
            }

            FindBall();
            return cachedBallTransform;
        }

        private void FindBall()
        {
            if (FootballBall.Instance != null)
            {
                cachedBallTransform = FootballBall.Instance.transform;
                cachedBallRb = FootballBall.Instance.BallRigidbody;
                return;
            }

            var fb = FindFirstObjectByType<FootballBall>();
            if (fb != null)
            {
                cachedBallTransform = fb.transform;
                cachedBallRb = fb.BallRigidbody;
                return;
            }

            var ballObj = GameObject.Find("MatchBall") ?? GameObject.Find("Ball") ?? GameObject.FindWithTag("Ball");
            if (ballObj != null)
            {
                cachedBallTransform = ballObj.transform;
                cachedBallRb = ballObj.GetComponent<Rigidbody>();
            }
        }

        private float GetBallSpeed()
        {
            if (cachedBallRb != null) return cachedBallRb.linearVelocity.magnitude;
            if (FootballBall.Instance != null) return FootballBall.Instance.Velocity.magnitude;
            return 0f;
        }
    }
}
