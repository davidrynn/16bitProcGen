using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

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
       // Player spawn configuration
        private const float PlayerStartHeight = 5f; // Adjust this value to change spawn height

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
            CreateGroundPlane(ref state);
            // Camera is created and linked in CreatePlayerEntity
            
            _hasSpawned = true;
            state.Enabled = false; // Disable after spawning
        }

        private Entity CreatePlayerEntity(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var entity = entityManager.CreateEntity();

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
                FallTime = 0f
            });

            entityManager.AddComponentData(entity, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });

            // Add transform
            entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = new float3(0, 2, 0), // Start 2 units above ground
                Rotation = quaternion.identity,
                Scale = 1f
            });

            entityManager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.TRS(
                    new float3(0, 2, 0),
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
            var playerPosition = new float3(0, 2, 0); // Start 2 units above ground
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
                entityManager.AddComponentData(entity, new PlayerCameraSettings
                {
                    FirstPersonOffset = new float3(0f, 1.6f, 0f),
                    ThirdPersonPivotOffset = new float3(0f, 1.5f, 0f),
                    ThirdPersonDistance = 3.5f,
                    IsThirdPerson = false
                });

                Debug.LogWarning("[PlayerBootstrap] Failed to create camera entity - camera system may not work");
            }

            // Create visual representation (minimal hybrid - GameObject sync)
            CreatePlayerVisual(entity, new float3(0, 2, 0));
            
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

        private void CreateGroundPlane(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var groundEntity = entityManager.CreateEntity();
            entityManager.SetName(groundEntity, "Ground Plane (ECS Physics Only)");

            // Transform
            var groundPosition = float3.zero;
            var groundTransform = new LocalTransform
            {
                Position = groundPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            };
            entityManager.AddComponentData(groundEntity, groundTransform);
            entityManager.AddComponentData(groundEntity, new LocalToWorld
            {
                Value = float4x4.TRS(groundPosition, quaternion.identity, new float3(1f))
            });

            // Physics collider (static box for ground)
            var groundSize = new float2(50f, 50f); // Large ground plane
            var boxGeometry = new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(groundSize.x, 0.1f, groundSize.y), // Thin box
                BevelRadius = 0f
            };

            entityManager.AddComponentData(groundEntity, new PhysicsCollider
            {
                Value = Unity.Physics.BoxCollider.Create(
                    boxGeometry,
                    new CollisionFilter
                    {
                        BelongsTo = 2u,    // Ground layer
                        CollidesWith = ~0u, // Collides with everything
                        GroupIndex = 0
                    },
                    Unity.Physics.Material.Default
                )
            });

            // Mark as static (no physics mass needed)
            entityManager.AddComponent<PhysicsWorldIndex>(groundEntity);
        }

        private void CreateGroundVisual(float3 position, float2 size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground Visual (ECS Synced)";
            ground.transform.position = position;
            ground.transform.localScale = new Vector3(size.x, 0.1f, size.y);

            // Make it look like ground (greenish)
            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.3f, 0.5f, 0.3f); // Green ground
            }

            // Remove GameObject collider - physics handled by ECS
            Object.Destroy(ground.GetComponent<UnityEngine.Collider>());

        }

        private Entity CreateMainCameraAndEntity(ref SystemState state, Entity playerEntity, float3 playerPosition, PlayerViewComponent playerView)
        {
            var entityManager = state.EntityManager;
            
            // Check if main camera already exists
            var existingCamera = Camera.main;
            if (existingCamera != null)
            {
                // If camera exists, we might not have its entity, so we'll still create one
                // This handles the case where camera exists but wasn't baked
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

