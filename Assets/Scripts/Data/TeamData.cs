using System;
using System.Collections.Generic;
using UnityEngine;

namespace Football.Data
{
    public enum FormationType
    {
        Formation_4_3_3,
        Formation_4_4_2,
        Formation_3_5_2,
        Formation_4_2_3_1,
        Formation_5_3_2
    }

    [Serializable]
    public class TeamData
    {
        public string teamId = "ARG";
        public string teamName = "Argentina";
        public string shortCode = "ARG";
        public string confederation = "CONMEBOL";

        public Color homePrimaryColor = new Color(0.45f, 0.72f, 1.0f); // Sky blue
        public Color homeSecondaryColor = Color.white;
        public Color awayPrimaryColor = new Color(0.05f, 0.08f, 0.25f); // Deep navy
        public Color awaySecondaryColor = new Color(0.85f, 0.85f, 0.85f);
        public Color goalkeeperColor = new Color(0.1f, 0.8f, 0.3f); // Neon green

        public FormationType defaultFormation = FormationType.Formation_4_3_3;

        public List<PlayerAttributes> startingEleven = new List<PlayerAttributes>();
        public List<PlayerAttributes> substitutes = new List<PlayerAttributes>();

        public float GetAverageOverall()
        {
            if (startingEleven == null || startingEleven.Count == 0) return 75f;
            float sum = 0f;
            foreach (var p in startingEleven)
            {
                sum += p.CalculateOverallRating();
            }
            return sum / startingEleven.Count;
        }

        public float GetStarRating()
        {
            float avg = GetAverageOverall();
            // Map 65 -> 3 stars, 75 -> 4 stars, 85+ -> 5 stars
            return Mathf.Clamp(1.0f + ((avg - 55f) / 8f), 1.0f, 5.0f);
        }
    }
}
