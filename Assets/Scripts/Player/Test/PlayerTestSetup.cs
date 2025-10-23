using UnityEngine;
using Unity.Entities;
using DOTS.Player.Authoring;

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
        
        [Header("Camera Settings")]
        [Tooltip("Camera field of view")]
        public float cameraFOV = 60f;
        [Tooltip("Camera distance from player")]
        public float cameraDistance = 5f;
        [Tooltip("Camera height above player")]
        public float cameraHeight = 2f;
        
        [Header("Environment")]
        [Tooltip("Create a simple ground plane")]
        public bool createGround = true;
        [Tooltip("Ground plane size")]
        public float groundSize = 50f;
        
        [Header("Debug")]
        [Tooltip("Add debugger to monitor player movement systems")]
        public bool addDebugger = true;
        
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
            
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);
            
            // Use existing material instead of creating new one
            Renderer renderer = ground.GetComponent<Renderer>();
            Material groundMat = Resources.Load<Material>("Materials/TerrainMat");
            if (groundMat == null)
            {
                // Fallback to a simple unlit material
                groundMat = new Material(Shader.Find("Unlit/Color"));
                groundMat.color = new Color(0.5f, 0.7f, 0.3f); // Greenish color
            }
            renderer.material = groundMat;
            
            // Add physics collider (should be there by default with CreatePrimitive)
            if (ground.GetComponent<Collider>() == null)
            {
                ground.AddComponent<BoxCollider>();
            }
        }
        
        private void CreatePlayer()
        {
            Debug.Log("Creating player...");
            
            // Create player GameObject
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1, 0);
            
            // Add capsule collider for physics
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0, 1, 0);
            
            // Add rigidbody for physics
            Rigidbody rb = player.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true; // Prevent player from tipping over
            
            // Add PlayerAuthoring component
            PlayerAuthoring playerAuthoring = player.AddComponent<PlayerAuthoring>();
            playerAuthoring.groundSpeed = groundSpeed;
            playerAuthoring.jumpImpulse = jumpImpulse;
            playerAuthoring.airControl = airControl;
            playerAuthoring.mouseSensitivity = mouseSensitivity;
            playerAuthoring.groundProbeDistance = 1.3f;
            
            // Add a simple visual representation
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerVisual";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = new Vector3(0, 0, 0);
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            
            // Remove the collider from visual (we have one on parent)
            DestroyImmediate(visual.GetComponent<Collider>());
            
            // Add a simple material
            Renderer visualRenderer = visual.GetComponent<Renderer>();
            Material playerMat = new Material(Shader.Find("Unlit/Color"));
            playerMat.color = new Color(0.2f, 0.4f, 0.8f); // Blue color
            visualRenderer.material = playerMat;
        }
        
        private void CreateCamera()
        {
            Debug.Log("Creating camera...");
            
            // Find the player
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player not found! Make sure player is created first.");
                return;
            }
            
            // Create camera
            GameObject cameraObj = new GameObject("PlayerCamera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.fieldOfView = cameraFOV;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            
            // For DOTS system, camera should be positioned at eye level for first-person view
            // The PlayerCameraSystem will handle positioning and rotation
            cameraObj.transform.position = player.transform.position + new Vector3(0, 1.6f, 0);
            cameraObj.transform.rotation = player.transform.rotation;
            
            // Assign camera to player
            PlayerAuthoring playerAuthoring = player.GetComponent<PlayerAuthoring>();
            if (playerAuthoring != null)
            {
                playerAuthoring.playerCamera = camera;
            }
            
            Debug.Log("Camera created and linked to player for DOTS system");
        }
        
        private void SetupLighting()
        {
            Debug.Log("Setting up lighting...");
            
            // Create directional light
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            
            // Position light
            lightObj.transform.rotation = Quaternion.Euler(45f, -45f, 0f);
            
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
            
            // Find and destroy test objects
            GameObject[] objectsToDestroy = {
                GameObject.Find("Ground"),
                GameObject.Find("Player"),
                GameObject.Find("Directional Light")
            };
            
            foreach (GameObject obj in objectsToDestroy)
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
            
            Debug.Log("Test environment cleared.");
        }
        
        private void AddDebugger()
        {
            Debug.Log("Adding player movement debugger...");
            
            // Create debugger GameObject
            GameObject debuggerObj = new GameObject("PlayerMovementDebugger");
            PlayerMovementDebugger debugger = debuggerObj.AddComponent<PlayerMovementDebugger>();
            
            Debug.Log("Player movement debugger added successfully");
        }
    }
}
