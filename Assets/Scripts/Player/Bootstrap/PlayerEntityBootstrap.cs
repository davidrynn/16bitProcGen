using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;
using DOTS.Terrain.Core;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Pure ECS approach: Creates player entity entirely from code without GameObject authoring.
    /// Runs once at startup to spawn the player with all required components.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerEntityBootstrap : ISystem
    {
        private bool _hasSpawned;
        // Player spawn configuration — high enough for terrain colliders to build before landing
        public const float PlayerStartHeight = 20f;

        public void OnCreate(ref SystemState state)
        {
            _hasSpawned = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Only spawn once
            if (_hasSpawned) 
            {
                state.Enabled = false;
                return;
            }

            // Check if player already exists (in case of scene reload)
            if (!SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build().IsEmpty)
            {
                _hasSpawned = true;
                state.Enabled = false;
                return;
            }

            var playerEntity = CreatePlayerEntity(ref state);
            // Ground plane removed: it sat at Y=0 inside the terrain surface range [-4,+4],
            // trapping the player under terrain after colliders built.
            // Terrain mesh colliders are the sole physics surface.
            
            _hasSpawned = true;
            state.Enabled = false; // Disable after spawning
        }

        private Entity CreatePlayerEntity(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var entity = entityManager.CreateEntity();
            var spawnPos = new float3(0, PlayerStartHeight, 0);

            // Add core player components
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

            entityManager.AddComponentData(entity, new PlayerInputComponent
            {
                Move = float2.zero,
                Look = float2.zero,
                JumpPressed = false
            });

            entityManager.AddComponentData(entity, new PlayerMovementState
            {
                Mode = PlayerMovementMode.Ground,
                IsGrounded = false,
                FallTime = 0f,
                PreviousPosition = spawnPos
            });

            entityManager.AddComponentData(entity, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });

            // Add transform
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = spawnPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            entityManager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(
                    spawnPos,
                    quaternion.identity,
                    new float3(1f)
                )
            });

            // Add physics components - CRITICAL for PlayerMovementSystem
            entityManager.AddComponentData(entity, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });

            entityManager.AddComponentData(entity, new PhysicsMass
            {
                InverseMass = 1f / 70f, // 70kg player
                InverseInertia = new float3(1f, 1f, 1f),
                Transform = RigidTransform.identity,
                AngularExpansionFactor = 0f,
                CenterOfMass = float3.zero
            });

            // Create capsule collider for player
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
                    new CollisionFilter
                    {
                        BelongsTo = 1u,    // Player layer
                        CollidesWith = ~0u, // Collides with everything
                        GroupIndex = 0
                    },
                    Unity.Physics.Material.Default
                )
            });

            // Add damping to smooth movement
            entityManager.AddComponentData(entity, new PhysicsDamping
            {
                Linear = 0f,
                Angular = 0f
            });

            // Prevent rotation constraints (freeze rotation to keep player upright)
            // Note: Rotation freezing is handled by the collider and not applying angular velocity

            // Gravity scale (normal gravity)
            entityManager.AddComponentData(entity, new PhysicsGravityFactor
            {
                Value = 1f
            });

            // Register the player with the active physics world so gravity and contacts run
            entityManager.AddSharedComponent(entity, new PhysicsWorldIndex());

            // Add player tag for identification
            entityManager.AddComponent<PlayerTag>(entity);

            // Get player position and view for camera setup
            var playerPosition = spawnPos;
            var playerView = entityManager.GetComponentData<PlayerViewComponent>(entity);

            // Create camera entity and link it to player
            Entity cameraEntity = CreateMainCameraAndEntity(ref state, entity, playerPosition, playerView);
            
            if (cameraEntity != Entity.Null)
            {
                // Create PlayerCameraLink to connect player to camera
                entityManager.AddComponentData(entity, new PlayerCameraLink
                {
                    CameraEntity = cameraEntity,
                    FollowAnchor = entity,  // Player entity as follow anchor
                    LookAnchor = entity     // Player entity as look anchor
                });
                
                entityManager.AddComponentData(entity, new PlayerCameraSettings
                {
                    FirstPersonOffset = new float3(0f, 1.6f, 0f),        // Camera at eye level
                    ThirdPersonPivotOffset = new float3(0f, 1.5f, 0f),   // Shoulder height pivot
                    ThirdPersonDistance = 3.5f,
                    IsThirdPerson = false
                });

            }
            else
            {
                entityManager.AddComponentData  (entity, new PlayerCameraSettings
                {
                    FirstPersonOffset = new float3(0f, 1.6f, 0f),
                    ThirdPersonPivotOffset = new float3(0f, 1.5f, 0f),
                    ThirdPersonDistance = 3.5f,
                    IsThirdPerson = false
                });

                Debug.LogWarning("[PlayerBootstrap] Failed to create camera entity - camera system may not work");
            }

            // Create visual representation (minimal hybrid - GameObject sync)
            CreatePlayerVisual(entity, spawnPos);
            
            return entity;
        }

        private void CreatePlayerVisual(Entity playerEntity, float3 position)
        {
            // Create a capsule to represent the player
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Player Visual (ECS Synced)";
            visual.transform.position = position;
            visual.transform.localScale = new Vector3(1f, 2f, 1f); // Taller capsule for player

            // Make it blue to distinguish from ground
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.4f, 1f); // Blue player
            }

            // Remove GameObject collider - physics is handled by ECS
            Object.Destroy(visual.GetComponent<UnityEngine.Collider>());

            // Add sync component to follow ECS entity
            var sync = visual.AddComponent<PlayerVisualSync>();
            sync.targetEntity = playerEntity;

        }

        private Entity CreateMainCameraAndEntity(ref SystemState state, Entity playerEntity, float3 playerPosition, PlayerViewComponent playerView)
        {
            var entityManager = state.EntityManager;

            // Phase 1 fix (BUG-003/BUG-004): disable and untag any pre-existing MainCamera-tagged
            // cameras so Camera.main resolves to our ECS-managed camera, not the Unity scene default.
            var preExistingMainCameras = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var go in preExistingMainCameras)
            {
                var cam = go.GetComponent<Camera>();
                if (cam != null)
                    cam.enabled = false;
                go.tag = "Untagged";
                DebugSettings.LogPlayer($"Disabled and untagged pre-existing MainCamera: '{go.name}' (instanceID={go.GetInstanceID()})");
            }

            // Ensure only one active listener to avoid repeated Unity warning spam.
            var existingListeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var listener in existingListeners)
            {
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }

            // Get camera settings (use default values that match PlayerCameraSettings)
            var cameraSettings = new PlayerCameraSettings
            {
                FirstPersonOffset = new float3(0f, 1.6f, 0f),        // Camera at eye level
                ThirdPersonPivotOffset = new float3(0f, 1.5f, 0f),   // Shoulder height pivot
                ThirdPersonDistance = 3.5f,
                IsThirdPerson = false
            };
            
            // Calculate camera position: player position + first person offset
            // This matches the logic in PlayerCameraSystem
            float3 cameraPosition = playerPosition + cameraSettings.FirstPersonOffset;
            
            // Calculate camera rotation from player view (same formula as PlayerCameraSystem)
            // Combine player yaw rotation with camera pitch rotation
            quaternion playerRotation = quaternion.AxisAngle(math.up(), math.radians(playerView.YawDegrees));
            quaternion pitchRotation = quaternion.AxisAngle(math.right(), math.radians(playerView.PitchDegrees));
            quaternion combinedRotation = math.mul(playerRotation, pitchRotation);

            // Create Unity Camera GameObject (required for rendering)
            var cameraGO = new GameObject("Main Camera (ECS Player)");
            var camera = cameraGO.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            // Set position and rotation based on calculated values
            cameraGO.transform.position = (Vector3)cameraPosition;
            cameraGO.transform.rotation = (Quaternion)combinedRotation;

            // Add AudioListener (required for 3D audio)
            cameraGO.AddComponent<AudioListener>();
            
            // CRITICAL: Create camera entity and link GameObject to it
            var cameraEntity = entityManager.CreateEntity();
            entityManager.SetName(cameraEntity, "Main Camera Entity");
            
            // Add MainCameraTag so camera systems can find it
            entityManager.AddComponent<MainCameraTag>(cameraEntity);
            
            // Add LocalTransform for the camera entity
            entityManager.AddComponentData(cameraEntity, new LocalTransform
            {
                Position = cameraPosition,
                Rotation = combinedRotation,
                Scale = 1f
            });
            
            // Add LocalToWorld
            entityManager.AddComponentData(cameraEntity, new LocalToWorld
            {
                Value = float4x4.TRS(cameraPosition, combinedRotation, new float3(1f))
            });
            
            // CRITICAL: Link the Camera GameObject to the entity so PlayerCameraSystem can update it
            entityManager.AddComponentObject(cameraEntity, camera);

            return cameraEntity;
        }
    }
}

