using System;
using UnityEngine;

namespace Football.Data
{
    public enum PlayerPosition
    {
        GK,  // Goalkeeper
        CB,  // Center Back
        LB,  // Left Back
        RB,  // Right Back
        LWB, // Left Wing Back
        RWB, // Right Wing Back
        CDM, // Central Defensive Midfielder
        CM,  // Central Midfielder
        LM,  // Left Midfielder
        RM,  // Right Midfielder
        CAM, // Central Attacking Midfielder
        LAM, // Left Attacking Midfielder
        RAM, // Right Attacking Midfielder
        LW,  // Left Winger
        RW,  // Right Winger
        ST   // Striker
    }

    public enum Footedness
    {
        Right,
        Left,
        Both
    }

    public enum WorkRate
    {
        Low,
        Medium,
        High
    }

    [Serializable]
    public class PlayerAttributes
    {
        public string playerName = "Player";
        public int jerseyNumber = 10;
        public PlayerPosition position = PlayerPosition.CM;
        public Footedness preferredFoot = Footedness.Right;
        public WorkRate attackingWorkRate = WorkRate.High;
        public WorkRate defendingWorkRate = WorkRate.Medium;

        [Range(150, 205)] public int heightCm = 182;
        [Range(55, 100)] public int weightKg = 75;

        // --- Pace (1-99) ---
        [Range(1, 99)] public int sprintSpeed = 75;
        [Range(1, 99)] public int acceleration = 75;

        // --- Shooting (1-99) ---
        [Range(1, 99)] public int finishing = 70;
        [Range(1, 99)] public int shotPower = 75;
        [Range(1, 99)] public int longShots = 70;
        [Range(1, 99)] public int curve = 70;
        [Range(1, 99)] public int penalties = 75;
        [Range(1, 99)] public int heading = 70;

        // --- Passing (1-99) ---
        [Range(1, 99)] public int shortPassing = 78;
        [Range(1, 99)] public int longPassing = 74;
        [Range(1, 99)] public int vision = 76;
        [Range(1, 99)] public int crossing = 72;

        // --- Dribbling (1-99) ---
        [Range(1, 99)] public int dribbling = 78;
        [Range(1, 99)] public int agility = 75;
        [Range(1, 99)] public int balance = 75;
        [Range(1, 99)] public int ballControl = 78;
        [Range(1, 99)] public int composure = 75;

        // --- Defending (1-99) ---
        [Range(1, 99)] public int interceptions = 65;
        [Range(1, 99)] public int marking = 65;
        [Range(1, 99)] public int standingTackle = 68;
        [Range(1, 99)] public int slidingTackle = 62;

        // --- Physical (1-99) ---
        [Range(1, 99)] public int strength = 72;
        [Range(1, 99)] public int stamina = 80;
        [Range(1, 99)] public int aggression = 70;
        [Range(1, 99)] public int jumping = 70;

        // --- Goalkeeping (1-99) ---
        [Range(1, 99)] public int gkDiving = 50;
        [Range(1, 99)] public int gkHandling = 50;
        [Range(1, 99)] public int gkKicking = 50;
        [Range(1, 99)] public int gkReflexes = 50;
        [Range(1, 99)] public int gkPositioning = 50;

        /// <summary>
        /// Calculates Overall Rating (OVR 1-99) dynamically based on positional weighting.
        /// </summary>
        public int CalculateOverallRating()
        {
            float ovr;
            switch (position)
            {
                case PlayerPosition.GK:
                    ovr = (gkDiving * 0.25f) + (gkHandling * 0.20f) + (gkReflexes * 0.25f) + (gkPositioning * 0.20f) + (gkKicking * 0.10f);
                    break;
                case PlayerPosition.CB:
                    ovr = (marking * 0.25f) + (standingTackle * 0.25f) + (interceptions * 0.15f) + (strength * 0.15f) + (jumping * 0.10f) + (sprintSpeed * 0.10f);
                    break;
                case PlayerPosition.LB:
                case PlayerPosition.RB:
                    ovr = (sprintSpeed * 0.20f) + (acceleration * 0.15f) + (standingTackle * 0.20f) + (crossing * 0.15f) + (stamina * 0.15f) + (interceptions * 0.15f);
                    break;
                case PlayerPosition.CDM:
                    ovr = (shortPassing * 0.20f) + (standingTackle * 0.20f) + (interceptions * 0.20f) + (stamina * 0.15f) + (strength * 0.15f) + (vision * 0.10f);
                    break;
                case PlayerPosition.CM:
                    ovr = (shortPassing * 0.25f) + (longPassing * 0.15f) + (vision * 0.20f) + (ballControl * 0.15f) + (stamina * 0.15f) + (agility * 0.10f);
                    break;
                case PlayerPosition.CAM:
                    ovr = (vision * 0.25f) + (shortPassing * 0.20f) + (ballControl * 0.20f) + (agility * 0.15f) + (finishing * 0.10f) + (longShots * 0.10f);
                    break;
                case PlayerPosition.LW:
                case PlayerPosition.RW:
                    ovr = (sprintSpeed * 0.25f) + (acceleration * 0.20f) + (agility * 0.15f) + (crossing * 0.15f) + (ballControl * 0.15f) + (finishing * 0.10f);
                    break;
                case PlayerPosition.ST:
                    ovr = (finishing * 0.30f) + (shotPower * 0.15f) + (sprintSpeed * 0.15f) + (acceleration * 0.10f) + (ballControl * 0.10f) + (composure * 0.10f) + (strength * 0.10f);
                    break;
                default:
                    ovr = (sprintSpeed + finishing + shortPassing + ballControl + standingTackle + stamina) / 6f;
                    break;
            }
            return Mathf.Clamp(Mathf.RoundToInt(ovr), 45, 99);
        }
    }
}
