using UnityEngine;
using Unity.Entities;
using DOTS.Player;
using DOTS.Player.Systems;

namespace DOTS.Player.Test
{
    /// <summary>
    /// Debug script to monitor and test player movement systems
    /// </summary>
    public class PlayerMovementDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        public bool enableDebugLogging = true;
        public bool showSystemStatus = true;
        public bool showComponentData = true;
        public float debugUpdateInterval = 0.5f;
        
        [Header("System Monitoring")]
        public bool monitorInputSystem = true;
        public bool monitorGroundingSystem = true;
        public bool monitorMovementSystem = true;
        public bool monitorCameraSystem = true;
        
        private float lastDebugTime;
        private World dotsWorld;
        private Entity playerEntity;
        
        void Start()
        {
            Debug.Log("=== PLAYER MOVEMENT DEBUGGER STARTED ===");
            
            // Wait for DOTS world to initialize
            Invoke(nameof(InitializeDebugger), 1f);
        }
        
        void InitializeDebugger()
        {
            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                Debug.LogWarning("DOTS World not available yet - will retry later");
                return;
            }
            
            Debug.Log("DOTS World found - initializing player movement debugger");
            FindPlayerEntity();
        }
        
        void FindPlayerEntity()
        {
            var entityManager = dotsWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(PlayerMovementConfig));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                playerEntity = entities[0];
                Debug.Log($"Player entity found: {playerEntity}");
            }
            else
            {
                Debug.LogWarning("No player entity found with PlayerMovementConfig");
                
                // Debug: Check what entities exist
                var allEntities = entityManager.GetAllEntities(Unity.Collections.Allocator.Temp);
                Debug.Log($"Total entities in world: {allEntities.Length}");
                
                // Check for any player-related components
                var playerInputQuery = entityManager.CreateEntityQuery(typeof(PlayerInputComponent));
                var playerInputEntities = playerInputQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Debug.Log($"Entities with PlayerInputComponent: {playerInputEntities.Length}");
                
                var playerStateQuery = entityManager.CreateEntityQuery(typeof(PlayerMovementState));
                var playerStateEntities = playerStateQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Debug.Log($"Entities with PlayerMovementState: {playerStateEntities.Length}");
                
                allEntities.Dispose();
                playerInputEntities.Dispose();
                playerStateEntities.Dispose();
            }
            
            entities.Dispose();
        }
        
        void Update()
        {
            // Retry initialization if world wasn't available
            if (dotsWorld == null)
            {
                InitializeDebugger();
            }
            
            if (Time.time - lastDebugTime >= debugUpdateInterval)
            {
                if (enableDebugLogging && dotsWorld != null)
                {
                    LogSystemStatus();
                }
                
                if (showComponentData && playerEntity != Entity.Null && dotsWorld != null)
                {
                    LogComponentData();
                }
                
                lastDebugTime = Time.time;
            }
        }
        
        void LogSystemStatus()
        {
            if (!showSystemStatus || dotsWorld == null) return;
            
            Debug.Log("=== PLAYER MOVEMENT SYSTEM STATUS ===");
            
            try
            {
                if (monitorInputSystem)
                {
                    var inputSystem = dotsWorld.GetExistingSystem<PlayerInputSystem>();
                    Debug.Log($"Input System: {(inputSystem != SystemHandle.Null ? "✅ Active" : "❌ Not Found")}");
                }
                
                if (monitorGroundingSystem)
                {
                    var groundingSystem = dotsWorld.GetExistingSystem<PlayerGroundingSystem>();
                    Debug.Log($"Grounding System: {(groundingSystem != SystemHandle.Null ? "✅ Active" : "❌ Not Found")}");
                }
                
                if (monitorMovementSystem)
                {
                    var movementSystem = dotsWorld.GetExistingSystem<PlayerMovementSystem>();
                    Debug.Log($"Movement System: {(movementSystem != SystemHandle.Null ? "✅ Active" : "❌ Not Found")}");
                }
                
                if (monitorCameraSystem)
                {
                    var cameraSystem = dotsWorld.GetExistingSystem<PlayerCameraSystem>();
                    Debug.Log($"Camera System: {(cameraSystem != SystemHandle.Null ? "✅ Active" : "❌ Not Found")}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error checking system status: {e.Message}");
            }
        }
        
        void LogComponentData()
        {
            if (!showComponentData || playerEntity == Entity.Null) return;
            
            var entityManager = dotsWorld.EntityManager;
            
            Debug.Log("=== PLAYER COMPONENT DATA ===");
            
            // Log input data
            if (entityManager.HasComponent<PlayerInputComponent>(playerEntity))
            {
                var input = entityManager.GetComponentData<PlayerInputComponent>(playerEntity);
                Debug.Log($"Input - Move: {input.Move}, Look: {input.Look}, Jump: {input.JumpPressed}");
            }
            
            // Log movement state
            if (entityManager.HasComponent<PlayerMovementState>(playerEntity))
            {
                var state = entityManager.GetComponentData<PlayerMovementState>(playerEntity);
                Debug.Log($"State - Mode: {state.Mode}, Grounded: {state.IsGrounded}, FallTime: {state.FallTime:F2}");
            }
            
            // Log view data
            if (entityManager.HasComponent<PlayerViewComponent>(playerEntity))
            {
                var view = entityManager.GetComponentData<PlayerViewComponent>(playerEntity);
                Debug.Log($"View - Yaw: {view.YawDegrees:F1}°, Pitch: {view.PitchDegrees:F1}°");
            }
            
            // Log transform data
            if (entityManager.HasComponent<Unity.Transforms.LocalTransform>(playerEntity))
            {
                var transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(playerEntity);
                Debug.Log($"Transform - Position: {transform.Position}, Rotation: {transform.Rotation}");
            }
            
            // Log physics velocity
            if (entityManager.HasComponent<Unity.Physics.PhysicsVelocity>(playerEntity))
            {
                var velocity = entityManager.GetComponentData<Unity.Physics.PhysicsVelocity>(playerEntity);
                Debug.Log($"Physics - Linear: {velocity.Linear}, Angular: {velocity.Angular}");
            }
        }
        
        [ContextMenu("Force Debug Update")]
        public void ForceDebugUpdate()
        {
            lastDebugTime = 0f; // Force immediate update
        }
        
        [ContextMenu("Find Player Entity")]
        public void FindPlayerEntityManual()
        {
            FindPlayerEntity();
        }
        
        void OnGUI()
        {
            if (!enableDebugLogging) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Player Movement Debugger", GUI.skin.box);
            
            if (playerEntity != Entity.Null)
            {
                GUILayout.Label($"Player Entity: {playerEntity}");
                
                if (GUILayout.Button("Force Debug Update"))
                {
                    ForceDebugUpdate();
                }
                
                if (GUILayout.Button("Find Player Entity"))
                {
                    FindPlayerEntityManual();
                }
            }
            else
            {
                GUILayout.Label("No player entity found");
            }
            
            GUILayout.EndArea();
        }
    }
}
