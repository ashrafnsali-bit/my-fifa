using UnityEngine;

namespace Football.Procedural
{
    /// <summary>
    /// Animates stadium crowd flags with natural wind ripple and wave fluttering.
    /// </summary>
    public class WavingFlag : MonoBehaviour
    {
        public float waveSpeed = 4.5f;
        public float waveAngle = 18.0f;
        public float phaseOffset = 0.0f;

        private Quaternion baseRotation;

        private void Awake()
        {
            baseRotation = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.time * waveSpeed + phaseOffset;
            float yaw = Mathf.Sin(t) * waveAngle;
            float roll = Mathf.Cos(t * 1.3f) * (waveAngle * 0.4f);

            transform.localRotation = baseRotation * Quaternion.Euler(roll, yaw, 0f);
        }
    }
}
