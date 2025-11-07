using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for ComputeShaderManager functionality
    /// Converted from ComputeShaderSetupTest.cs
    /// </summary>
    [TestFixture]
    public class ComputeShaderTests
    {
        private ComputeShaderManager computeManager;

        [SetUp]
        public void SetUp()
        {
            // Get the ComputeShaderManager instance
            computeManager = ComputeShaderManager.Instance;
            Assert.IsNotNull(computeManager, "ComputeShaderManager instance should be available");
        }

        [Test]
        public void ShaderLoading_AllShadersLoaded()
        {
            // Test all 6 shaders are loaded
            Assert.IsNotNull(computeManager.NoiseShader, "Noise shader should be loaded");
            Assert.IsNotNull(computeManager.ErosionShader, "Erosion shader should be loaded");
            Assert.IsNotNull(computeManager.WeatherShader, "Weather shader should be loaded");
            Assert.IsNotNull(computeManager.ModificationShader, "Modification shader should be loaded");
            Assert.IsNotNull(computeManager.WFCShader, "WFC shader should be loaded");
            Assert.IsNotNull(computeManager.StructureShader, "Structure shader should be loaded");
        }

        [Test]
        public void KernelValidation_AllKernelsValid()
        {
            // Test all 8 kernels have valid indices (>= 0)
            Assert.GreaterOrEqual(computeManager.NoiseKernel, 0, "Noise kernel should be valid");
            Assert.GreaterOrEqual(computeManager.BiomeNoiseKernel, 0, "Biome noise kernel should be valid");
            Assert.GreaterOrEqual(computeManager.StructureNoiseKernel, 0, "Structure noise kernel should be valid");
            Assert.GreaterOrEqual(computeManager.ErosionKernel, 0, "Erosion kernel should be valid");
            Assert.GreaterOrEqual(computeManager.WeatherKernel, 0, "Weather kernel should be valid");
            Assert.GreaterOrEqual(computeManager.ModificationKernel, 0, "Modification kernel should be valid");
            Assert.GreaterOrEqual(computeManager.WFCKernel, 0, "WFC kernel should be valid");
            Assert.GreaterOrEqual(computeManager.StructureKernel, 0, "Structure kernel should be valid");
        }

        [Test]
        [TestCase(64, 8)]
        [TestCase(128, 16)]
        [TestCase(256, 32)]
        [TestCase(512, 64)]
        public void ThreadGroupCalculation_CorrectForResolution(int resolution, int expectedThreadGroups)
        {
            int actualThreadGroups = computeManager.CalculateThreadGroups(resolution);
            Assert.AreEqual(expectedThreadGroups, actualThreadGroups,
                $"Thread groups for resolution {resolution} should be {expectedThreadGroups}, got {actualThreadGroups}");
        }

        [Test]
        public void PerformanceMetrics_UpdateAndRetrieve()
        {
            // Test performance metrics update
            float noiseTime = 0.5f;
            float erosionTime = 0.3f;
            float weatherTime = 0.2f;

            computeManager.UpdatePerformanceMetrics(noiseTime, erosionTime, weatherTime);
            var metrics = computeManager.GetPerformanceMetrics();

            Assert.AreEqual(noiseTime, metrics.noiseTime, 0.001f,
                "Noise time should be updated correctly");
            Assert.AreEqual(erosionTime, metrics.erosionTime, 0.001f,
                "Erosion time should be updated correctly");
            Assert.AreEqual(weatherTime, metrics.weatherTime, 0.001f,
                "Weather time should be updated correctly");
        }

        [Test]
        public void ShaderValidation_ReturnsTrue()
        {
            bool isValid = computeManager.ValidateShaders();
            Assert.IsTrue(isValid, "Shader validation should pass");
        }

        [Test]
        public void ThreadGroupSize_IsPositive()
        {
            Assert.Greater(computeManager.ThreadGroupSize, 0,
                "Thread group size should be a positive value");
        }
    }
}

