using UnityEngine;
using Football.Core;
using Football.PhysicsEngine;

namespace Football.Audio
{
    public class CrowdAudioManager : MonoBehaviour
    {
        public static CrowdAudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        public AudioSource ambientMurmurSource;
        public AudioSource excitementSwellSource;
        public AudioSource oneShotStingerSource;
        public AudioSource foleySource;

        [Header("Clips")]
        public AudioClip goalRoarClip;
        public AudioClip nearMissGaspClip;
        public AudioClip foulJeerClip;
        public AudioClip whistleClip;
        public AudioClip kickBallClip;
        public AudioClip woodworkHitClip;
        public AudioClip netRustleClip;

        public AudioClip slideTackleClip;

        [Header("Excitement Curve Tuning")]
        [Range(0f, 1f)] public float currentExcitement = 0.1f;
        public float dangerZoneDistance = 28.0f; // Distance from goal where excitement begins rising

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            InitializeProceduralAudioClips();
            SetupAudioSources();
        }

        private void InitializeProceduralAudioClips()
        {
            if (kickBallClip == null) kickBallClip = ProceduralAudioFactory.CreateKickBallClip();
            if (whistleClip == null) whistleClip = ProceduralAudioFactory.CreateRefereeWhistleClip();
            if (woodworkHitClip == null) woodworkHitClip = ProceduralAudioFactory.CreateWoodworkHitClip();
            if (netRustleClip == null) netRustleClip = ProceduralAudioFactory.CreateNetRustleClip();
            if (goalRoarClip == null) goalRoarClip = ProceduralAudioFactory.CreateGoalRoarClip();
            if (nearMissGaspClip == null) nearMissGaspClip = ProceduralAudioFactory.CreateNearMissGaspClip();
            if (slideTackleClip == null) slideTackleClip = ProceduralAudioFactory.CreateSlideTackleClip();
            if (foulJeerClip == null) foulJeerClip = ProceduralAudioFactory.CreateNearMissGaspClip();
        }

        private void SetupAudioSources()
        {
            if (ambientMurmurSource == null) ambientMurmurSource = gameObject.AddComponent<AudioSource>();
            if (excitementSwellSource == null) excitementSwellSource = gameObject.AddComponent<AudioSource>();
            if (oneShotStingerSource == null) oneShotStingerSource = gameObject.AddComponent<AudioSource>();
            if (foleySource == null) foleySource = gameObject.AddComponent<AudioSource>();

            ambientMurmurSource.loop = true;
            ambientMurmurSource.volume = 0.45f;
            if (ambientMurmurSource.clip == null)
            {
                ambientMurmurSource.clip = ProceduralAudioFactory.CreateCrowdMurmurLoop();
                ambientMurmurSource.Play();
            }

            excitementSwellSource.loop = true;
            excitementSwellSource.volume = 0.0f;
            if (excitementSwellSource.clip == null)
            {
                excitementSwellSource.clip = ProceduralAudioFactory.CreateExcitementSwellLoop();
                excitementSwellSource.Play();
            }

            oneShotStingerSource.playOnAwake = false;
            foleySource.playOnAwake = false;
        }

        private void OnEnable()
        {
            GameEvents.OnGoalScored += HandleGoal;
            GameEvents.OnGoalNetHit += HandleGoalNetHit;
            GameEvents.OnWoodworkHit += HandleWoodwork;
            GameEvents.OnFoulCalled += HandleFoul;
            GameEvents.OnShotTaken += HandleShot;
            GameEvents.OnPassInitiated += HandlePass;
            GameEvents.OnMatchStateChanged += HandleMatchState;
        }

        private void OnDisable()
        {
            GameEvents.OnGoalScored -= HandleGoal;
            GameEvents.OnGoalNetHit -= HandleGoalNetHit;
            GameEvents.OnWoodworkHit -= HandleWoodwork;
            GameEvents.OnFoulCalled -= HandleFoul;
            GameEvents.OnShotTaken -= HandleShot;
            GameEvents.OnPassInitiated -= HandlePass;
            GameEvents.OnMatchStateChanged -= HandleMatchState;
        }

        private void HandleGoalNetHit()
        {
            PlayFoley(netRustleClip, 0.85f);
        }

        private void HandlePass(int teamId, Transform receiver)
        {
            PlayFoley(kickBallClip, Random.Range(0.45f, 0.65f));
        }

        private void HandleMatchState(MatchState state)
        {
            if (state == MatchState.InPlay || state == MatchState.HalfTime || state == MatchState.FullTime)
            {
                PlayFoley(whistleClip, 0.85f);
            }
        }

        private void Update()
        {
            UpdateExcitementFromBallPosition();
        }

        private void UpdateExcitementFromBallPosition()
        {
            var ball = FootballBall.Instance;
            if (ball == null) return;

            // Measure proximity of ball to either goal
            float distHomeGoal = Vector3.Distance(ball.transform.position, PitchConstants.HomeGoalCenter);
            float distAwayGoal = Vector3.Distance(ball.transform.position, PitchConstants.AwayGoalCenter);
            float closestGoalDist = Mathf.Min(distHomeGoal, distAwayGoal);

            // Excitement scales as ball approaches penalty box
            float targetExcitement = Mathf.Clamp01(1.0f - (closestGoalDist / dangerZoneDistance));
            currentExcitement = Mathf.Lerp(currentExcitement, targetExcitement, 3.5f * Time.deltaTime);

            if (excitementSwellSource != null)
            {
                excitementSwellSource.volume = currentExcitement * 0.85f;
            }

            GameEvents.TriggerCrowdExcitement(currentExcitement);
        }

        private void HandleGoal(int teamId, Vector3 pos)
        {
            PlayStinger(goalRoarClip, 1.0f);
        }

        private void HandleWoodwork()
        {
            PlayFoley(woodworkHitClip, 0.9f);
            PlayStinger(nearMissGaspClip, 0.8f);
        }

        private void HandleFoul(int teamId, Vector3 pos, bool isPenalty)
        {
            PlayFoley(whistleClip, 0.85f);
            PlayStinger(foulJeerClip, 0.6f);
        }

        private void HandleShot(int teamId, ShotType type, float power)
        {
            // Pitch scales slightly with power
            PlayFoley(kickBallClip, Mathf.Clamp01(0.5f + power * 0.5f));
        }

        public void PlayStinger(AudioClip clip, float volume = 1f)
        {
            if (clip != null && oneShotStingerSource != null)
            {
                oneShotStingerSource.PlayOneShot(clip, volume);
            }
        }

        public void PlayFoley(AudioClip clip, float volume = 1f)
        {
            if (clip != null && foleySource != null)
            {
                foleySource.pitch = Random.Range(0.92f, 1.08f);
                foleySource.PlayOneShot(clip, volume);
            }
        }
    }
}
