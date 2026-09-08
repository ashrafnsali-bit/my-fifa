using System;
using UnityEngine;

namespace Football.Tactics
{
    public enum DifficultyLevel
    {
        Beginner,
        SemiPro,
        Professional,
        WorldClass,
        Legendary
    }

    [Serializable]
    public class AIDifficultySettings
    {
        public DifficultyLevel level = DifficultyLevel.Professional;

        [Range(0.01f, 0.6f)] public float reactionLatency = 0.25f; // Seconds before AI responds to changes
        [Range(0f, 20f)] public float passErrorAngle = 6.0f;       // Angular inaccuracy on passes
        [Range(0f, 1f)] public float shotAccuracy = 0.75f;         // 1.0 = surgical precision
        [Range(0f, 1f)] public float pressingAggression = 0.65f;   // Urgency when closing down ball carrier
        [Range(0.5f, 5.0f)] public float markingDistance = 2.0f;   // Proximity when marking off-ball runners

        public static AIDifficultySettings GetPreset(DifficultyLevel diff)
        {
            var settings = new AIDifficultySettings { level = diff };
            switch (diff)
            {
                case DifficultyLevel.Beginner:
                    settings.reactionLatency = 0.45f;
                    settings.passErrorAngle = 14.0f;
                    settings.shotAccuracy = 0.50f;
                    settings.pressingAggression = 0.30f;
                    settings.markingDistance = 4.0f;
                    break;
                case DifficultyLevel.SemiPro:
                    settings.reactionLatency = 0.35f;
                    settings.passErrorAngle = 9.0f;
                    settings.shotAccuracy = 0.65f;
                    settings.pressingAggression = 0.50f;
                    settings.markingDistance = 3.0f;
                    break;
                case DifficultyLevel.Professional:
                    settings.reactionLatency = 0.22f;
                    settings.passErrorAngle = 5.5f;
                    settings.shotAccuracy = 0.78f;
                    settings.pressingAggression = 0.70f;
                    settings.markingDistance = 2.0f;
                    break;
                case DifficultyLevel.WorldClass:
                    settings.reactionLatency = 0.12f;
                    settings.passErrorAngle = 3.0f;
                    settings.shotAccuracy = 0.88f;
                    settings.pressingAggression = 0.85f;
                    settings.markingDistance = 1.4f;
                    break;
                case DifficultyLevel.Legendary:
                    settings.reactionLatency = 0.04f;
                    settings.passErrorAngle = 1.0f;
                    settings.shotAccuracy = 0.96f;
                    settings.pressingAggression = 0.95f;
                    settings.markingDistance = 0.9f;
                    break;
            }
            return settings;
        }
    }
}
