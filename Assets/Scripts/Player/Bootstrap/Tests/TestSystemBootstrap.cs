using Unity.Entities;
using DOTS.Player.Bootstrap;

namespace DOTS.Player.Tests.Bootstrap
{
    /// <summary>
    /// Test helper to bootstrap player systems without requiring ProjectFeatureConfig.
    /// Use this in Unity Test Runner tests to ensure required systems are created.
    /// This bypasses both [DisableAutoCreation] and ProjectFeatureConfig requirements.
    /// </summary>
    public static class TestSystemBootstrap
    {
        /// <summary>
        /// Creates only the bootstrap systems (PlayerEntityBootstrap, etc.)
        /// Use this to create player and camera entities for testing.
        /// </summary>
        public static void CreateBootstrapSystemsOnly(World world)
        {
            if (world == null || !world.IsCreated)
            {
                UnityEngine.Debug.LogError("[TestSystemBootstrap] World is null or not created");
                return;
            }

            world.CreateSystem<PlayerBootstrapFixedRateInstaller>();
            world.CreateSystem<PlayerEntityBootstrap>();
            
            UnityEngine.Debug.Log("[TestSystemBootstrap] Created bootstrap systems only");
        }
    }
}

