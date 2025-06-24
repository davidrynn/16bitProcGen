using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 60f;
    public float jumpForce = 10f;
    public Camera playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;

    private Rigidbody rb;
    private Vector2 rotation = Vector2.zero;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        CheckGrounded();
    }

    void CheckGrounded()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal") * moveSpeed;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        rotation.x += mouseX;
        rotation.y -= mouseY;
        rotation.y = Mathf.Clamp(rotation.y, -maxLookAngle, maxLookAngle);

        transform.rotation = Quaternion.Euler(0, rotation.x, 0);
        playerCamera.transform.localRotation = Quaternion.Euler(rotation.y, 0, 0);
    }
}
