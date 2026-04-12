using System.Collections.Generic;
using DOTS.Terrain.Trees;
using NUnit.Framework;
using UnityEngine;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TreeChunkRenderSystemBoundsTests
    {
        [Test]
        public void TryBuildWorldBounds_UsesInstancePositions_NotWorldOrigin()
        {
            var matrices = new List<Matrix4x4>
            {
                Matrix4x4.TRS(new Vector3(5500f, 8f, -3200f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(new Vector3(5512f, 10f, -3188f), Quaternion.identity, Vector3.one),
            };

            var meshBounds = new Bounds(Vector3.zero, new Vector3(2f, 4f, 2f));
            const float uniformScale = 1.5f;

            var built = TreeChunkRenderSystem.TryBuildWorldBounds(
                matrices,
                in meshBounds,
                uniformScale,
                out var bounds);

            Assert.IsTrue(built, "Expected bounds to be generated when matrices are present.");

            var extents = meshBounds.extents * uniformScale;
            var expectedMin = Vector3.Min(
                                  new Vector3(5500f, 8f, -3200f),
                                  new Vector3(5512f, 10f, -3188f)) - extents;
            var expectedMax = Vector3.Max(
                                  new Vector3(5500f, 8f, -3200f),
                                  new Vector3(5512f, 10f, -3188f)) + extents;
            var expectedCenter = (expectedMin + expectedMax) * 0.5f;
            var expectedSize = expectedMax - expectedMin;

            Assert.AreEqual(expectedCenter.x, bounds.center.x, 1e-5f);
            Assert.AreEqual(expectedCenter.y, bounds.center.y, 1e-5f);
            Assert.AreEqual(expectedCenter.z, bounds.center.z, 1e-5f);

            Assert.AreEqual(expectedSize.x, bounds.size.x, 1e-5f);
            Assert.AreEqual(expectedSize.y, bounds.size.y, 1e-5f);
            Assert.AreEqual(expectedSize.z, bounds.size.z, 1e-5f);

            Assert.Greater(bounds.center.x, 5000f, "Bounds center should track far-world instances.");
            Assert.Less(bounds.center.z, -3000f, "Bounds center should track far-world instances.");
        }

        [Test]
        public void TryBuildWorldBounds_EmptyInput_ReturnsFalse()
        {
            var matrices = new List<Matrix4x4>();
            var meshBounds = new Bounds(Vector3.zero, Vector3.one);

            var built = TreeChunkRenderSystem.TryBuildWorldBounds(
                matrices,
                in meshBounds,
                1f,
                out _);

            Assert.IsFalse(built);
        }

        [Test]
        public void TryPrepareSubmissionFrame_InvalidConfig_ClearsPreviouslyQueuedPendingState()
        {
            var shader = Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader, "Expected at least one built-in shader for test material creation.");

            var mesh = new Mesh();
            var material = new Material(shader);

            try
            {
                var validConfig = new TreeRenderConfig
                {
                    Mesh = mesh,
                    Material = material,
                    UniformScale = 1f
                };

                var prepared = TreeChunkRenderSystem.TryPrepareSubmissionFrame(validConfig);
                Assert.IsTrue(prepared);

                TreeChunkRenderSystem.AddPendingMatrixForTests(Matrix4x4.identity);
                Assert.IsTrue(TreeChunkRenderSystem.HasPendingSubmissionDataForTests(),
                    "Expected pending submission data after valid prep and queued matrix.");

                var preparedWithInvalid = TreeChunkRenderSystem.TryPrepareSubmissionFrame(null);
                Assert.IsFalse(preparedWithInvalid);
                Assert.IsFalse(TreeChunkRenderSystem.HasPendingSubmissionDataForTests(),
                    "Invalid config must clear stale pending submission state to prevent ghost draws.");
            }
            finally
            {
                TreeChunkRenderSystem.TryPrepareSubmissionFrame(null);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
