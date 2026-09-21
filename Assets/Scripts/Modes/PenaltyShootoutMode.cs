using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;

namespace Football.Modes
{
    /// <summary>
    /// Fast-paced, high-stakes Penalty Shootout Mode (EA Sports FC / FIFA style):
    /// - 5 regular rounds per team (Team 1 vs Team 2) + Sudden Death if tied.
    /// - Dynamic Aim Reticle & Green-Time Precision Meter.
    /// - Goalkeeper anticipatory dive AI with reaction times.
    /// - Real-time penalty scorecard HUD and celebration presentation.
    /// - Toggle anytime with 'P' key or HUD Button.
    /// </summary>
    public class PenaltyShootoutMode : MonoBehaviour
    {
        public static PenaltyShootoutMode Instance { get; private set; }

        [Header("Shootout State")]
        public bool isPenaltyModeActive = false;
        public int currentRound = 1; // 1 to 5+
        public int currentKickingTeam = 1; // 1 = Team 1, 2 = Team 2
        public int team1Score = 0;
        public int team2Score = 0;

        public List<bool?> team1Results = new List<bool?>();
        public List<bool?> team2Results = new List<bool?>();

        [Header("Tuning & Physics")]
        public float shotPower = 26f;
        public float timingMeterSpeed = 2.4f;

        private bool isAiming = false;
        private bool isShotTaken = false;
        private float timingPhase = 0f;
        private Vector2 aimOffset = Vector2.zero; // X (-1 to 1), Y (0 to 1)
        private string winnerText = "";
        private bool isShootoutFinished = false;

        private PlayerRuntimeState currentKicker;
        private PlayerRuntimeState currentGK;
        private FootballBall ball;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            ball = FootballBall.Instance ?? FindFirstObjectByType<FootballBall>();
        }

        private void Update()
        {
            // Toggle Penalty Shootout Mode with 'P' key anytime
            var keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.pKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.P))
            {
                if (!isPenaltyModeActive)
                {
                    StartPenaltyShootout();
                }
                else
                {
                    ExitPenaltyShootout();
                }
            }

            if (!isPenaltyModeActive || isShootoutFinished) return;

            // Update timing meter oscillations
            timingPhase = Mathf.PingPong(Time.time * timingMeterSpeed, 1.0f);

            // Read aiming input (WASD / Arrows / Left Stick)
            Vector2 aimInput = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) aimInput.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) aimInput.x += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) aimInput.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) aimInput.y -= 1f;
            }
            if (aimInput == Vector2.zero)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) aimInput.x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) aimInput.x += 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) aimInput.y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) aimInput.y -= 1f;
            }

            aimOffset.x = Mathf.Clamp(aimOffset.x + aimInput.x * Time.deltaTime * 1.8f, -1f, 1f);
            aimOffset.y = Mathf.Clamp(aimOffset.y + aimInput.y * Time.deltaTime * 1.8f, 0f, 1f);

            // Shoot Trigger (Space / Enter / J / Gamepad South)
            bool shootTrigger = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame)) ||
                                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);

            if (shootTrigger && !isShotTaken)
            {
                ExecutePenaltyKick();
            }
        }

        public void StartPenaltyShootout()
        {
            isPenaltyModeActive = true;
            isShootoutFinished = false;
            winnerText = "";
            currentRound = 1;
            currentKickingTeam = 1;
            team1Score = 0;
            team2Score = 0;
            team1Results.Clear();
            team2Results.Clear();

            GameEvents.TriggerMatchStateChanged(MatchState.PenaltyShootout);
            SetupCurrentPenaltyKick();
        }

        public void ExitPenaltyShootout()
        {
            isPenaltyModeActive = false;
            GameEvents.TriggerMatchStateChanged(MatchState.InPlay);
        }

        private void SetupCurrentPenaltyKick()
        {
            isShotTaken = false;
            aimOffset = new Vector2(0f, 0.4f);

            ball = FootballBall.Instance ?? FindFirstObjectByType<FootballBall>();
            if (ball == null) return;

            // Target goal is always defending goal for opponent
            int targetGoalTeam = (currentKickingTeam == 1) ? 2 : 1;
            Vector3 goalCenter = PitchConstants.GetDefendingGoalCenter(targetGoalTeam);

            // Penalty spot is 11m (or scaled 9.5m) in front of defending goal
            Vector3 penaltySpot = goalCenter + (targetGoalTeam == 1 ? Vector3.forward : Vector3.back) * 9.5f;
            penaltySpot.y = 0.12f;

            // Position ball at penalty spot
            ball.transform.position = penaltySpot;
            ball.BallRigidbody.linearVelocity = Vector3.zero;
            ball.BallRigidbody.angularVelocity = Vector3.zero;

            // Find Kicker from active team and Goalkeeper from defending team
            var allPlayers = FindObjectsByType<PlayerRuntimeState>(FindObjectsSortMode.None);
            currentKicker = null;
            currentGK = null;

            foreach (var p in allPlayers)
            {
                if (p.teamId == currentKickingTeam && (p.attributes == null || p.attributes.position != PlayerPosition.GK))
                {
                    if (currentKicker == null) currentKicker = p;
                }
                if (p.teamId == targetGoalTeam && (p.attributes != null && p.attributes.position == PlayerPosition.GK))
                {
                    currentGK = p;
                }
            }

            // Position Kicker 2.5m behind ball
            if (currentKicker != null)
            {
                Vector3 kickerPos = penaltySpot + (targetGoalTeam == 1 ? Vector3.back : Vector3.forward) * 2.2f;
                currentKicker.transform.position = kickerPos;
                currentKicker.transform.rotation = Quaternion.LookRotation(goalCenter - kickerPos);
                var loc = currentKicker.GetComponent<FootballPlayerLocomotion>();
                if (loc != null) loc.ResetPosition(kickerPos);
            }

            // Position Goalkeeper directly on goal line center
            if (currentGK != null)
            {
                Vector3 gkPos = goalCenter;
                gkPos.y = 0f;
                currentGK.transform.position = gkPos;
                currentGK.transform.rotation = Quaternion.LookRotation(penaltySpot - goalCenter);
                var loc = currentGK.GetComponent<FootballPlayerLocomotion>();
                if (loc != null) loc.ResetPosition(gkPos);
            }

            // Focus Camera on Penalty Action
            GameEvents.TriggerCameraSnapRequested();
        }

        private void ExecutePenaltyKick()
        {
            if (isShotTaken || ball == null) return;
            isShotTaken = true;

            int targetGoalTeam = (currentKickingTeam == 1) ? 2 : 1;
            Vector3 goalCenter = PitchConstants.GetDefendingGoalCenter(targetGoalTeam);

            // Precision timing calculation (Green time = middle 0.40 to 0.60)
            float timingAccuracy = 1.0f - Mathf.Abs(timingPhase - 0.5f) * 2f;
            bool isGreenTime = timingAccuracy > 0.75f;

            // Calculate exact strike trajectory
            float targetX = aimOffset.x * 3.4f; // Post width spread
            float targetY = Mathf.Lerp(0.3f, 2.2f, aimOffset.y); // Goal height
            Vector3 targetPoint = goalCenter + new Vector3(targetX, targetY, 0f);

            // Inaccuracy deviation if timing was mistimed
            if (!isGreenTime)
            {
                float errorSpread = (1f - timingAccuracy) * 1.5f;
                targetPoint += new Vector3(Random.Range(-errorSpread, errorSpread), Random.Range(-errorSpread * 0.5f, errorSpread), 0f);
            }

            Vector3 kickDir = (targetPoint - ball.transform.position).normalized;
            float finalSpeed = shotPower * (isGreenTime ? 1.15f : 0.95f);

            // Animate kicker
            if (currentKicker != null)
            {
                var anim = currentKicker.GetComponent<ProceduralRunnerAnimator>();
                if (anim != null) anim.TriggerKickAnimation(true);
            }

            // Trigger Goalkeeper Dive AI
            StartCoroutine(GoalkeeperDiveRoutine(targetX, targetY));

            // Fire Ball Physics
            ball.Kick(kickDir * finalSpeed, Vector3.up * (aimOffset.x * 8f), currentKicker != null ? currentKicker.jerseyNumber : 10, currentKickingTeam);

            // Evaluate penalty outcome after 1.8 seconds
            StartCoroutine(EvaluatePenaltyRoutine(targetGoalTeam));
        }

        private IEnumerator GoalkeeperDiveRoutine(float targetX, float targetY)
        {
            yield return new WaitForSeconds(0.12f); // GK reaction delay
            if (currentGK == null) yield break;

            var anim = currentGK.GetComponent<ProceduralRunnerAnimator>();
            bool diveRight = (currentKickingTeam == 1) ? (targetX < 0f) : (targetX > 0f);
            bool isHigh = targetY > 1.2f;

            // 70% chance GK guesses direction correctly
            if (Random.value < 0.70f)
            {
                if (anim != null) anim.TriggerGoalkeeperDive(diveRight, isHigh);
                var rb = currentGK.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = (diveRight ? currentGK.transform.right : -currentGK.transform.right) * 4.8f + (isHigh ? Vector3.up * 2.2f : Vector3.zero);
                }
            }
            else
            {
                // Wrong guess dive
                if (anim != null) anim.TriggerGoalkeeperDive(!diveRight, false);
            }
        }

        private IEnumerator EvaluatePenaltyRoutine(int defendingTeamId)
        {
            yield return new WaitForSeconds(1.8f);

            // Check if goal was scored
            Vector3 goalCenter = PitchConstants.GetDefendingGoalCenter(defendingTeamId);
            bool isGoal = ball != null && Mathf.Abs(ball.transform.position.z - goalCenter.z) < 1.8f &&
                          Mathf.Abs(ball.transform.position.x) < 3.8f && ball.transform.position.y < 2.6f;

            if (isGoal)
            {
                if (currentKickingTeam == 1) team1Score++;
                else team2Score++;
                GameEvents.TriggerGoalScored(currentKickingTeam, ball != null ? ball.transform.position : goalCenter);
            }
            else
            {
                GameEvents.TriggerGoalkeeperSave(defendingTeamId);
            }

            // Record scorecard
            if (currentKickingTeam == 1) team1Results.Add(isGoal);
            else team2Results.Add(isGoal);

            // Check for Shootout Winner after rounds
            CheckShootoutWinner();

            yield return new WaitForSeconds(2.0f);

            if (!isShootoutFinished)
            {
                // Advance turn
                if (currentKickingTeam == 1)
                {
                    currentKickingTeam = 2;
                }
                else
                {
                    currentKickingTeam = 1;
                    currentRound++;
                }
                SetupCurrentPenaltyKick();
            }
        }

        private void CheckShootoutWinner()
        {
            int r1 = team1Results.Count;
            int r2 = team2Results.Count;

            // In standard 5 rounds: check if mathematically impossible for trailing team to catch up
            if (r1 == r2 && r1 >= 5)
            {
                if (team1Score > team2Score)
                {
                    isShootoutFinished = true;
                    winnerText = "TEAM 1 WINS THE PENALTY SHOOTOUT!";
                }
                else if (team2Score > team1Score)
                {
                    isShootoutFinished = true;
                    winnerText = "TEAM 2 WINS THE PENALTY SHOOTOUT!";
                }
            }
            else if (r1 >= 3 && r2 >= 3 && r1 == r2)
            {
                int remaining1 = 5 - r1;
                int remaining2 = 5 - r2;
                if (team1Score > team2Score + remaining2)
                {
                    isShootoutFinished = true;
                    winnerText = "TEAM 1 WINS THE PENALTY SHOOTOUT!";
                }
                else if (team2Score > team1Score + remaining1)
                {
                    isShootoutFinished = true;
                    winnerText = "TEAM 2 WINS THE PENALTY SHOOTOUT!";
                }
            }
        }

        private void OnGUI()
        {
            // On-screen toggle button
            GUI.skin.button.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            if (!isPenaltyModeActive)
            {
                if (GUI.Button(new Rect(Screen.width - 240, 16, 225, 36), "🎯 Penalty Shootout (P)"))
                {
                    StartPenaltyShootout();
                }
                return;
            }

            // Penalty HUD Overlay
            float boxW = 440;
            float boxH = 160;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = 18;

            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

            GUILayout.BeginArea(new Rect(boxX + 12, boxY + 8, boxW - 24, boxH - 16));
            GUILayout.Label("<color=#00ff88><b>⚽ FIFA 26 PENALTY SHOOTOUT</b></color>", new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, alignment = TextAnchor.MiddleCenter });

            // Scoreboard
            string t1Dots = GetResultDots(team1Results);
            string t2Dots = GetResultDots(team2Results);

            GUILayout.Label($"<b>TEAM 1:</b> {team1Score}  [{t1Dots}]", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"<b>TEAM 2:</b> {team2Score}  [{t2Dots}]", new GUIStyle(GUI.skin.label) { richText = true });

            if (isShootoutFinished)
            {
                GUILayout.Label($"<color=#ffff00><b>🏆 {winnerText}</b></color>", new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("🔄 Play Again", GUILayout.Height(30)))
                {
                    StartPenaltyShootout();
                }
                if (GUILayout.Button("⬅️ Return to Match", GUILayout.Height(24)))
                {
                    ExitPenaltyShootout();
                }
            }
            else
            {
                GUILayout.Label($"<b>Current Kicker:</b> Team {currentKickingTeam} (Round {currentRound}) | <b>Space/Enter:</b> Strike");
                
                // Timing Meter Bar
                float meterW = boxW - 36;
                Rect meterRect = GUILayoutUtility.GetRect(meterW, 14);
                GUI.Box(meterRect, "");
                float indicatorX = meterRect.x + timingPhase * (meterRect.width - 8);
                bool isGreen = Mathf.Abs(timingPhase - 0.5f) < 0.12f;
                Color meterCol = isGreen ? Color.green : Color.red;
                GUI.color = meterCol;
                GUI.DrawTexture(new Rect(indicatorX, meterRect.y, 8, 14), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
        }

        private string GetResultDots(List<bool?> results)
        {
            string s = "";
            for (int i = 0; i < 5; i++)
            {
                if (i < results.Count)
                {
                    s += (results[i] == true) ? "<color=#00ff88>●</color> " : "<color=#ff3333>●</color> ";
                }
                else
                {
                    s += "<color=#777777>○</color> ";
                }
            }
            return s;
        }
    }
}
