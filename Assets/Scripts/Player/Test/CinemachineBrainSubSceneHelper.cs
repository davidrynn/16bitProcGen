using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Test
{
    /// <summary>
    /// Ensures CinemachineBrain can find and use CinemachineCamera components in subscenes.
    /// This is needed because subscene cameras may not be automatically discovered by the Brain.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CinemachineBrainSubSceneHelper : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only run once to set up the cameras
            if (_initialized)
                return;

            var entityManager = state.EntityManager;
            var foundCamera = false;

            // Find all CinemachineCamera components on entities
            foreach (var entity in SystemAPI.QueryBuilder().WithAll<CinemachineCamera>().Build().ToEntityArray(state.WorldUpdateAllocator))
            {
                if (!entityManager.HasComponent<CinemachineCamera>(entity))
                    continue;

                var virtualCamera = entityManager.GetComponentObject<CinemachineCamera>(entity);
                if (virtualCamera == null)
                    continue;

                // Ensure it's enabled so it registers with CinemachineCore
                if (!virtualCamera.enabled)
                {
                    virtualCamera.enabled = false; // Disable first
                    virtualCamera.enabled = true;  // Then enable to trigger registration
                    Debug.Log($"CinemachineBrainSubSceneHelper: Force-enabled {virtualCamera.name}");
                }

                foundCamera = true;
            }

            if (foundCamera)
            {
                _initialized = true;
                Debug.Log("CinemachineBrainSubSceneHelper: Initialized subscene virtual cameras");
            }
        }
    }
}




