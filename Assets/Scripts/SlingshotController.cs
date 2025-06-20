using UnityEngine;

public class SlingshotController : MonoBehaviour
{
    [Header("Slingshot Settings")]
    public float maxCharge = 50f;
    public float chargeMultiplier = 2.0f;
    public float powerFactor = 15f;
    public float minChargeThreshold = 5f;
    public float minLookUpAngle = 30f; // Positive angle means looking up

    private bool isCharging = false;
    private float chargeAmount = 0f;
    private Vector3 lockedDirection;
    private Vector3 initialMouseWorldPos;
    private Camera mainCamera;
    private PlayerController playerController;
    private bool playerLocked = false;

    public bool IsCharging => isCharging;

    void Start()
    {
        mainCamera = Camera.main;
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("PlayerController component not found!");
        }

        // Ensure camera is parented to player
        if (mainCamera != null && mainCamera.transform.parent != transform)
        {
            mainCamera.transform.parent = transform;
            mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0); // Adjust height as needed
            mainCamera.transform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        bool leftDown = Input.GetMouseButton(0);
        bool rightDown = Input.GetMouseButton(1);
        bool bothDown = leftDown && rightDown;

        // Check if we're looking up enough
        float lookUpAngle = Vector3.Angle(mainCamera.transform.forward, Vector3.forward);
        bool isLookingUp = lookUpAngle > minLookUpAngle;
        Debug.Log($"Look angle: {lookUpAngle}, Is looking up: {isLookingUp}");

        // Start charging
        if (bothDown && !isCharging && isLookingUp)
        {
            isCharging = true;
            chargeAmount = 0f;
            
            // Get initial mouse position in world space
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                initialMouseWorldPos = ray.GetPoint(distance);
                Debug.Log($"Started charging. Initial mouse world position: {initialMouseWorldPos}");
            }

            // Lock the direction to camera's forward direction
            lockedDirection = mainCamera.transform.forward;
            lockedDirection.Normalize();
            Debug.Log($"Locked direction: {lockedDirection}");

            // Lock player movement
            playerLocked = true;
            if (playerController != null)
            {
                playerController.enabled = false;
                Debug.Log("Player movement locked");
            }
        }

        // Update charge while holding
        if (isCharging && bothDown)
        {
            // Get current mouse position in world space
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 currentMouseWorldPos = ray.GetPoint(distance);
                Vector3 pullVector = initialMouseWorldPos - currentMouseWorldPos; // Pull back = positive charge
                
                // Project pull vector onto the locked direction
                float pullDistance = Vector3.Dot(pullVector, lockedDirection);
                
                // Increase charge based on pull distance
                chargeAmount = Mathf.Clamp(pullDistance * chargeMultiplier, 0f, maxCharge);
                
                Debug.Log($"Charging: Pull Distance = {pullDistance}, Charge = {chargeAmount}");
            }
        }

        // Release and launch
        if (isCharging && (!bothDown || !playerLocked))
        {
            if (chargeAmount > minChargeThreshold)
            {
                Vector3 force = lockedDirection * chargeAmount * powerFactor;
                Debug.Log($"Launching with force: {force} (Charge: {chargeAmount}, Power: {powerFactor})");
                playerController.ApplySlingshotForce(force);
            }
            else
            {
                Debug.Log($"Not enough charge to launch: {chargeAmount} < {minChargeThreshold}");
            }

            // Reset state
            isCharging = false;
            chargeAmount = 0f;
            lockedDirection = Vector3.zero;
            playerLocked = false;

            // Re-enable player movement
            if (playerController != null)
            {
                playerController.enabled = true;
                Debug.Log("Player movement unlocked");
            }
        }
    }

    void OnGUI()
    {
        if (isCharging)
        {
            // Draw charge indicator
            float chargePercentage = chargeAmount / maxCharge;
            float barWidth = 100f;
            float barHeight = 20f;
            float barX = Screen.width / 2 - barWidth / 2;
            float barY = Screen.height - 50f;

            // Background
            GUI.Box(new Rect(barX, barY, barWidth, barHeight), "");
            
            // Charge level
            GUI.Box(new Rect(barX, barY, barWidth * chargePercentage, barHeight), "");
            
            // Text
            GUI.Label(new Rect(barX, barY - 20f, barWidth, 20f), $"Charge: {chargeAmount:F1}");
        }
    }
} 