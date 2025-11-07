using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;
#if UNITY_ENTITIES_GRAPHICS
using Unity.Rendering;
#endif

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// PURE ECS RENDERING ALTERNATIVE (Fast-Follow)
    /// 
    /// This system uses Entities.Graphics package for true pure ECS rendering.
    /// No GameObjects at runtime - everything is rendered via GPU instancing.
    /// 
    /// TO USE THIS:
    /// 1. Install Entities.Graphics package (Window > Package Manager > Unity Registry > Entities Graphics)
    /// 2. Replace PlayerEntityBootstrap with this system
    /// 3. Disable PlayerVisualSync GameObjects
    /// 
    /// BENEFITS:
    /// - True pure ECS - no GameObjects
    /// - GPU instancing for thousands of entities
    /// - Burst-compiled rendering pipeline
    /// - Better performance at scale
    /// 
    /// DISABLED BY DEFAULT - Only enable when ready to use Entities.Graphics
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerEntityBootstrap_PureECS : ISystem
    {
        private bool _hasSpawned;

        public void OnCreate(ref SystemState state)
        {
            // DISABLED BY DEFAULT - This creates duplicate entities
            // Only enable when ready to migrate to pure ECS rendering
            state.Enabled = false;
            _hasSpawned = false;
            
            Debug.Log("[PlayerEntityBootstrap_PureECS] DISABLED - Use PlayerEntityBootstrap instead. Enable this only when ready to migrate to Entities.Graphics rendering.");
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only spawn once
            if (_hasSpawned) 
            {
                state.Enabled = false;
                return;
            }

            // Check if player already exists
            if (!SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build().IsEmpty)
            {
                Debug.Log("[PlayerBootstrap_PureECS] Player entity already exists, skipping spawn");
                _hasSpawned = true;
                state.Enabled = false;
                return;
            }

            CreatePlayerEntityWithRendering(ref state);
            CreateGroundPlaneWithRendering(ref state);
            CreateCameraEntity(ref state);
            
            _hasSpawned = true;
            state.Enabled = false;
        }

        private void CreatePlayerEntityWithRendering(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var entity = entityManager.CreateEntity();
            entityManager.SetName(entity, "Player (Pure ECS)");

            // Add all the same gameplay components as regular bootstrap
            entityManager.AddComponentData(entity, new PlayerMovementConfig
            {
                GroundSpeed = 10f,
                JumpImpulse = 5f,
                AirControl = 0.2f,
                SlingshotImpulse = 30f,
                SwimSpeed = 6f,
                ZeroGDamping = 2f,
                MouseSensitivity = 0.1f,
                MaxPitchDegrees = 85f,
                GroundProbeDistance = 1.3f
            });

            entityManager.AddComponentData(entity, new PlayerInputComponent());
            entityManager.AddComponentData(entity, new PlayerMovementState());
            entityManager.AddComponentData(entity, new PlayerViewComponent());
            
            var playerPos = new float3(0, 2, 0);
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = playerPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            entityManager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(playerPos, quaternion.identity, new float3(1f))
            });

            // Physics components
            entityManager.AddComponentData(entity, new PhysicsVelocity());
            
            entityManager.AddComponentData(entity, new PhysicsMass
            {
                InverseMass = 1f / 70f,
                InverseInertia = new float3(1f),
                Transform = RigidTransform.identity,
                AngularExpansionFactor = 0f,
                CenterOfMass = float3.zero
            });

            var capsuleGeometry = new CapsuleGeometry
            {
                Vertex0 = new float3(0, 0.5f, 0),
                Vertex1 = new float3(0, 1.5f, 0),
                Radius = 0.5f
            };

            entityManager.AddComponentData(entity, new PhysicsCollider
            {
                Value = Unity.Physics.CapsuleCollider.Create(
                    capsuleGeometry,
                    new CollisionFilter { BelongsTo = 1u, CollidesWith = ~0u },
                    Unity.Physics.Material.Default
                )
            });

            entityManager.AddComponentData(entity, new PhysicsDamping { Linear = 0f, Angular = 0f });
            entityManager.AddComponentData(entity, new PhysicsGravityFactor { Value = 1f });
            entityManager.AddComponent<PlayerTag>(entity);
            entityManager.AddComponentData(entity, new PlayerCameraSettings { Offset = new float3(0f, 1.6f, 0f) });

            // ===== PURE ECS RENDERING =====
            // NOTE: Requires Entities.Graphics package to be installed
            // Install via: Window > Package Manager > Unity Registry > Entities Graphics
#if UNITY_ENTITIES_GRAPHICS
            // TODO: Replace with actual mesh/material loading from Resources or AssetBundle
            // For now, this is a placeholder showing the structure
            
            // Option 1: Create procedural mesh in code
            var mesh = CreateCapsuleMesh();
            var material = CreatePlayerMaterial();
            
            // Add render mesh component (Entities.Graphics)
            entityManager.AddComponentData(entity, new RenderMesh
            {
                mesh = mesh,
                material = material,
                subMesh = 0,
                layer = 0,
                castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true
            });

            // Add render bounds for culling
            entityManager.AddComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = playerPos,
                    Extents = new float3(0.5f, 1f, 0.5f) // Capsule bounds
                }
            });

            Debug.Log("[PlayerBootstrap_PureECS] Player entity created with pure ECS rendering");
#else
            Debug.LogWarning("[PlayerBootstrap_PureECS] Entities.Graphics package not installed. Install via Package Manager to enable pure ECS rendering.");
            Debug.LogWarning("[PlayerBootstrap_PureECS] Player entity created but without visual rendering. Use PlayerEntityBootstrap for minimal hybrid approach.");
#endif
        }

        private void CreateGroundPlaneWithRendering(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var groundEntity = entityManager.CreateEntity();
            entityManager.SetName(groundEntity, "Ground (Pure ECS)");

            var groundPos = float3.zero;
            var groundSize = new float2(50f, 50f);

            entityManager.AddComponentData(groundEntity, new LocalTransform
            {
                Position = groundPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            entityManager.AddComponentData(groundEntity, new LocalToWorld
            {
                Value = float4x4.TRS(groundPos, quaternion.identity, new float3(1f))
            });

            // Physics collider
            var boxGeometry = new BoxGeometry
            {
                Size = new float3(groundSize.x, 0.1f, groundSize.y),
                Orientation = quaternion.identity
            };

            entityManager.AddComponentData(groundEntity, new PhysicsCollider
            {
                Value = Unity.Physics.BoxCollider.Create(boxGeometry, CollisionFilter.Default)
            });

            entityManager.AddComponent<PhysicsWorldIndex>(groundEntity);

#if UNITY_ENTITIES_GRAPHICS
            // Pure ECS rendering for ground
            var groundMesh = CreatePlaneMesh(groundSize);
            var groundMaterial = CreateGroundMaterial();

            entityManager.AddComponentData(groundEntity, new RenderMesh
            {
                mesh = groundMesh,
                material = groundMaterial,
                subMesh = 0,
                layer = 0,
                castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true
            });

            entityManager.AddComponentData(groundEntity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = groundPos,
                    Extents = new float3(groundSize.x * 0.5f, 0.05f, groundSize.y * 0.5f)
                }
            });

            Debug.Log("[PlayerBootstrap_PureECS] Ground plane created with pure ECS rendering");
#else
            Debug.LogWarning("[PlayerBootstrap_PureECS] Ground plane created but without visual rendering (Entities.Graphics not installed).");
#endif
        }

        private void CreateCameraEntity(ref SystemState state)
        {
            // NOTE: Camera still needs GameObject for Unity's rendering pipeline
            // But we can create a camera entity that the camera system follows
            
            var entityManager = state.EntityManager;
            var cameraEntity = entityManager.CreateEntity();
            entityManager.SetName(cameraEntity, "Camera Entity (ECS)");

            var cameraPos = new float3(0, 3, -5);
            entityManager.AddComponentData(cameraEntity, new LocalTransform
            {
                Position = cameraPos,
                Rotation = quaternion.LookRotation(math.normalize(new float3(0, 2, 0) - cameraPos), math.up()),
                Scale = 1f
            });

            entityManager.AddComponent<MainCameraTag>(cameraEntity);

            // Create Unity Camera GameObject that follows this entity
            var cameraGO = new GameObject("Main Camera (Pure ECS)");
            var camera = cameraGO.AddComponent<Camera>();
            camera.tag = "MainCamera";
            cameraGO.AddComponent<AudioListener>();

            // Camera system will sync GameObject to entity transform
            Debug.Log("[PlayerBootstrap_PureECS] Camera entity created");
        }

        // ===== HELPER METHODS FOR PURE ECS RENDERING =====

        private Mesh CreateCapsuleMesh()
        {
            // Create a simple capsule mesh procedurally
            // In production, load from Resources or AssetBundle
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule).GetComponent<MeshFilter>().sharedMesh;
            return mesh;
        }

        private Mesh CreatePlaneMesh(float2 size)
        {
            // Create a simple plane mesh
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Plane).GetComponent<MeshFilter>().sharedMesh;
            return mesh;
        }

        private UnityEngine.Material CreatePlayerMaterial()
        {
            // Create material with blue color
            var material = new UnityEngine.Material(Shader.Find("Standard"));
            material.color = new Color(0.2f, 0.4f, 1f); // Blue player
            return material;
        }

        private UnityEngine.Material CreateGroundMaterial()
        {
            // Create material with green color
            var material = new UnityEngine.Material(Shader.Find("Standard"));
            material.color = new Color(0.3f, 0.5f, 0.3f); // Green ground
            return material;
        }
    }
}

