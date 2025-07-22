using UnityEngine;

/// <summary>
/// Simple free-flying camera controller for terrain testing
/// Allows easy exploration of generated terrain
/// </summary>
public class TerrainTestCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 50f;
    public float fastMoveSpeed = 100f;
    public float mouseSensitivity = 3f;
    public float maxLookAngle = 90f;
    
    [Header("Camera Settings")]
    public bool lockCursor = true;
    public bool showCursorOnEscape = true;
    
    [Header("Debug Info")]
    public bool showDebugInfo = true;
    public bool showPosition = true;
    public bool showFPS = true;
    
    private Vector2 rotation = Vector2.zero;
    private float currentMoveSpeed;
    private float deltaTime;
    private float fps;
    
    void Start()
    {
        // Set initial rotation
        rotation.x = transform.eulerAngles.y;
        rotation.y = -transform.eulerAngles.x;
        
        // Lock cursor for mouse look
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        currentMoveSpeed = moveSpeed;
    }
    
    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleCursorToggle();
        UpdateDebugInfo();
    }
    
    void HandleMouseLook()
    {
        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        rotation.x += mouseX;
        rotation.y -= mouseY;
        rotation.y = Mathf.Clamp(rotation.y, -maxLookAngle, maxLookAngle);
        
        transform.rotation = Quaternion.Euler(rotation.y, rotation.x, 0);
    }
    
    void HandleMovement()
    {
        // Fast movement with Shift
        currentMoveSpeed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
        
        // WASD movement
        float moveX = Input.GetAxis("Horizontal") * currentMoveSpeed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * currentMoveSpeed * Time.deltaTime;
        
        // QE for up/down movement
        float moveY = 0;
        if (Input.GetKey(KeyCode.E)) moveY += currentMoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) moveY -= currentMoveSpeed * Time.deltaTime;
        
        // Apply movement
        Vector3 move = transform.right * moveX + transform.forward * moveZ + Vector3.up * moveY;
        transform.position += move;
    }
    
    void HandleCursorToggle()
    {
        if (!showCursorOnEscape) return;
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    void UpdateDebugInfo()
    {
        if (!showDebugInfo) return;
        
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
    } 
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(20, 20, 350, 250));
        GUILayout.Label("=== TERRAIN TEST CAMERA ===");
        
        if (showPosition)
        {
            GUILayout.Label($"Position: {transform.position}");
            GUILayout.Label($"Rotation: {transform.eulerAngles}");
        }
        
        if (showFPS)
        {
            GUILayout.Label($"FPS: {fps:F1}");
        }
        
        GUILayout.Label("=== CONTROLS ===");
        GUILayout.Label("WASD: Move");
        GUILayout.Label("Mouse: Look");
        GUILayout.Label("Q/E: Up/Down");
        GUILayout.Label("Shift: Fast Move");
        GUILayout.Label("Escape: Toggle Cursor");
        GUILayout.Label("Space: Force Terrain Gen");
        
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// Reset camera to a good starting position for terrain viewing
    /// </summary>
    [ContextMenu("Reset to Terrain View")]
    public void ResetToTerrainView()
    {
        transform.position = new Vector3(0, 50, 0);
        rotation = Vector2.zero;
        transform.rotation = Quaternion.identity;
    }
    
    /// <summary>
    /// Teleport to a specific position
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
    }
} 