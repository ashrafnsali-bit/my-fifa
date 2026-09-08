using System.Collections.Generic;
using UnityEngine;
using Football.Core;

namespace Football.Audio
{
    public class FootballCommentarySystem : MonoBehaviour
    {
        public static FootballCommentarySystem Instance { get; private set; }

        [Header("Audio Output")]
        public AudioSource commentarySource;

        [Header("Commentary VO Banks")]
        public List<AudioClip> kickoffLines = new List<AudioClip>();
        public List<AudioClip> goalLines = new List<AudioClip>();
        public List<AudioClip> foulLines = new List<AudioClip>();
        public List<AudioClip> penaltyLines = new List<AudioClip>();
        public List<AudioClip> woodworkLines = new List<AudioClip>();
        public List<AudioClip> fulltimeLines = new List<AudioClip>();

        public string latestCommentarySubtitle { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (commentarySource == null)
            {
                commentarySource = gameObject.AddComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            GameEvents.OnMatchStateChanged += HandleMatchState;
            GameEvents.OnGoalScored += HandleGoal;
            GameEvents.OnFoulCalled += HandleFoul;
            GameEvents.OnWoodworkHit += HandleWoodwork;
        }

        private void OnDisable()
        {
            GameEvents.OnMatchStateChanged -= HandleMatchState;
            GameEvents.OnGoalScored -= HandleGoal;
            GameEvents.OnFoulCalled -= HandleFoul;
            GameEvents.OnWoodworkHit -= HandleWoodwork;
        }

        private void HandleMatchState(MatchState state)
        {
            switch (state)
            {
                case MatchState.KickOff:
                    Speak("And we're underway in this FIFA World Cup clash!", kickoffLines);
                    break;
                case MatchState.HalfTime:
                    Speak("The referee blows for half-time. Time for the managers to regroup.", null);
                    break;
                case MatchState.FullTime:
                    Speak("That is the final whistle! What an extraordinary match!", fulltimeLines);
                    break;
                case MatchState.PenaltyKick:
                    Speak("Penalty awarded! The ultimate test of nerve and precision.", penaltyLines);
                    break;
            }
        }

        private void HandleGoal(int teamId, Vector3 pos)
        {
            Speak($"GOOOOAAAL! What an incredible finish from Team {teamId}!", goalLines);
        }

        private void HandleFoul(int teamId, Vector3 pos, bool isPenalty)
        {
            if (!isPenalty)
            {
                Speak("The referee halts play. That was a reckless challenge.", foulLines);
            }
        }

        private void HandleWoodwork()
        {
            Speak("Off the post! Inches away from a sensational goal!", woodworkLines);
        }

        private void Speak(string subtitle, List<AudioClip> clips)
        {
            latestCommentarySubtitle = subtitle;
            Debug.Log($"[Commentary]: {subtitle}");

            if (clips != null && clips.Count > 0 && commentarySource != null)
            {
                var clip = clips[Random.Range(0, clips.Count)];
                if (clip != null) commentarySource.PlayOneShot(clip);
            }
        }
    }
}
