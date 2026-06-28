using DOTS.Terrain.Rocks;
using NUnit.Framework;
using UnityEngine;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class RockChunkRenderSystemBoundsTests : SurfaceScatterRenderSystemContractTestsBase<RockRenderConfig>
    {
        [Test]
        public void TryPrepareSubmissionFrame_DisabledInstancing_AutoEnablesInstancing()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader, "Expected a built-in shader for instancing validation.");

            var mesh = new Mesh();
            var material = new Material(shader)
            {
                enableInstancing = false,
            };

            try
            {
                var config = new RockRenderConfig
                {
                    Mesh = mesh,
                    Material = material,
                    UniformScale = 1f,
                };

                var prepared = RockChunkRenderSystem.TryPrepareSubmissionFrame(config);
                Assert.IsTrue(prepared, "Rock render prep should succeed after auto-enabling material instancing.");
                Assert.IsTrue(material.enableInstancing, "Rock render prep should enable material instancing when disabled.");
            }
            finally
            {
                RockChunkRenderSystem.TryPrepareSubmissionFrame(null);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TryPrepareSubmissionFrame_MeshVariantsOnly_ConfigIsAccepted()
        {
            var shader = Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(shader, "Expected a built-in shader for variant-only config validation.");

            var mesh = new Mesh();
            var material = new Material(shader);

            try
            {
                var config = new RockRenderConfig
                {
                    MeshVariants = new[] { mesh },
                    Mesh = null,
                    Material = material,
                    UniformScale = 1f,
                };

                var prepared = RockChunkRenderSystem.TryPrepareSubmissionFrame(config);
                Assert.IsTrue(prepared, "Variant-only config should be accepted for rock rendering.");

                RockChunkRenderSystem.AddPendingMatrixForTests(Matrix4x4.identity);
                Assert.IsTrue(RockChunkRenderSystem.HasPendingSubmissionDataForTests());
            }
            finally
            {
                RockChunkRenderSystem.TryPrepareSubmissionFrame(null);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TryPrepareSubmissionFrame_ErrorShaderMaterial_UsesRuntimeFallback()
        {
            var errorShader = Shader.Find("Hidden/InternalErrorShader");
            Assert.IsNotNull(errorShader, "Expected Hidden/InternalErrorShader to exist for fallback validation.");

            var mesh = new Mesh();
            var material = new Material(errorShader)
            {
                enableInstancing = false,
            };

            try
            {
                var config = new RockRenderConfig
                {
                    Mesh = mesh,
                    Material = material,
                    UniformScale = 1f,
                };

                var prepared = RockChunkRenderSystem.TryPrepareSubmissionFrame(config);
                Assert.IsTrue(prepared, "Rock render prep should fall back to a runtime material when source shader is invalid.");

                RockChunkRenderSystem.AddPendingMatrixForTests(Matrix4x4.identity);
                Assert.IsTrue(RockChunkRenderSystem.HasPendingSubmissionDataForTests());
            }
            finally
            {
                RockChunkRenderSystem.TryPrepareSubmissionFrame(null);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
            }
        }

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

        protected override RockRenderConfig CreateConfigWithLodVariants(
            Mesh[] meshVariants,
            Mesh[] lodMeshVariants,
            Material material,
            float lodSwapDistance)
        {
            return new RockRenderConfig
            {
                MeshVariants = meshVariants,
                LodMeshVariants = lodMeshVariants,
                Material = material,
                UniformScale = 1f,
                LodSwapDistance = lodSwapDistance,
            };
        }

        protected override Mesh GetPendingMeshForTests(int variantIndex, int lodLevel)
        {
            return RockChunkRenderSystem.GetPendingMeshForTests(variantIndex, lodLevel);
        }

        protected override void ClearPendingSubmissionStateForTests()
        {
            RockChunkRenderSystem.TryPrepareSubmissionFrame(null);
        }
    }
}