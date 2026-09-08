using System;
using UnityEngine;
using Football.Core;

namespace Football.Data
{
    [Serializable]
    public struct FormationSlot
    {
        public PlayerPosition position;
        public Vector2 normalizedOffset; // x: -1.0 (left) to +1.0 (right), y: -1.0 (own goal) to +1.0 (opponent goal)
        public string roleName;

        public FormationSlot(PlayerPosition pos, float normX, float normZ, string role)
        {
            position = pos;
            normalizedOffset = new Vector2(normX, normZ);
            roleName = role;
        }
    }

    /// <summary>
    /// Tactical formation template defining positional anchors for all 11 players.
    /// </summary>
    public static class FormationData
    {
        public static FormationSlot[] GetFormationSlots(FormationType type)
        {
            switch (type)
            {
                case FormationType.Formation_4_3_3:
                    return new FormationSlot[]
                    {
                        new FormationSlot(PlayerPosition.GK,   0.00f, -0.92f, "Goalkeeper"),
                        new FormationSlot(PlayerPosition.LB,  -0.70f, -0.65f, "Left Back"),
                        new FormationSlot(PlayerPosition.CB,  -0.25f, -0.70f, "Left Center Back"),
                        new FormationSlot(PlayerPosition.CB,   0.25f, -0.70f, "Right Center Back"),
                        new FormationSlot(PlayerPosition.RB,   0.70f, -0.65f, "Right Back"),
                        new FormationSlot(PlayerPosition.CDM,  0.00f, -0.45f, "Defensive Midfielder"),
                        new FormationSlot(PlayerPosition.CM,  -0.35f, -0.20f, "Left Central Midfielder"),
                        new FormationSlot(PlayerPosition.CM,   0.35f, -0.20f, "Right Central Midfielder"),
                        new FormationSlot(PlayerPosition.LW,  -0.75f,  0.25f, "Left Winger"),
                        new FormationSlot(PlayerPosition.ST,   0.00f,  0.40f, "Striker"),
                        new FormationSlot(PlayerPosition.RW,   0.75f,  0.25f, "Right Winger")
                    };

                case FormationType.Formation_4_4_2:
                    return new FormationSlot[]
                    {
                        new FormationSlot(PlayerPosition.GK,   0.00f, -0.92f, "Goalkeeper"),
                        new FormationSlot(PlayerPosition.LB,  -0.70f, -0.65f, "Left Back"),
                        new FormationSlot(PlayerPosition.CB,  -0.25f, -0.70f, "Left Center Back"),
                        new FormationSlot(PlayerPosition.CB,   0.25f, -0.70f, "Right Center Back"),
                        new FormationSlot(PlayerPosition.RB,   0.70f, -0.65f, "Right Back"),
                        new FormationSlot(PlayerPosition.LM,  -0.75f, -0.15f, "Left Midfielder"),
                        new FormationSlot(PlayerPosition.CM,  -0.25f, -0.25f, "Left Central Midfielder"),
                        new FormationSlot(PlayerPosition.CM,   0.25f, -0.25f, "Right Central Midfielder"),
                        new FormationSlot(PlayerPosition.RM,   0.75f, -0.15f, "Right Midfielder"),
                        new FormationSlot(PlayerPosition.ST,  -0.25f,  0.35f, "Left Striker"),
                        new FormationSlot(PlayerPosition.ST,   0.25f,  0.35f, "Right Striker")
                    };

                case FormationType.Formation_3_5_2:
                    return new FormationSlot[]
                    {
                        new FormationSlot(PlayerPosition.GK,   0.00f, -0.92f, "Goalkeeper"),
                        new FormationSlot(PlayerPosition.CB,  -0.45f, -0.70f, "Left Center Back"),
                        new FormationSlot(PlayerPosition.CB,   0.00f, -0.72f, "Sweeper / Central CB"),
                        new FormationSlot(PlayerPosition.CB,   0.45f, -0.70f, "Right Center Back"),
                        new FormationSlot(PlayerPosition.LWB, -0.80f, -0.10f, "Left Wing Back"),
                        new FormationSlot(PlayerPosition.CDM, -0.25f, -0.40f, "Left Defensive Mid"),
                        new FormationSlot(PlayerPosition.CDM,  0.25f, -0.40f, "Right Defensive Mid"),
                        new FormationSlot(PlayerPosition.CAM,  0.00f,  0.05f, "Attacking Midfielder"),
                        new FormationSlot(PlayerPosition.RWB,  0.80f, -0.10f, "Right Wing Back"),
                        new FormationSlot(PlayerPosition.ST,  -0.25f,  0.38f, "Left Striker"),
                        new FormationSlot(PlayerPosition.ST,   0.25f,  0.38f, "Right Striker")
                    };

                case FormationType.Formation_4_2_3_1:
                default:
                    return new FormationSlot[]
                    {
                        new FormationSlot(PlayerPosition.GK,   0.00f, -0.92f, "Goalkeeper"),
                        new FormationSlot(PlayerPosition.LB,  -0.70f, -0.65f, "Left Back"),
                        new FormationSlot(PlayerPosition.CB,  -0.25f, -0.70f, "Left Center Back"),
                        new FormationSlot(PlayerPosition.CB,   0.25f, -0.70f, "Right Center Back"),
                        new FormationSlot(PlayerPosition.RB,   0.70f, -0.65f, "Right Back"),
                        new FormationSlot(PlayerPosition.CDM, -0.28f, -0.42f, "Left Pivot"),
                        new FormationSlot(PlayerPosition.CDM,  0.28f, -0.42f, "Right Pivot"),
                        new FormationSlot(PlayerPosition.LAM, -0.65f,  0.10f, "Left Attacking Mid"),
                        new FormationSlot(PlayerPosition.CAM,  0.00f,  0.08f, "Central Attacking Mid"),
                        new FormationSlot(PlayerPosition.RAM,  0.65f,  0.10f, "Right Attacking Mid"),
                        new FormationSlot(PlayerPosition.ST,   0.00f,  0.42f, "Lone Striker")
                    };
            }
        }

        /// <summary>
        /// Translates a normalized formation slot into world coordinates for a specific team.
        /// Team 1 defends -Z and attacks +Z.
        /// Team 2 defends +Z and attacks -Z (mirrored).
        /// </summary>
        public static Vector3 GetWorldPosition(FormationSlot slot, int teamId, float teamShiftZ = 0f, float pitchWidthScale = 0.85f)
        {
            float x = slot.normalizedOffset.x * PitchConstants.HalfWidth * pitchWidthScale;
            float z = slot.normalizedOffset.y * PitchConstants.HalfLength * 0.85f;

            if (teamId == 2)
            {
                // Mirror for away team defending positive Z
                x = -x;
                z = -z;
                z -= teamShiftZ;
            }
            else
            {
                z += teamShiftZ;
            }

            return new Vector3(x, 0f, z);
        }
    }
}
