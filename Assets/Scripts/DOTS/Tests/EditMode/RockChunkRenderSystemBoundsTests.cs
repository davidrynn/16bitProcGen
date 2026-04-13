using DOTS.Terrain.Rocks;
using NUnit.Framework;
using UnityEngine;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class RockChunkRenderSystemBoundsTests : SurfaceScatterRenderSystemContractTestsBase<RockRenderConfig>
    {
        protected override bool TryBuildWorldBounds(
            System.Collections.Generic.IReadOnlyList<Matrix4x4> matrices,
            in Bounds meshBounds,
            float uniformScale,
            out Bounds worldBounds)
        {
            return RockChunkRenderSystem.TryBuildWorldBounds(
                matrices,
                in meshBounds,
                uniformScale,
                out worldBounds);
        }

        protected override bool TryPrepareSubmissionFrame(RockRenderConfig config)
        {
            return RockChunkRenderSystem.TryPrepareSubmissionFrame(config);
        }

        protected override void AddPendingMatrixForTests(in Matrix4x4 matrix)
        {
            RockChunkRenderSystem.AddPendingMatrixForTests(matrix);
        }

        protected override bool HasPendingSubmissionDataForTests()
        {
            return RockChunkRenderSystem.HasPendingSubmissionDataForTests();
        }

        protected override RockRenderConfig CreateValidConfig(Mesh mesh, Material material, float uniformScale)
        {
            return new RockRenderConfig
            {
                Mesh = mesh,
                Material = material,
                UniformScale = uniformScale,
            };
        }

        protected override void ClearPendingSubmissionStateForTests()
        {
            RockChunkRenderSystem.TryPrepareSubmissionFrame(null);
        }
    }
}