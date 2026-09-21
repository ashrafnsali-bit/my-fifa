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
        [Tooltip("1 = Player 1 (Team 1, WASD/Gamepad1), 2 = Player 2 (Team 2, Arrows/Keypad/Gamepad2)")]
        public int playerIndex = 1;

        private FootballPlayerLocomotion locomotion;
        private FootballPlayerActions actions;
        private PlayerRuntimeState runtimeState;

        private void Awake()
        {
            locomotion = GetComponent<FootballPlayerLocomotion>();
            actions = GetComponent<FootballPlayerActions>();
            runtimeState = GetComponent<PlayerRuntimeState>();
            if (runtimeState != null)
            {
                playerIndex = runtimeState.teamId == 2 ? 2 : 1;
            }
        }

        private void Update()
        {
            if (!isHumanControlled || runtimeState.isSentOff) return;

            if (GameEvents.CurrentMatchState == MatchState.GoalScored)
            {
                locomotion.SetMovementInput(Vector2.zero, false);
                return;
            }

            Vector2 moveInput = Vector2.zero;
            bool sprintHeld = false;

            var keyboard = Keyboard.current;
            var gamepads = Gamepad.all;
            Gamepad myGamepad = null;
            if (gamepads.Count >= playerIndex)
            {
                myGamepad = gamepads[playerIndex - 1];
            }
            else if (gamepads.Count > 0 && playerIndex == 1)
            {
                myGamepad = gamepads[0];
            }

            bool spaceDown = false, spaceHeld = false, spaceUp = false;
            bool lDown = false, lHeld = false, lUp = false;
            bool jDown = false, jUp = false;
            bool kDown = false, kUp = false;
            bool iDown = false, iUp = false;

            var mouse = Mouse.current;

            if (playerIndex == 1)
            {
                // === PLAYER 1 BINDINGS (Team 1: WASD / Left Hand + Mouse / Gamepad 1) ===
                bool enterDown = (keyboard != null && (keyboard.enterKey.wasPressedThisFrame)) || Input.GetKeyDown(KeyCode.Return);
                bool mouseDown = (mouse != null && mouse.leftButton.wasPressedThisFrame) || Input.GetMouseButtonDown(0);

                spaceDown = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Space) || enterDown || mouseDown;
                spaceHeld = (keyboard != null && keyboard.spaceKey.isPressed) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return);
                spaceUp = (keyboard != null && keyboard.spaceKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.Return) || Input.GetMouseButtonUp(0);

                lDown = (keyboard != null && keyboard.lKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.L) || (myGamepad != null && myGamepad.buttonEast.wasPressedThisFrame);
                lHeld = (keyboard != null && keyboard.lKey.isPressed) || Input.GetKey(KeyCode.L) || (myGamepad != null && myGamepad.buttonEast.isPressed);
                lUp = (keyboard != null && keyboard.lKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.L) || (myGamepad != null && myGamepad.buttonEast.wasReleasedThisFrame);

                jDown = (keyboard != null && keyboard.jKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.J) || (myGamepad != null && myGamepad.buttonSouth.wasPressedThisFrame) || enterDown || mouseDown;
                jUp = (keyboard != null && keyboard.jKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.J) || (myGamepad != null && myGamepad.buttonSouth.wasReleasedThisFrame);

                kDown = (keyboard != null && keyboard.kKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.K) || (myGamepad != null && myGamepad.buttonWest.wasPressedThisFrame);
                kUp = (keyboard != null && keyboard.kKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.K) || (myGamepad != null && myGamepad.buttonWest.wasReleasedThisFrame);

                iDown = (keyboard != null && keyboard.iKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.I) || (myGamepad != null && myGamepad.buttonNorth.wasPressedThisFrame);
                iUp = (keyboard != null && keyboard.iKey.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.I) || (myGamepad != null && myGamepad.buttonNorth.wasReleasedThisFrame);

                // WASD Directional Movement
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed) moveInput.y += 1f;
                    if (keyboard.sKey.isPressed) moveInput.y -= 1f;
                    if (keyboard.dKey.isPressed) moveInput.x += 1f;
                    if (keyboard.aKey.isPressed) moveInput.x -= 1f;
                    if (keyboard.leftShiftKey.isPressed) sprintHeld = true;
                }
                if (moveInput == Vector2.zero)
                {
                    if (Input.GetKey(KeyCode.W)) moveInput.y += 1f;
                    if (Input.GetKey(KeyCode.S)) moveInput.y -= 1f;
                    if (Input.GetKey(KeyCode.D)) moveInput.x += 1f;
                    if (Input.GetKey(KeyCode.A)) moveInput.x -= 1f;
                    if (Input.GetKey(KeyCode.LeftShift)) sprintHeld = true;
                }
            }
            else
            {
                // === PLAYER 2 BINDINGS (Team 2: Arrow Keys / Keypad / Gamepad 2) ===
                bool kp1Down = (keyboard != null && keyboard.numpad1Key.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Period) || (myGamepad != null && myGamepad.buttonSouth.wasPressedThisFrame);
                bool kp1Up = (keyboard != null && keyboard.numpad1Key.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Keypad1) || Input.GetKeyUp(KeyCode.Period) || (myGamepad != null && myGamepad.buttonSouth.wasReleasedThisFrame);

                bool kp3Down = (keyboard != null && keyboard.numpad3Key.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.RightControl) || (myGamepad != null && myGamepad.buttonEast.wasPressedThisFrame);
                bool kp3Held = (keyboard != null && keyboard.numpad3Key.isPressed) || Input.GetKey(KeyCode.Keypad3) || Input.GetKey(KeyCode.RightControl) || (myGamepad != null && myGamepad.buttonEast.isPressed);
                bool kp3Up = (keyboard != null && keyboard.numpad3Key.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Keypad3) || Input.GetKeyUp(KeyCode.RightControl) || (myGamepad != null && myGamepad.buttonEast.wasReleasedThisFrame);

                bool kp2Down = (keyboard != null && keyboard.numpad2Key.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Slash) || (myGamepad != null && myGamepad.buttonWest.wasPressedThisFrame);
                bool kp2Up = (keyboard != null && keyboard.numpad2Key.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Keypad2) || Input.GetKeyUp(KeyCode.Slash) || (myGamepad != null && myGamepad.buttonWest.wasReleasedThisFrame);

                bool kp5Down = (keyboard != null && keyboard.numpad5Key.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Quote) || (myGamepad != null && myGamepad.buttonNorth.wasPressedThisFrame);
                bool kp5Up = (keyboard != null && keyboard.numpad5Key.wasReleasedThisFrame) || Input.GetKeyUp(KeyCode.Keypad5) || Input.GetKeyUp(KeyCode.Quote) || (myGamepad != null && myGamepad.buttonNorth.wasReleasedThisFrame);

                spaceDown = kp3Down;
                spaceHeld = kp3Held;
                spaceUp = kp3Up;
                lDown = kp3Down;
                lHeld = kp3Held;
                lUp = kp3Up;
                jDown = kp1Down;
                jUp = kp1Up;
                kDown = kp2Down;
                kUp = kp2Up;
                iDown = kp5Down;
                iUp = kp5Up;

                // Arrow Keys Movement
                if (keyboard != null)
                {
                    if (keyboard.upArrowKey.isPressed) moveInput.y += 1f;
                    if (keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
                    if (keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
                    if (keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
                    if (keyboard.rightShiftKey.isPressed || keyboard.numpad0Key.isPressed) sprintHeld = true;
                }
                if (moveInput == Vector2.zero)
                {
                    if (Input.GetKey(KeyCode.UpArrow)) moveInput.y += 1f;
                    if (Input.GetKey(KeyCode.DownArrow)) moveInput.y -= 1f;
                    if (Input.GetKey(KeyCode.RightArrow)) moveInput.x += 1f;
                    if (Input.GetKey(KeyCode.LeftArrow)) moveInput.x -= 1f;
                    if (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.Keypad0)) sprintHeld = true;
                }
            }

            // Gamepad stick & trigger inputs
            if (myGamepad != null)
            {
                Vector2 stick = myGamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    moveInput = stick;
                }

                if (myGamepad.rightTrigger.isPressed || myGamepad.rightShoulder.isPressed)
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
            bool isRestartState = (GameEvents.CurrentMatchState == MatchState.KickOff ||
                                   GameEvents.CurrentMatchState == MatchState.ThrowIn ||
                                   GameEvents.CurrentMatchState == MatchState.CornerKick ||
                                   GameEvents.CurrentMatchState == MatchState.GoalKick ||
                                   GameEvents.CurrentMatchState == MatchState.FreeKick ||
                                   GameEvents.CurrentMatchState == MatchState.PenaltyKick);
            bool isInPossession = runtimeState.hasBall || distToBall < 4.5f || isRestartState;

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
            bool isRestartState = (GameEvents.CurrentMatchState == MatchState.KickOff ||
                                   GameEvents.CurrentMatchState == MatchState.ThrowIn ||
                                   GameEvents.CurrentMatchState == MatchState.CornerKick ||
                                   GameEvents.CurrentMatchState == MatchState.GoalKick ||
                                   GameEvents.CurrentMatchState == MatchState.FreeKick ||
                                   GameEvents.CurrentMatchState == MatchState.PenaltyKick);

            // Instant restart execution on any pass/kick key down or up
            if (isRestartState)
            {
                if (kickDown || kickUp)
                {
                    ExecuteReleaseShot(aimDir, keyboard);
                    return;
                }
                if (passDown || passUp)
                {
                    Transform target = FindTeammateInDirection(aimDir);
                    actions.ReleasePass(PassType.Ground, aimDir, target);
                    if (target != null) GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                    return;
                }
                if (lobDown || lobUp)
                {
                    Transform target = FindTeammateInDirection(aimDir);
                    actions.ReleasePass(PassType.Lobbed, aimDir, target);
                    if (target != null) GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                    return;
                }
                if (throughDown || throughUp)
                {
                    Transform target = FindTeammateInDirection(aimDir);
                    actions.ReleasePass(PassType.ThroughBall, aimDir, target);
                    if (target != null) GameEvents.TriggerPassInitiated(runtimeState.teamId, target);
                    return;
                }
            }

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
