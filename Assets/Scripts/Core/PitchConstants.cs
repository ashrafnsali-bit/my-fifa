using UnityEngine;

namespace Football.Core
{
    /// <summary>
    /// Standard FIFA regulation pitch dimensions and spatial query utilities.
    /// Pitch coordinate system: Origin (0,0,0) is center circle.
    /// Z-axis is length (-52.5m to +52.5m). Team Home defends -Z, Team Away defends +Z.
    /// X-axis is width (-34.0m to +34.0m).
    /// Y-axis is vertical height.
    /// </summary>
    public static class PitchConstants
    {
        public const float PitchLength = 105.0f;
        public const float PitchWidth = 68.0f;
        public const float HalfLength = PitchLength * 0.5f; // 52.5m
        public const float HalfWidth = PitchWidth * 0.5f;   // 34.0m

        public const float GoalWidth = 7.32f;
        public const float GoalHeight = 2.44f;
        public const float GoalDepth = 2.0f;

        public const float PenaltyBoxLength = 16.5f;
        public const float PenaltyBoxWidth = 40.32f;
        public const float PenaltyBoxHalfWidth = PenaltyBoxWidth * 0.5f; // 20.16m

        public const float SixYardBoxLength = 5.5f;
        public const float SixYardBoxWidth = 18.32f;
        public const float SixYardBoxHalfWidth = SixYardBoxWidth * 0.5f; // 9.16m

        public const float PenaltySpotDistance = 11.0f;
        public const float CenterCircleRadius = 9.15f;
        public const float CornerArcRadius = 1.0f;

        public static readonly Vector3 HomeGoalCenter = new Vector3(0f, GoalHeight * 0.5f, -HalfLength);
        public static readonly Vector3 AwayGoalCenter = new Vector3(0f, GoalHeight * 0.5f, HalfLength);

        public static readonly Vector3 HomeGoalLineCenter = new Vector3(0f, 0f, -HalfLength);
        public static readonly Vector3 AwayGoalLineCenter = new Vector3(0f, 0f, HalfLength);

        public static readonly Vector3 HomePenaltySpot = new Vector3(0f, 0f, -HalfLength + PenaltySpotDistance);
        public static readonly Vector3 AwayPenaltySpot = new Vector3(0f, 0f, HalfLength - PenaltySpotDistance);

        public static bool IsInsidePitch(Vector3 pos, float margin = 0f)
        {
            return Mathf.Abs(pos.x) <= (HalfWidth + margin) && Mathf.Abs(pos.z) <= (HalfLength + margin);
        }

        public static Vector3 ClampToPitch(Vector3 pos, float padding = 1.0f)
        {
            float clampedX = Mathf.Clamp(pos.x, -HalfWidth + padding, HalfWidth - padding);
            float clampedZ = Mathf.Clamp(pos.z, -HalfLength + padding, HalfLength - padding);
            return new Vector3(clampedX, pos.y, clampedZ);
        }

        public static bool IsInsidePenaltyBox(Vector3 pos, bool homeSide)
        {
            if (Mathf.Abs(pos.x) > PenaltyBoxHalfWidth) return false;

            if (homeSide)
            {
                return pos.z >= -HalfLength && pos.z <= (-HalfLength + PenaltyBoxLength);
            }
            else
            {
                return pos.z <= HalfLength && pos.z >= (HalfLength - PenaltyBoxLength);
            }
        }

        public static Vector3 GetCornerPosition(bool homeSide, bool leftSide)
        {
            float x = leftSide ? -HalfWidth : HalfWidth;
            float z = homeSide ? -HalfLength : HalfLength;
            return new Vector3(x, 0f, z);
        }

        public static Vector3 GetPenaltySpot(bool homeSide)
        {
            return homeSide ? HomePenaltySpot : AwayPenaltySpot;
        }

        public static Vector3 GetThrowInPosition(Vector3 ballOutPosition)
        {
            float x = ballOutPosition.x > 0 ? HalfWidth : -HalfWidth;
            float z = Mathf.Clamp(ballOutPosition.z, -HalfLength, HalfLength);
            return new Vector3(x, 0f, z);
        }

        public static Vector3 GetTargetGoalCenter(int teamId)
        {
            // Team 1 attacks Away Goal (+Z), Team 2 attacks Home Goal (-Z)
            return teamId == 1 ? AwayGoalCenter : HomeGoalCenter;
        }

        public static Vector3 GetDefendingGoalCenter(int teamId)
        {
            return teamId == 1 ? HomeGoalCenter : AwayGoalCenter;
        }
    }
}
