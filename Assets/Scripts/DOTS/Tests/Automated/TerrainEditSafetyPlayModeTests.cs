using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainEditSafetyPlayModeTests
    {
        [UnityTest]
        public IEnumerator AddEdit_OverlappingPlayerCapsule_IsBlocked()
        {
            var playerOrigin = float3.zero;
            var overlappingAddEdit = SDFEdit.CreateBox(
                new float3(0f, 1.0f, 0f),
                new float3(0.6f, 0.6f, 0.6f),
                SDFEditOperation.Add);

            var blocked = TerrainEditInputSystem.IsAddEditBlockedByPlayerSafetyVolume(
                in overlappingAddEdit,
                playerOrigin,
                clearance: 0.15f);

            Assert.IsTrue(blocked,
                "Add edit intersecting player safety capsule must be blocked.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AddEdit_AwayFromPlayerCapsule_IsNotBlocked()
        {
            var playerOrigin = float3.zero;
            var farAddEdit = SDFEdit.CreateBox(
                new float3(4f, 1.0f, 0f),
                new float3(0.5f, 0.5f, 0.5f),
                SDFEditOperation.Add);

            var blocked = TerrainEditInputSystem.IsAddEditBlockedByPlayerSafetyVolume(
                in farAddEdit,
                playerOrigin,
                clearance: 0.15f);

            Assert.IsFalse(blocked,
                "Add edit outside player safety capsule should be allowed.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SubtractEdit_Overlap_IsNotBlockedByAddGuard()
        {
            var playerOrigin = float3.zero;
            var overlappingSubtractEdit = SDFEdit.CreateBox(
                new float3(0f, 1.0f, 0f),
                new float3(0.6f, 0.6f, 0.6f),
                SDFEditOperation.Subtract);

            var blocked = TerrainEditInputSystem.IsAddEditBlockedByPlayerSafetyVolume(
                in overlappingSubtractEdit,
                playerOrigin,
                clearance: 0.15f);

            Assert.IsFalse(blocked,
                "Safety guard is add-only; overlapping subtract edits should not be blocked here.");
            yield return null;
        }
    }
}
