using NUnit.Framework;
using UnityEngine;
using Football.Core;
using Football.Data;

namespace Football.Tests
{
    public class TacticalFormationTests
    {
        [Test]
        [TestCase(FormationType.Formation_4_3_3)]
        [TestCase(FormationType.Formation_4_4_2)]
        [TestCase(FormationType.Formation_3_5_2)]
        [TestCase(FormationType.Formation_4_2_3_1)]
        public void Formations_ContainElevenPlayersWithinPitchBounds(FormationType type)
        {
            var slots = FormationData.GetFormationSlots(type);
            Assert.AreEqual(11, slots.Length, $"Formation {type} must contain exactly 11 slots.");

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                Vector3 homeWorldPos = FormationData.GetWorldPosition(slot, 1);
                Vector3 awayWorldPos = FormationData.GetWorldPosition(slot, 2);

                Assert.IsTrue(PitchConstants.IsInsidePitch(homeWorldPos), $"Home slot {slot.roleName} at {homeWorldPos} must be inside pitch boundaries.");
                Assert.IsTrue(PitchConstants.IsInsidePitch(awayWorldPos), $"Away slot {slot.roleName} at {awayWorldPos} must be inside pitch boundaries.");

                // Verify Home attacks +Z (Z > -HalfLength) and Away defends +Z (Z < HalfLength)
                Assert.GreaterOrEqual(homeWorldPos.z, -PitchConstants.HalfLength);
                Assert.LessOrEqual(awayWorldPos.z, PitchConstants.HalfLength);
            }
        }
    }
}
