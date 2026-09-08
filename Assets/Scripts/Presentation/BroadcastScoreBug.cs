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

        private void Start()
        {
            FindHumanPlayerReferences();
            UpdateScoreDisplay(0, 0);

            if (matchBannerText != null)
                matchBannerText.gameObject.SetActive(false);

            if (powerBarSlider != null)
                powerBarSlider.gameObject.SetActive(false);
        }

        private void FindHumanPlayerReferences()
        {
            var inputHandler = FindObjectOfType<FootballInputHandler>();
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
            switch (state)
            {
                case MatchState.HalfTime:
                    ShowBanner("HALF TIME", 4.0f);
                    break;
                case MatchState.FullTime:
                    ShowBanner("FULL TIME", 5.0f);
                    break;
                case MatchState.PenaltyKick:
                    ShowBanner("PENALTY", 2.5f);
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
