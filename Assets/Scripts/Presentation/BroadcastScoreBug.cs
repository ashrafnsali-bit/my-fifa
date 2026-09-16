using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Football.Core;
using Football.Engine;
using Football.Locomotion;

namespace Football.Presentation
{
    public class BroadcastScoreBug : MonoBehaviour
    {
        [Header("UI Text Displays (TextMeshPro)")]
        public TextMeshProUGUI homeTeamText;
        public TextMeshProUGUI awayTeamText;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI clockText;
        public TextMeshProUGUI stoppageTimeText;
        public TextMeshProUGUI matchBannerText;

        [Header("Gauges & Sliders")]
        public Slider powerBarSlider;
        public Slider staminaBarSlider;

        [Header("Colors")]
        public Image homeColorBadge;
        public Image awayColorBadge;

        private FootballPlayerActions humanActions;
        private PlayerRuntimeState humanRuntime;

        private void OnEnable()
        {
            GameEvents.OnScoreUpdated += HandleScoreUpdated;
            GameEvents.OnMatchStateChanged += HandleStateChanged;
            GameEvents.OnGoalScored += HandleGoalScored;
        }

        private void OnDisable()
        {
            GameEvents.OnScoreUpdated -= HandleScoreUpdated;
            GameEvents.OnMatchStateChanged -= HandleStateChanged;
            GameEvents.OnGoalScored -= HandleGoalScored;
        }

        [Header("Center Break Modal (Half-Time / Full-Time)")]
        public GameObject centerModalPanel;
        public TextMeshProUGUI centerModalTitleText;
        public TextMeshProUGUI centerModalScoreText;
        public TextMeshProUGUI centerModalPromptText;

        private void Start()
        {
            EnsureCenterModal();
            FindHumanPlayerReferences();
            UpdateScoreDisplay(0, 0);

            if (matchBannerText != null)
                matchBannerText.gameObject.SetActive(false);

            if (powerBarSlider != null)
                powerBarSlider.gameObject.SetActive(false);
        }

        private void EnsureCenterModal()
        {
            if (centerModalPanel != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            centerModalPanel = new GameObject("CenterBreakModal");
            centerModalPanel.transform.SetParent(canvas.transform, false);

            var rect = centerModalPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(580f, 270f);

            var img = centerModalPanel.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.10f, 0.94f);

            // Title Header (Gold)
            centerModalTitleText = CreateModalTMP(centerModalPanel.transform, "HALF TIME", new Vector2(0f, 80f), new Vector2(540f, 50f), 26, new Color(0.98f, 0.82f, 0.25f));

            // Big Score in Center
            centerModalScoreText = CreateModalTMP(centerModalPanel.transform, "ARG  0 - 0  FRA", new Vector2(0f, 10f), new Vector2(540f, 75f), 52, Color.white);

            // Prompt Subtitle (Cyan)
            centerModalPromptText = CreateModalTMP(centerModalPanel.transform, "PRESS ANY BUTTON TO START 2ND HALF\nاضغط على أي زر للمتابعة", new Vector2(0f, -75f), new Vector2(540f, 55f), 16, new Color(0.4f, 0.85f, 1.0f));

            centerModalPanel.SetActive(false);
        }

        private TextMeshProUGUI CreateModalTMP(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;

            try
            {
                var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (allFonts != null && allFonts.Length > 0) tmp.font = allFonts[0];
            }
            catch { }

            return tmp;
        }

        public void ShowCenterModal(string title, string prompt)
        {
            EnsureCenterModal();
            if (centerModalPanel == null) return;

            if (centerModalTitleText != null) centerModalTitleText.text = title;
            if (centerModalPromptText != null) centerModalPromptText.text = prompt;

            if (centerModalScoreText != null && MatchEngine.Instance != null)
            {
                string hName = MatchEngine.Instance.homeTeamName.Substring(0, Mathf.Min(3, MatchEngine.Instance.homeTeamName.Length)).ToUpper();
                string aName = MatchEngine.Instance.awayTeamName.Substring(0, Mathf.Min(3, MatchEngine.Instance.awayTeamName.Length)).ToUpper();
                centerModalScoreText.text = $"<color=#72B8FF>{hName}</color>  {MatchEngine.Instance.homeScore} - {MatchEngine.Instance.awayScore}  <color=#FF5555>{aName}</color>";
            }

            centerModalPanel.SetActive(true);
        }

        public void HideCenterModal()
        {
            if (centerModalPanel != null)
            {
                centerModalPanel.SetActive(false);
            }
        }

        private void FindHumanPlayerReferences()
        {
            var inputHandler = FindFirstObjectByType<FootballInputHandler>();
            if (inputHandler != null)
            {
                humanActions = inputHandler.GetComponent<FootballPlayerActions>();
                humanRuntime = inputHandler.GetComponent<PlayerRuntimeState>();
            }
        }

        private void Update()
        {
            UpdateClockDisplay();
            UpdatePlayerGauges();

            // Prompt breathing pulse animation while break modal is active
            if (centerModalPanel != null && centerModalPanel.activeSelf && centerModalPromptText != null)
            {
                float alpha = 0.70f + Mathf.Sin(Time.time * 4.5f) * 0.30f;
                Color c = centerModalPromptText.color;
                c.a = alpha;
                centerModalPromptText.color = c;
            }
        }

        private void UpdateClockDisplay()
        {
            if (MatchEngine.Instance == null || clockText == null) return;

            float matchMins = MatchEngine.Instance.matchClockMinutes;
            int totalSeconds = Mathf.FloorToInt(matchMins * 60f);
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;

            clockText.text = $"{m:00}:{s:00}";

            // Stoppage time badge
            if (stoppageTimeText != null)
            {
                bool inStoppage = (MatchEngine.Instance.currentHalf == 1 && m >= 45) || (MatchEngine.Instance.currentHalf == 2 && m >= 90);
                stoppageTimeText.gameObject.SetActive(inStoppage);
                if (inStoppage)
                {
                    stoppageTimeText.text = $"+{MatchEngine.Instance.addedTimeMinutes}'";
                }
            }
        }

        private void UpdatePlayerGauges()
        {
            if (humanActions != null && powerBarSlider != null)
            {
                float power = humanActions.CurrentPowerRatio;
                if (power > 0.01f)
                {
                    powerBarSlider.gameObject.SetActive(true);
                    powerBarSlider.value = power;
                }
                else
                {
                    powerBarSlider.gameObject.SetActive(false);
                }
            }

            if (humanRuntime != null && staminaBarSlider != null)
            {
                staminaBarSlider.value = humanRuntime.currentStamina / PlayerRuntimeState.MaxStamina;
            }
        }

        private void HandleScoreUpdated(int home, int away)
        {
            UpdateScoreDisplay(home, away);
        }

        private void UpdateScoreDisplay(int home, int away)
        {
            if (scoreText != null)
            {
                scoreText.text = $"{home} - {away}";
            }

            if (MatchEngine.Instance != null)
            {
                if (homeTeamText != null) homeTeamText.text = MatchEngine.Instance.homeTeamName.Substring(0, Mathf.Min(3, MatchEngine.Instance.homeTeamName.Length)).ToUpper();
                if (awayTeamText != null) awayTeamText.text = MatchEngine.Instance.awayTeamName.Substring(0, Mathf.Min(3, MatchEngine.Instance.awayTeamName.Length)).ToUpper();
            }
        }

        private void HandleGoalScored(int teamId, Vector3 pos)
        {
            ShowBanner("GOAL!", 3.0f);
        }

        private void HandleStateChanged(MatchState state)
        {
            bool isHumanTeam = (Football.Engine.MatchEngine.Instance == null || Football.Engine.MatchEngine.Instance.CurrentSetPieceTeam == 1);
            switch (state)
            {
                case MatchState.KickOff:
                    HideCenterModal();
                    bool isHumanKickoff = Football.Engine.MatchEngine.Instance == null || Football.Engine.MatchEngine.Instance.CurrentKickoffTeam == 1;
                    ShowBanner(isHumanKickoff ? "PRESS SPACE OR J TO KICK OFF" : "OPPONENT KICK OFF", 999.0f);
                    break;
                case MatchState.ThrowIn:
                    HideCenterModal();
                    ShowBanner(isHumanTeam ? "THROW-IN (PRESS J TO PASS)" : "OPPONENT THROW-IN", 999.0f);
                    break;
                case MatchState.CornerKick:
                    HideCenterModal();
                    ShowBanner(isHumanTeam ? "CORNER KICK (PRESS J PASS / K CROSS)" : "OPPONENT CORNER KICK", 999.0f);
                    break;
                case MatchState.GoalKick:
                    HideCenterModal();
                    ShowBanner(isHumanTeam ? "GOAL KICK (PRESS J PASS / K LOB)" : "OPPONENT GOAL KICK", 999.0f);
                    break;
                case MatchState.FreeKick:
                    HideCenterModal();
                    ShowBanner(isHumanTeam ? "FREE KICK (PRESS J PASS / SPACE SHOOT)" : "OPPONENT FREE KICK", 999.0f);
                    break;
                case MatchState.PenaltyKick:
                    HideCenterModal();
                    ShowBanner(isHumanTeam ? "PENALTY KICK (PRESS SPACE TO SHOOT)" : "OPPONENT PENALTY", 999.0f);
                    break;
                case MatchState.InPlay:
                    HideCenterModal();
                    HideBanner();
                    break;
                case MatchState.HalfTime:
                    HideBanner();
                    ShowCenterModal("HALF TIME | نهاية الشوط الأول", "PRESS ANY BUTTON TO START 2ND HALF\nاضغط على أي زر للانتقال إلى الشوط الثاني");
                    break;
                case MatchState.FullTime:
                    HideBanner();
                    ShowCenterModal("FULL TIME | نهاية المباراة", "GAME OVER | انتهت المباراة\nPRESS ANY BUTTON TO EXIT / اضغط على أي زر لإنهاء اللعبة");
                    break;
            }
        }

        private void ShowBanner(string text, float duration)
        {
            if (matchBannerText == null) return;
            matchBannerText.text = text;
            matchBannerText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideBanner));
            Invoke(nameof(HideBanner), duration);
        }

        private void HideBanner()
        {
            if (matchBannerText != null)
                matchBannerText.gameObject.SetActive(false);
        }
    }
}
