using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using Unity.Collections;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Bootstrap with visual representation - spawns player and camera entities with rendering.
    /// This version creates a Unity Camera GameObject that follows the camera entity.
    /// </summary>
    public class PlayerCameraBootstrap_WithVisuals : MonoBehaviour
    {
        [Header("Initial Positions")]
        [SerializeField] private Vector3 playerStartPosition = new Vector3(0, 10, 0);
        [SerializeField] private Vector3 cameraStartPosition = new Vector3(0, 3, -4);

        [Header("Visual Representation")]
        [SerializeField] private Mesh playerMesh; // Assign a cube or sphere mesh
        [SerializeField] private UnityEngine.Material playerMaterial; // Assign a material
        [SerializeField] private bool createPlayerVisuals = true;

        [Header("Ground Plane")]
        [SerializeField] private Vector3 groundPosition = new Vector3(0, 0, 0);
        [SerializeField] private Vector2 groundSize = new Vector2(20f, 20f); // Width and depth
        [SerializeField] private bool createGroundVisuals = true;

        [Header("Physics")]
        [SerializeField] private float playerMass = 70f;
        [SerializeField] private float playerHeight = 2f;
        [SerializeField] private float playerRadius = 0.5f;

        private Camera mainCamera;
        private Entity cameraEntity;
        private BlobAssetReference<Unity.Physics.Collider> playerCollider;
        private BlobAssetReference<Unity.Physics.Collider> groundCollider;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            // ===========================================
            // 1. Create Ground Plane Entity (with physics)
            // ===========================================
            CreateGroundPlane(entityManager);

            // ===========================================
            // 2. Create Player Entity (with physics and optional mesh)
            // ===========================================
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
            entityManager.AddComponentData(playerEntity, new LocalToWorld
            {
                Value = float4x4.TRS(playerTransform.Position, playerTransform.Rotation, new float3(playerTransform.Scale))
            });

            // Add input components for mouse/keyboard control
            entityManager.AddComponentData(playerEntity, new PlayerInputComponent
            {
                Move = float2.zero,
                Look = float2.zero,
                JumpPressed = false
            });

            entityManager.AddComponentData(playerEntity, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });

            entityManager.AddComponentData(playerEntity, new PlayerMovementConfig
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

            // Add physics components to player
            AddPlayerPhysics(entityManager, playerEntity);

            // Optional: Add visual representation for player
            if (createPlayerVisuals)
            {
                CreatePlayerVisualGameObject(playerEntity, playerTransform);
            }

            Debug.Log($"[PlayerCameraBootstrap] Player entity spawned at {playerStartPosition} with physics");

            // ===========================================
            // 3. Create Camera Entity
            // ===========================================
            cameraEntity = entityManager.CreateEntity();
            entityManager.SetName(cameraEntity, "MainCamera (Runtime)");
            
            entityManager.AddComponent<MainCameraTag>(cameraEntity);
            
            var cameraTransform = new LocalTransform
            {
                Position = cameraStartPosition,
                Rotation = quaternion.LookRotation(math.normalize(playerStartPosition - cameraStartPosition), math.up()),
                Scale = 1f
            };
            entityManager.AddComponentData(cameraEntity, cameraTransform);
            entityManager.AddComponentData(cameraEntity, new LocalToWorld
            {
                Value = float4x4.TRS(cameraTransform.Position, cameraTransform.Rotation, new float3(cameraTransform.Scale))
            });

            Debug.Log($"[PlayerCameraBootstrap] Camera entity spawned at {cameraStartPosition}");

            // ===========================================
            // 4. Create Unity Camera GameObject (for rendering)
            // ===========================================
            CreateCameraGameObject();

            Debug.Log("[PlayerCameraBootstrap] CameraFollowSystem will automatically start following player");
        }

        private void CreatePlayerVisualGameObject(Entity playerEntity, LocalTransform transform)
        {
            // Create a simple cube to represent the player
            var playerGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerGO.name = "Player Visual (Debug)";
            playerGO.transform.position = transform.Position;
            playerGO.transform.rotation = transform.Rotation;
            playerGO.transform.localScale = new Vector3(1f, 2f, 1f); // Make it look like a character

            // Apply material if provided
            if (playerMaterial != null)
            {
                playerGO.GetComponent<Renderer>().material = playerMaterial;
            }
            else
            {
                // Default blue color for player
                playerGO.GetComponent<Renderer>().material.color = Color.blue;
            }

            // Store entity reference (for syncing in Update if needed)
            var sync = playerGO.AddComponent<EntityVisualSync>();
            sync.entity = playerEntity;
            
            Debug.Log("[PlayerCameraBootstrap] Created player visual GameObject");
        }

        private void CreateCameraGameObject()
        {
            // Create a standard Unity Camera GameObject
            var cameraGO = new GameObject("Main Camera (GameObject)");
            mainCamera = cameraGO.AddComponent<Camera>();
            
            // Configure camera
            mainCamera.tag = "MainCamera";
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.nearClipPlane = 0.3f;
            mainCamera.farClipPlane = 1000f;
            
            // Position it at the camera entity's position
            var world = World.DefaultGameObjectInjectionWorld;
            var cameraTransform = world.EntityManager.GetComponentData<LocalTransform>(cameraEntity);
            cameraGO.transform.position = cameraTransform.Position;
            cameraGO.transform.rotation = cameraTransform.Rotation;

            // Add AudioListener (required for 3D audio)
            cameraGO.AddComponent<AudioListener>();

            Debug.Log("[PlayerCameraBootstrap] Created Camera GameObject for rendering");
        }

        private void LateUpdate()
        {
            // Sync the Camera GameObject with the camera entity
            if (mainCamera != null && cameraEntity != Entity.Null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.EntityManager.Exists(cameraEntity))
                {
                    var cameraTransform = world.EntityManager.GetComponentData<LocalTransform>(cameraEntity);
                    mainCamera.transform.position = cameraTransform.Position;
                    mainCamera.transform.rotation = cameraTransform.Rotation;
                }
            }
        }

        private void CreateGroundPlane(EntityManager entityManager)
        {
            var groundEntity = entityManager.CreateEntity();
            entityManager.SetName(groundEntity, "Ground Plane (Runtime)");

            // Transform
            var groundTransform = new LocalTransform
            {
                Position = groundPosition,
                Rotation = quaternion.identity,
                Scale = 1f
            };
            entityManager.AddComponentData(groundEntity, groundTransform);
            entityManager.AddComponentData(groundEntity, new LocalToWorld
            {
                Value = float4x4.TRS(groundTransform.Position, groundTransform.Rotation, new float3(groundTransform.Scale))
            });

            // Physics collider (static box)
            var boxGeometry = new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(groundSize.x, 0.1f, groundSize.y), // Thin box for ground
                BevelRadius = 0f
            };

            groundCollider = Unity.Physics.BoxCollider.Create(boxGeometry, CollisionFilter.Default);
            entityManager.AddComponentData(groundEntity, new PhysicsCollider { Value = groundCollider });
            entityManager.AddSharedComponent(groundEntity, new PhysicsWorldIndex());

            Debug.Log($"[PlayerCameraBootstrap] Ground plane created at {groundPosition} with size {groundSize}");

            // Optional: Create visual representation
            if (createGroundVisuals)
            {
                CreateGroundVisualGameObject(groundTransform);
            }
        }

        private void AddPlayerPhysics(EntityManager entityManager, Entity playerEntity)
        {
            // Create capsule collider for player
            var capsuleGeometry = new CapsuleGeometry
            {
                Vertex0 = new float3(0f, -playerHeight * 0.5f + playerRadius, 0f),
                Vertex1 = new float3(0f, playerHeight * 0.5f - playerRadius, 0f),
                Radius = playerRadius
            };

            playerCollider = Unity.Physics.CapsuleCollider.Create(capsuleGeometry, CollisionFilter.Default);
            entityManager.AddComponentData(playerEntity, new PhysicsCollider { Value = playerCollider });

            // Add physics velocity
            entityManager.AddComponentData(playerEntity, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });

            // Add physics mass
            entityManager.AddComponentData(playerEntity, PhysicsMass.CreateDynamic(
                MassProperties.UnitSphere,
                playerMass
            ));

            // Add gravity factor (1.0 = normal gravity)
            entityManager.AddComponentData(playerEntity, new PhysicsGravityFactor { Value = 1f });

            // Add to physics world
            entityManager.AddSharedComponent(playerEntity, new PhysicsWorldIndex());

            Debug.Log($"[PlayerCameraBootstrap] Physics components added to player (mass: {playerMass}kg)");
        }

        private void CreateGroundVisualGameObject(LocalTransform transform)
        {
            var groundGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundGO.name = "Ground Plane Visual (Debug)";
            groundGO.transform.position = transform.Position;
            groundGO.transform.rotation = transform.Rotation;
            groundGO.transform.localScale = new Vector3(groundSize.x, 0.1f, groundSize.y);

            // Make it look like ground
            var renderer = groundGO.GetComponent<Renderer>();
            renderer.material.color = new Color(0.3f, 0.5f, 0.3f); // Greenish ground

            // Remove the collider since physics is handled by DOTS
            Destroy(groundGO.GetComponent<UnityEngine.Collider>());

            Debug.Log("[PlayerCameraBootstrap] Created ground visual GameObject");
        }

        private void OnDestroy()
        {
            // Clean up camera GameObject when bootstrap is destroyed
            if (mainCamera != null)
            {
                Destroy(mainCamera.gameObject);
            }

            // Dispose physics colliders
            if (playerCollider.IsCreated)
            {
                playerCollider.Dispose();
            }
            if (groundCollider.IsCreated)
            {
                groundCollider.Dispose();
            }
        }
    }

    /// <summary>
    /// Helper component to sync GameObject visuals with DOTS entities
    /// </summary>
    public class EntityVisualSync : MonoBehaviour
    {
        public Entity entity;
        private bool hasLoggedFirstUpdate = false;

        private void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            
            if (world == null)
            {
                return;
            }
            
            if (entity == Entity.Null)
            {
                return;
            }
            
            if (!world.EntityManager.Exists(entity))
            {
                return;
            }
            
            if (!world.EntityManager.HasComponent<LocalTransform>(entity))
            {
                return;
            }
            
            var transform = world.EntityManager.GetComponentData<LocalTransform>(entity);
            this.transform.position = transform.Position;
            this.transform.rotation = transform.Rotation;
            
            if (!hasLoggedFirstUpdate)
            {
                Debug.Log($"[EntityVisualSync] First successful sync! Entity {entity.Index} at {transform.Position}");
                hasLoggedFirstUpdate = true;
            }
        }
    }
}

