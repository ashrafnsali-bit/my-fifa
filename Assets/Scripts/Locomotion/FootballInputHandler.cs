using UnityEngine;
using UnityEngine.InputSystem;
using Football.Core;
using Football.PhysicsEngine;

namespace Football.Locomotion
{
    [RequireComponent(typeof(FootballPlayerLocomotion), typeof(FootballPlayerActions))]
    public class FootballInputHandler : MonoBehaviour
    {
        public bool isHumanControlled = true;

        private FootballPlayerLocomotion locomotion;
        private FootballPlayerActions actions;
        private PlayerRuntimeState runtimeState;

        private void Awake()
        {
            locomotion = GetComponent<FootballPlayerLocomotion>();
            actions = GetComponent<FootballPlayerActions>();
            runtimeState = GetComponent<PlayerRuntimeState>();
        }

        private void Update()
        {
            if (!isHumanControlled || runtimeState.isSentOff) return;

            if (GameEvents.CurrentMatchState == MatchState.GoalScored || GameEvents.CurrentMatchState == MatchState.KickOff)
            {
                locomotion.SetMovementInput(Vector2.zero, false);
                return;
            }

            Vector2 moveInput = Vector2.zero;
            bool sprintHeld = false;

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            // 1. Unified robust input reading (New Input System + Legacy Input fallback)
            bool spaceDown = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Space);
            bool spaceHeld = (keyboard != null && keyboard.spaceKey.isPressed) || Input.GetKey(KeyCode.Space);
            bool spaceUp = (keyboard != null && keyboard.spaceKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Space);

            bool lDown = (keyboard != null && keyboard.lKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.L) || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            bool lHeld = (keyboard != null && keyboard.lKey.isPressed) || Input.GetKey(KeyCode.L) || (gamepad != null && gamepad.buttonEast.isPressed);
            bool lUp = (keyboard != null && keyboard.lKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.L) || (gamepad != null && gamepad.buttonEast.wasReleasedThisFrame);

            bool jDown = (keyboard != null && keyboard.jKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.J) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool jUp = (keyboard != null && keyboard.jKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.J) || (gamepad != null && gamepad.buttonSouth.wasReleasedThisFrame);

            bool kDown = (keyboard != null && keyboard.kKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.K) || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
            bool kUp = (keyboard != null && keyboard.kKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.K) || (gamepad != null && gamepad.buttonWest.wasReleasedThisFrame);

            bool iDown = (keyboard != null && keyboard.iKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.I) || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);
            bool iUp = (keyboard != null && keyboard.iKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.I) || (gamepad != null && gamepad.buttonNorth.wasReleasedThisFrame);

            // Read directional movement from Keyboard
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;

                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                {
                    sprintHeld = true;
                }
            }

            // Legacy Input fallback for movement
            if (moveInput == Vector2.zero)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveInput.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveInput.y -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveInput.x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveInput.x -= 1f;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) sprintHeld = true;
            }

            // Read Gamepad input
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    moveInput = stick;
                }

                if (gamepad.rightTrigger.isPressed || gamepad.rightShoulder.isPressed)
                {
                    sprintHeld = true;
                }
            }

            // Camera-relative steering so stick/WASD always matches what user sees on screen
            var cam = Camera.main;
            Vector3 worldMoveDir = Vector3.zero;
            if (cam != null && (Mathf.Abs(moveInput.x) > 0.01f || Mathf.Abs(moveInput.y) > 0.01f))
            {
                Vector3 camForward = cam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = cam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                worldMoveDir = camRight * moveInput.x + camForward * moveInput.y;
            }
            else
            {
                worldMoveDir = new Vector3(moveInput.x, 0f, moveInput.y);
            }

            locomotion.SetWorldMovementInput(worldMoveDir, sprintHeld);

            Vector3 aimDir = worldMoveDir.sqrMagnitude > 0.05f ? worldMoveDir.normalized : transform.forward;

            var ball = FootballBall.Instance ?? FindFirstObjectByType<FootballBall>();
            Vector3 toBall = ball != null ? (ball.transform.position - transform.position) : Vector3.zero;
            toBall.y = 0f;
            float distToBall = ball != null ? toBall.magnitude : 99f;
            bool isInPossession = runtimeState.hasBall || distToBall < 3.8f;

            bool kickDown = spaceDown || lDown;
            bool kickHeld = spaceHeld || lHeld;
            bool kickUp = spaceUp || lUp;

            if (isInPossession)
            {
                HandlePossessionInput(aimDir, keyboard, kickDown, kickHeld, kickUp, jDown, jUp, kDown, kUp, iDown, iUp);
            }
            else
            {
                HandleDefendingInput(aimDir, distToBall, kickDown, kickUp, kDown);
            }
        }

        private void HandlePossessionInput(Vector3 aimDir, Keyboard keyboard, bool kickDown, bool kickHeld, bool kickUp, bool passDown, bool passUp, bool lobDown, bool lobUp, bool throughDown, bool throughUp)
        {
            // --- Shooting / Powerful Kick (Spacebar / L Key) ---
            if (kickDown)
            {
                actions.StartShotCharge();
            }

            // Auto-release if fully charged
            if (kickHeld && actions.CurrentPowerRatio >= 0.99f)
            {
                ExecuteReleaseShot(aimDir, keyboard);
            }
            else if (kickUp)
            {
                ExecuteReleaseShot(aimDir, keyboard);
            }

            // --- Ground Passing (J Key) ---
            if (passDown) actions.StartPassCharge();
            if (passUp)
            {
                Transform target = FindTeammateInDirection(aimDir);
                actions.ReleasePass(PassType.Ground, aimDir, target);
                if (target != null)
                {
                    GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                }
            }

            // --- Lobbed Pass / Cross (K Key) ---
            if (lobDown) actions.StartPassCharge();
            if (lobUp)
            {
                Transform target = FindTeammateInDirection(aimDir);
                actions.ReleasePass(PassType.Lobbed, aimDir, target);
                if (target != null)
                {
                    GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                }
            }

            // --- Through Ball (I Key) ---
            if (throughDown) actions.StartPassCharge();
            if (throughUp)
            {
                Transform target = FindTeammateInDirection(aimDir);
                actions.ReleasePass(PassType.ThroughBall, aimDir, target);
                if (target != null)
                {
                    GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                }
            }
        }

        private void ExecuteReleaseShot(Vector3 aimDir, Keyboard keyboard)
        {
            ShotType shotType = ShotType.Standard;
            if ((keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rKey.isPressed)) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.R))
            {
                shotType = ShotType.Finesse;
            }
            else if ((keyboard != null && keyboard.leftCtrlKey.isPressed) || Input.GetKey(KeyCode.LeftControl))
            {
                shotType = ShotType.Chip;
            }
            else if (locomotion.IsSprinting)
            {
                shotType = ShotType.Power;
            }

            actions.ReleaseShot(shotType, aimDir);
        }

        private Transform FindTeammateInDirection(Vector3 aimDir)
        {
            var players = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            Transform bestTeammate = null;
            float bestScore = float.MinValue;

            Transform fallbackTeammate = null;
            float minFallbackDist = float.MaxValue;

            Vector3 refDir = aimDir.sqrMagnitude > 0.05f ? aimDir.normalized : transform.forward;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || p == runtimeState || p.teamId != runtimeState.teamId || p.isSentOff) continue;

                Vector3 toTeammate = p.transform.position - transform.position;
                toTeammate.y = 0;
                float dist = toTeammate.magnitude;
                if (dist < 1.2f || dist > 48f) continue;

                // Track closest teammate as robust fallback
                if (dist < minFallbackDist)
                {
                    minFallbackDist = dist;
                    fallbackTeammate = p.transform;
                }

                float dot = Vector3.Dot(toTeammate.normalized, refDir);
                // Broad cone: accepts passes up to 150 degrees
                if (dot > 0.10f)
                {
                    float score = (dot * 40f) - dist * 0.45f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTeammate = p.transform;
                    }
                }
            }
            return bestTeammate ?? fallbackTeammate;
        }

        private void HandleDefendingInput(Vector3 aimDir, float distToBall, bool kickDown, bool kickUp, bool slidePress)
        {
            // If player presses Spacebar/L near ball while defending, perform an instant powerful clearance kick/shot towards goal!
            if ((kickDown || kickUp) && distToBall <= 4.2f)
            {
                actions.ExecuteShot(ShotType.Power, aimDir, 0.85f);
                return;
            }

            // Standing Tackle if further away
            if (kickDown)
            {
                actions.ExecuteStandingTackle();
            }

            // Slide Tackle (Keycode K / Button X / Square)
            if (slidePress)
            {
                actions.ExecuteSlideTackle();
            }
        }
    }
}
