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

            Vector2 moveInput = Vector2.zero;
            bool sprintHeld = false;

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            // 1. Read Keyboard input
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

            // 2. Read Gamepad input
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

            var ball = FootballBall.Instance;
            float distToBall = (ball != null) ? Vector3.Distance(transform.position, ball.transform.position) : 99f;
            bool isInPossession = runtimeState.hasBall || distToBall < 2.5f;

            if (isInPossession)
            {
                HandlePossessionInput(aimDir, keyboard, gamepad);
            }
            else
            {
                HandleDefendingInput(keyboard, gamepad);
            }
        }

        private void HandlePossessionInput(Vector3 aimDir, Keyboard keyboard, Gamepad gamepad)
        {
            bool shootDown = (keyboard != null && keyboard.lKey.wasPressedThisFrame) ||
                             (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            bool shootUp = (keyboard != null && keyboard.lKey.wasReleasedThisFrame) ||
                           (gamepad != null && gamepad.buttonEast.wasReleasedThisFrame);

            bool passDown = (keyboard != null && (keyboard.jKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool passUp = (keyboard != null && (keyboard.jKey.wasReleasedThisFrame || keyboard.spaceKey.wasReleasedThisFrame)) ||
                          (gamepad != null && gamepad.buttonSouth.wasReleasedThisFrame);

            bool lobDown = (keyboard != null && keyboard.kKey.wasPressedThisFrame) ||
                           (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
            bool lobUp = (keyboard != null && keyboard.kKey.wasReleasedThisFrame) ||
                         (gamepad != null && gamepad.buttonWest.wasReleasedThisFrame);

            bool throughDown = (keyboard != null && keyboard.iKey.wasPressedThisFrame) ||
                               (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);
            bool throughUp = (keyboard != null && keyboard.iKey.wasReleasedThisFrame) ||
                             (gamepad != null && gamepad.buttonNorth.wasReleasedThisFrame);

            // --- Shooting ---
            if (shootDown) actions.StartShotCharge();
            if (shootUp)
            {
                ShotType shotType = ShotType.Standard;
                if (keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rKey.isPressed)) shotType = ShotType.Finesse;
                else if (keyboard != null && keyboard.leftCtrlKey.isPressed) shotType = ShotType.Chip;
                else if (locomotion.IsSprinting) shotType = ShotType.Power;

                actions.ReleaseShot(shotType, aimDir);
            }

            // --- Ground Passing ---
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

            // --- Lobbed Pass / Cross ---
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

            // --- Through Ball ---
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

        private void HandleDefendingInput(Keyboard keyboard, Gamepad gamepad)
        {
            // Standing Tackle (Keycode L / Button B / Circle)
            bool tacklePress = (keyboard != null && keyboard.lKey.wasPressedThisFrame) ||
                               (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            if (tacklePress)
            {
                actions.ExecuteStandingTackle();
            }

            // Slide Tackle (Keycode K / Button X / Square)
            bool slidePress = (keyboard != null && keyboard.kKey.wasPressedThisFrame) ||
                              (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
            if (slidePress)
            {
                actions.ExecuteSlideTackle();
            }
        }
    }
}
