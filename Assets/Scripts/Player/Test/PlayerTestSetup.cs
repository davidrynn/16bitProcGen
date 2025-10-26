using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using PhysicsMaterial = Unity.Physics.Material;

namespace DOTS.Player.Test
{
    /// <summary>
    /// Quick setup script to create a playable test environment with player movement
    /// </summary>
    public class PlayerTestSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [Tooltip("Automatically set up test environment on start")]
        public bool setupOnStart = true;
        
        [Header("Player Settings")]
        [Tooltip("Ground speed for player movement")]
        public float groundSpeed = 10f;
        [Tooltip("Jump impulse strength")]
        public float jumpImpulse = 5f;
        [Tooltip("Air control factor (0-1)")]
        [Range(0f, 1f)] public float airControl = 0.2f;
        [Tooltip("Mouse sensitivity for camera")]
        public float mouseSensitivity = 0.1f;
        [Tooltip("Maximum look pitch in degrees")]
        public float maxPitchDegrees = 85f;
        [Tooltip("Raycast distance for ground probing")]
        public float groundProbeDistance = 1.3f;
        [Tooltip("Slingshot impulse strength (future mode)")]
        public float slingshotImpulse = 30f;
        [Tooltip("Swim speed (future mode)")]
        public float swimSpeed = 6f;
        [Tooltip("Zero-G damping factor (future mode)")]
        public float zeroGDamping = 2f;
        
        [Header("Camera Settings")]
        [Tooltip("Camera field of view")]
        public float cameraFOV = 60f;
        [Tooltip("Camera distance from player")]
        public float cameraDistance = 0f;
        [Tooltip("Camera height above player")]
        public float cameraHeight = 1.6f;
        
        [Header("Environment")]
        [Tooltip("Create a simple ground plane")]
        public bool createGround = true;
        [Tooltip("Ground plane size")]
        public float groundSize = 50f;
        
        [Header("Debug")]
        [Tooltip("Add debugger to monitor player movement systems")]
        public bool addDebugger = true;

        private World dotsWorld;
        private EntityManager entityManager;
        private Entity playerEntity = Entity.Null;
        private Entity groundEntity = Entity.Null;
        private Entity cameraEntity = Entity.Null;
        private BlobAssetReference<Unity.Physics.Collider> playerCollider;
        private BlobAssetReference<Unity.Physics.Collider> groundCollider;
        private GameObject cameraObject;
        private GameObject groundVisual;
        private GameObject playerVisual;
        private UnityEngine.Material playerMaterial;
        private GameObject debuggerObject;
        private GameObject lightingObject;
        
        private void Start()
        {
             // Disable rendering debug to suppress dungeon system error messages
            DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug = false;

            if (setupOnStart)
            {
                SetupTestEnvironment();
            }
        }
        
        [ContextMenu("Setup Test Environment")]
        public void SetupTestEnvironment()
        {
            Debug.Log("=== SETTING UP PLAYER TEST ENVIRONMENT ===");

            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                Debug.LogError("DOTS World not available. Open a SubScene or ensure Entities bootstrap has run before calling Setup.");
                return;
            }

            entityManager = dotsWorld.EntityManager;

            ClearTestEnvironmentInternal();
            EnsurePhysicsStepExists();
            
            // Create ground if requested
            if (createGround)
            {
                CreateGround();
            }
            
            // Create player
            CreatePlayer();
            
            // Create camera
            CreateCamera();
            
            // Add lighting
            SetupLighting();
            
            // Add debugger if requested
            if (addDebugger)
            {
                AddDebugger();
            }
            
            Debug.Log("=== PLAYER TEST ENVIRONMENT READY ===");
            Debug.Log("Controls: WASD to move, Space to jump, Mouse to look around");
        }
        
        private void CreateGround()
        {
            Debug.Log("Creating ground plane...");

            groundVisual = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundVisual.name = "Ground";
            groundVisual.transform.position = Vector3.zero;
            groundVisual.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);
            
            Renderer renderer = groundVisual.GetComponent<Renderer>();
            UnityEngine.Material groundMat = Resources.Load<UnityEngine.Material>("Materials/TerrainMat");
            if (groundMat == null)
            {
                groundMat = new UnityEngine.Material(Shader.Find("Unlit/Color"));
                groundMat.color = new Color(0.5f, 0.7f, 0.3f);
            }
            renderer.material = groundMat;

            var groundGeometry = new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(groundSize, 0.1f, groundSize),
                Orientation = quaternion.identity,
                BevelRadius = 0f
            };

            groundCollider = Unity.Physics.BoxCollider.Create(groundGeometry, CollisionFilter.Default, PhysicsMaterial.Default);
            groundEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(groundEntity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            entityManager.AddComponent<LocalToWorld>(groundEntity);
            entityManager.AddComponentData(groundEntity, new PhysicsCollider { Value = groundCollider });
            entityManager.AddSharedComponent(groundEntity, new PhysicsWorldIndex());
        }
        
        private void CreatePlayer()
        {
            Debug.Log("Creating player...");

            var startPosition = new float3(0f, 1f, 0f);
            var cameraOffset = new float3(0f, math.max(0.1f, cameraHeight - startPosition.y), -cameraDistance);
            var capsuleGeometry = new CapsuleGeometry
            {
                Vertex0 = new float3(0f, -0.5f, 0f),
                Vertex1 = new float3(0f, 0.5f, 0f),
                Radius = 0.5f
            };

            playerCollider = Unity.Physics.CapsuleCollider.Create(capsuleGeometry, CollisionFilter.Default, PhysicsMaterial.Default);

            playerEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(playerEntity, LocalTransform.FromPositionRotationScale(startPosition, quaternion.identity, 1f));
            entityManager.AddComponent<LocalToWorld>(playerEntity);
            entityManager.AddComponentData(playerEntity, new PhysicsCollider { Value = playerCollider });
            entityManager.AddComponentData(playerEntity, PhysicsMass.CreateDynamic(playerCollider.Value.MassProperties, 70f));
            entityManager.AddComponentData(playerEntity, PhysicsVelocity.Zero);
            entityManager.AddComponentData(playerEntity, new PhysicsDamping { Linear = 0f, Angular = 0f });
            entityManager.AddComponentData(playerEntity, new PhysicsGravityFactor { Value = 1f });
            entityManager.AddSharedComponent(playerEntity, new PhysicsWorldIndex());

            entityManager.AddComponentData(playerEntity, new PlayerMovementConfig
            {
                GroundSpeed = groundSpeed,
                JumpImpulse = jumpImpulse,
                AirControl = airControl,
                SlingshotImpulse = slingshotImpulse,
                SwimSpeed = swimSpeed,
                ZeroGDamping = zeroGDamping,
                MouseSensitivity = mouseSensitivity,
                MaxPitchDegrees = maxPitchDegrees,
                GroundProbeDistance = math.max(0.1f, groundProbeDistance)
            });

            entityManager.AddComponentData(playerEntity, new PlayerInputComponent
            {
                Move = float2.zero,
                Look = float2.zero,
                JumpPressed = false
            });

            entityManager.AddComponentData(playerEntity, new PlayerMovementState
            {
                Mode = PlayerMovementMode.Ground,
                IsGrounded = false,
                FallTime = 0f
            });

            entityManager.AddComponentData(playerEntity, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });

            entityManager.AddComponentData(playerEntity, new PlayerCameraSettings
            {
                Offset = cameraOffset
            });

            CreatePlayerVisual(startPosition);
        }
        
        private void CreateCamera()
        {
            Debug.Log("Creating camera...");

            if (!entityManager.Exists(playerEntity))
            {
                Debug.LogError("Player entity not initialized before camera creation.");
                return;
            }

            cameraObject = new GameObject("PlayerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = cameraFOV;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var cameraSettings = entityManager.GetComponentData<PlayerCameraSettings>(playerEntity);
            var cameraWorldPosition = playerTransform.Position + cameraSettings.Offset;

            cameraObject.transform.position = new Vector3(cameraWorldPosition.x, cameraWorldPosition.y, cameraWorldPosition.z);
            cameraObject.transform.rotation = Quaternion.identity;

            cameraEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(cameraEntity, LocalTransform.FromPositionRotationScale(cameraWorldPosition, quaternion.identity, 1f));
            entityManager.AddComponent<LocalToWorld>(cameraEntity);
            entityManager.AddComponentObject(cameraEntity, camera);

            entityManager.AddComponentData(playerEntity, new PlayerCameraLink
            {
                CameraEntity = cameraEntity
            });

            Debug.Log("Camera created and linked to player for DOTS system");
        }
        
        private void SetupLighting()
        {
            Debug.Log("Setting up lighting...");
            
            // Create directional light
            lightingObject = new GameObject("Directional Light");
            Light light = lightingObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            
            // Position light
            lightingObject.transform.rotation = Quaternion.Euler(45f, -45f, 0f);
            
            // Set ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
        }
        
        [ContextMenu("Clear Test Environment")]
        public void ClearTestEnvironment()
        {
            Debug.Log("Clearing test environment...");

            ClearTestEnvironmentInternal();

            DestroyUnityObject(lightingObject);
            lightingObject = null;

            Debug.Log("Test environment cleared.");
        }
        
        private void AddDebugger()
        {
            Debug.Log("Adding player movement debugger...");
            
            // Create debugger GameObject
            debuggerObject = new GameObject("PlayerMovementDebugger");
            debuggerObject.AddComponent<PlayerMovementDebugger>();
            
            Debug.Log("Player movement debugger added successfully");
        }

        private void ClearTestEnvironmentInternal()
        {
            if (dotsWorld != null && dotsWorld.IsCreated)
            {
                var manager = dotsWorld.EntityManager;
                if (manager.Exists(playerEntity))
                {
                    manager.DestroyEntity(playerEntity);
                    playerEntity = Entity.Null;
                }

                if (manager.Exists(groundEntity))
                {
                    manager.DestroyEntity(groundEntity);
                    groundEntity = Entity.Null;
                }

                if (manager.Exists(cameraEntity))
                {
                    manager.DestroyEntity(cameraEntity);
                    cameraEntity = Entity.Null;
                }
            }

            if (playerCollider.IsCreated)
            {
                playerCollider.Dispose();
                playerCollider = default;
            }

            if (groundCollider.IsCreated)
            {
                groundCollider.Dispose();
                groundCollider = default;
            }

            DestroyUnityObject(groundVisual);
            groundVisual = null;

            DestroyUnityObject(playerVisual);
            playerVisual = null;

            DestroyUnityObject(playerMaterial);
            playerMaterial = null;

            DestroyUnityObject(cameraObject);
            cameraObject = null;

            DestroyUnityObject(debuggerObject);
            debuggerObject = null;

            DestroyUnityObject(lightingObject);
            lightingObject = null;
        }

        private void OnDestroy()
        {
            ClearTestEnvironmentInternal();
        }

        private void DestroyUnityObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
#else
            Destroy(obj);
#endif
        }

        private void CreatePlayerVisual(float3 startPosition)
        {
            playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerVisual.name = "PlayerVisual";
            playerVisual.transform.position = new Vector3(startPosition.x, startPosition.y, startPosition.z);

            var collider = playerVisual.GetComponent<UnityEngine.Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
#else
                Destroy(collider);
#endif
            }

            var renderer = playerVisual.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogWarning("PlayerTestSetup: Unable to find a suitable shader for the player visual. Visual will use default material.");
                playerMaterial = null;
            }
            else
            {
                playerMaterial = new UnityEngine.Material(shader);

                if (playerMaterial.HasProperty("_BaseColor"))
                {
                    playerMaterial.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.8f));
                }
                else if (playerMaterial.HasProperty("_Color"))
                {
                    playerMaterial.SetColor("_Color", new Color(0.2f, 0.4f, 0.8f));
                }

                renderer.material = playerMaterial;
            }

            var follower = playerVisual.AddComponent<EntityVisualFollower>();
            follower.Initialize(dotsWorld, playerEntity);
        }

        private void EnsurePhysicsStepExists()
        {
            using var physicsStepQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsStep>());
            if (physicsStepQuery.IsEmpty)
            {
                var physicsStepEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(physicsStepEntity, PhysicsStep.Default);
                Debug.Log("PhysicsStep singleton created for runtime physics simulation.");
            }
        }

        private class EntityVisualFollower : MonoBehaviour
        {
            private World world;
            private Entity entity;

            public void Initialize(World targetWorld, Entity targetEntity)
            {
                world = targetWorld;
                entity = targetEntity;
            }

            private void Update()
            {
                if (world == null || !world.IsCreated)
                {
                    return;
                }

                var manager = world.EntityManager;
                if (!manager.Exists(entity))
                {
                    return;
                }

                if (manager.HasComponent<LocalTransform>(entity))
                {
                    var localTransform = manager.GetComponentData<LocalTransform>(entity);
                    transform.position = localTransform.Position;
                    transform.rotation = localTransform.Rotation;
                    transform.localScale = Vector3.one * localTransform.Scale;
                }
                else if (manager.HasComponent<LocalToWorld>(entity))
                {
                    var ltw = manager.GetComponentData<LocalToWorld>(entity);
                    transform.position = ltw.Position;
                    transform.rotation = Quaternion.LookRotation(ltw.Forward, ltw.Up);
                    transform.localScale = Vector3.one;
                }
            }
        }
    }
}
