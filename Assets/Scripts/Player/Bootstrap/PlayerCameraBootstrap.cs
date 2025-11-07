using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Pure code DOTS bootstrap - spawns player and camera entities at runtime.
    /// No authoring, no subscenes required. Just attach this to a GameObject in your scene.
    /// </summary>
    public class PlayerCameraBootstrap : MonoBehaviour
    {
        [Header("Initial Positions")]
        [SerializeField] private Vector3 playerStartPosition = new Vector3(0, 1, 0);
        [SerializeField] private Vector3 cameraStartPosition = new Vector3(0, 3, -4);

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            // Create Player Entity
            var playerEntity = entityManager.CreateEntity();
            entityManager.SetName(playerEntity, "Player (Runtime)");
            
            // Add player components
            entityManager.AddComponent<PlayerTag>(playerEntity);
            
            var playerTransform = new LocalTransform
            {
                Position = playerStartPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            };
            entityManager.AddComponentData(playerEntity, playerTransform);
            
            // LocalToWorld is required for CameraFollowSystem
            entityManager.AddComponentData(playerEntity, new LocalToWorld
            {
                Value = float4x4.TRS(playerTransform.Position, playerTransform.Rotation, new float3(playerTransform.Scale))
            });

            Debug.Log($"[PlayerCameraBootstrap] Player entity spawned at {playerStartPosition}");

            // Create Camera Entity
            var cameraEntity = entityManager.CreateEntity();
            entityManager.SetName(cameraEntity, "MainCamera (Runtime)");
            
            // Add camera components
            entityManager.AddComponent<MainCameraTag>(cameraEntity);
            
            var cameraTransform = new LocalTransform
            {
                Position = cameraStartPosition,
                Rotation = quaternion.LookRotation(math.normalize(playerStartPosition - cameraStartPosition), math.up()),
                Scale = 1f
            };
            entityManager.AddComponentData(cameraEntity, cameraTransform);
            
            // LocalToWorld for camera
            entityManager.AddComponentData(cameraEntity, new LocalToWorld
            {
                Value = float4x4.TRS(cameraTransform.Position, cameraTransform.Rotation, new float3(cameraTransform.Scale))
            });

            Debug.Log($"[PlayerCameraBootstrap] Camera entity spawned at {cameraStartPosition}");
            Debug.Log("[PlayerCameraBootstrap] CameraFollowSystem will automatically start following player");
        }
    }
}

