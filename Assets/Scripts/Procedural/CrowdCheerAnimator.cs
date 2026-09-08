using UnityEngine;

namespace Football.Procedural
{
    /// <summary>
    /// Breathes life into the stadium grandstands with subtle wave-like harmonic motion,
    /// simulating dynamic crowd singing, cheering, and banner sway.
    /// </summary>
    public class CrowdCheerAnimator : MonoBehaviour
    {
        [Header("Animation Tuning")]
        public float cheerFrequency = 3.2f;
        public float verticalBobAmount = 0.08f;
        public float wavePhaseOffset = 0.0f;
        public float swayAngle = 0.8f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        private void Update()
        {
            float time = Time.time * cheerFrequency + wavePhaseOffset;

            // Dual sinusoidal wave for organic crowd cheering cadence
            float cheerWave = Mathf.Sin(time) * 0.7f + Mathf.Sin(time * 1.8f) * 0.3f;
            float swayWave = Mathf.Cos(time * 0.85f);

            transform.localPosition = baseLocalPosition + new Vector3(0f, cheerWave * verticalBobAmount, 0f);
            transform.localRotation = baseLocalRotation * Quaternion.Euler(swayWave * swayAngle, 0f, 0f);
        }
    }
}
