using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 60f;
    public float jumpForce = 10f;
    public Camera playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;
    public float airControlFactor = 0.5f; // Reduced control while in air
    public float slingshotCooldown = 0.5f; // Cooldown between slingshots

    private Rigidbody rb;
    private Vector2 rotation = Vector2.zero;
    private bool isGrounded;
    private float lastSlingshotTime;
    private SlingshotController slingshotController;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        slingshotController = GetComponent<SlingshotController>();
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        CheckGrounded();
    }

    void CheckGrounded()
    {
        // Simple ground check using a raycast
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal") * moveSpeed;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        
        // Apply air control factor when not grounded
        if (!isGrounded)
        {
            move *= airControlFactor;
        }

        // Only modify horizontal velocity, preserve vertical velocity
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void HandleMouseLook()
    {
        // Don't allow camera movement while slingshot is charging
        if (slingshotController != null && slingshotController.IsCharging)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotation.x += mouseX;
        rotation.y -= mouseY;
        rotation.y = Mathf.Clamp(rotation.y, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(0, rotation.x, 0);
        playerCamera.transform.localRotation = Quaternion.Euler(rotation.y, 0, 0);
    }

    // Called by SlingshotController when applying force
    public void ApplySlingshotForce(Vector3 force)
    {
        if (Time.time - lastSlingshotTime < slingshotCooldown)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero; // Reset velocity before applying force
        rb.AddForce(force, ForceMode.Impulse);
        lastSlingshotTime = Time.time;
    }
}
