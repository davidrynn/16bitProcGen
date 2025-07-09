using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatically sets up the scene for visual testing
/// Adds necessary components and configures the environment
/// </summary>
public class VisualTestSceneSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    public bool setupOnStart = true;
    public bool addCamera = true;
    public bool addLighting = true;
    public bool addVisualTest = true;
    
    [Header("Camera Settings")]
    public Vector3 cameraPosition = new Vector3(0, 15, -10);
    public Vector3 cameraRotation = new Vector3(45, 0, 0);
    public float cameraFOV = 60f;
    
    [Header("Lighting Settings")]
    public Vector3 lightPosition = new Vector3(10, 10, 10);
    public Vector3 lightRotation = new Vector3(45, -45, 0);
    public Color lightColor = Color.white;
    public float lightIntensity = 1.5f;
    
    private void Start()
    {
        if (setupOnStart)
        {
            SetupVisualTestScene();
        }
    }
    
    /// <summary>
    /// Sets up the complete visual test scene
    /// </summary>
    public void SetupVisualTestScene()
    {
        Debug.Log("=== SETTING UP VISUAL TEST SCENE ===");
        
        if (addCamera)
        {
            SetupCamera();
        }
        
        if (addLighting)
        {
            SetupLighting();
        }
        
        if (addVisualTest)
        {
            SetupVisualTest();
        }
        
        Debug.Log("=== VISUAL TEST SCENE SETUP COMPLETE ===");
    }
    
    /// <summary>
    /// Sets up the camera for visual testing
    /// </summary>
    private void SetupCamera()
    {
        Debug.Log("Setting up camera...");
        
        // Find or create main camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraGO = new GameObject("Main Camera");
            mainCamera = cameraGO.AddComponent<Camera>();
            cameraGO.tag = "MainCamera";
        }
        
        // Position and configure camera
        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);
        mainCamera.fieldOfView = cameraFOV;
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        mainCamera.backgroundColor = new Color(0.2f, 0.3f, 0.5f); // Nice blue sky color
        
        // Add camera controller for easy navigation
        var cameraController = mainCamera.gameObject.GetComponent<SimpleCameraController>();
        if (cameraController == null)
        {
            cameraController = mainCamera.gameObject.AddComponent<SimpleCameraController>();
        }
        
        Debug.Log("✓ Camera setup complete");
    }
    
    /// <summary>
    /// Sets up lighting for visual testing
    /// </summary>
    private void SetupLighting()
    {
        Debug.Log("Setting up lighting...");
        
        // Find or create directional light
        Light directionalLight = FindFirstObjectByType<Light>();
        if (directionalLight == null)
        {
            var lightGO = new GameObject("Directional Light");
            directionalLight = lightGO.AddComponent<Light>();
        }
        
        // Configure light
        directionalLight.type = LightType.Directional;
        directionalLight.transform.position = lightPosition;
        directionalLight.transform.rotation = Quaternion.Euler(lightRotation);
        directionalLight.color = lightColor;
        directionalLight.intensity = lightIntensity;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.8f;
        
        // Add ambient light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1.0f);
        RenderSettings.ambientEquatorColor = new Color(0.7f, 0.7f, 0.7f);
        RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);
        RenderSettings.ambientIntensity = 0.3f;
        
        Debug.Log("✓ Lighting setup complete");
    }
    
    /// <summary>
    /// Sets up the visual test component
    /// </summary>
    private void SetupVisualTest()
    {
        Debug.Log("Setting up visual test...");
        
        // Find or create visual test
        SimpleVisualDebugTest visualTest = FindFirstObjectByType<SimpleVisualDebugTest>();
        if (visualTest == null)
        {
            var testGO = new GameObject("Visual Debug Test");
            visualTest = testGO.AddComponent<SimpleVisualDebugTest>();
        }
        
        // Configure test settings
        visualTest.runVisualTestOnStart = true;
        visualTest.testChunkCount = 4; // 2x2 grid
        visualTest.chunkResolution = 32;
        visualTest.chunkSize = 10f;
        visualTest.heightScale = 5f;
        visualTest.showHeightColors = true;
        visualTest.showWireframe = false;
        
        Debug.Log("✓ Visual test setup complete");
    }
    
    /// <summary>
    /// Context menu for manual setup
    /// </summary>
    [ContextMenu("Setup Visual Test Scene")]
    private void SetupScene()
    {
        SetupVisualTestScene();
    }
    
    /// <summary>
    /// Context menu for running the visual test
    /// </summary>
    [ContextMenu("Run Visual Test")]
    private void RunVisualTest()
    {
        var visualTest = FindFirstObjectByType<SimpleVisualDebugTest>();
        if (visualTest != null)
        {
            visualTest.RunVisualDebugTest();
        }
        else
        {
            Debug.LogError("No SimpleVisualDebugTest found in scene!");
        }
    }
}

/// <summary>
/// Simple camera controller for navigating the visual test
/// </summary>
public class SimpleCameraController : MonoBehaviour
{
    [Header("Camera Controls")]
    public float moveSpeed = 10f;
    public float rotateSpeed = 100f;
    public float zoomSpeed = 5f;
    
    private Vector3 lastMousePosition;
    private bool isRotating = false;
    
    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }
    
    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;
        
        if (Input.GetKey(KeyCode.W))
            movement += transform.forward;
        if (Input.GetKey(KeyCode.S))
            movement -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            movement -= transform.right;
        if (Input.GetKey(KeyCode.D))
            movement += transform.right;
        if (Input.GetKey(KeyCode.Q))
            movement += Vector3.up;
        if (Input.GetKey(KeyCode.E))
            movement += Vector3.down;
        
        transform.position += movement * moveSpeed * Time.deltaTime;
    }
    
    private void HandleRotation()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
        }
        
        if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
        }
        
        if (isRotating)
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            transform.Rotate(Vector3.up, delta.x * rotateSpeed * Time.deltaTime);
            transform.Rotate(transform.right, -delta.y * rotateSpeed * Time.deltaTime);
            lastMousePosition = Input.mousePosition;
        }
    }
    
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            transform.position += transform.forward * scroll * zoomSpeed;
        }
    }
} 