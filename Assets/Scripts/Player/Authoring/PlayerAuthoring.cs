using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using DOTS.Player;

namespace DOTS.Player.Authoring
{
    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Movement")]
        public float groundSpeed = 10f;
        public float jumpImpulse = 5f;
        [Range(0f, 1f)] public float airControl = 0.2f;

        [Header("Future Modes")]
        public float slingshotImpulse = 30f;
        public float swimSpeed = 6f;
        public float zeroGDamping = 2f;

        [Header("View")]
        public float mouseSensitivity = 0.1f;
        public float maxPitchDegrees = 85f;

        [Header("Probing")]
        public float groundProbeDistance = 1.3f;

        [Header("Camera")]
        public Camera playerCamera;

        private void Awake()
        {
            // Ensure Rigidbody exists - Unity's physics baking system will convert this to DOTS physics components
            UnityEngine.Rigidbody rb = GetComponent<UnityEngine.Rigidbody>();
            if (rb == null)
            {
                Debug.Log("No Rigidbody found, adding one");
                rb = gameObject.AddComponent<UnityEngine.Rigidbody>();
                rb.mass = 70f;
                rb.linearDamping = 0f;
                rb.angularDamping = 0f;
                rb.constraints = UnityEngine.RigidbodyConstraints.FreezeRotation; // Prevent player from tipping over
            }
            
            // Ensure Collider exists - required for physics interactions
            if (GetComponent<UnityEngine.Collider>() == null)
            {
                Debug.Log("No Collider found, adding one");
                var capsule = gameObject.AddComponent<UnityEngine.CapsuleCollider>();
                capsule.height = 2f;
                capsule.radius = 0.5f;
                capsule.center = new Vector3(0, 1, 0);
            }
        }

        private class PlayerAuthoringBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add movement configuration
                AddComponent(entity, new PlayerMovementConfig
                {
                    GroundSpeed = authoring.groundSpeed,
                    JumpImpulse = authoring.jumpImpulse,
                    AirControl = authoring.airControl,
                    SlingshotImpulse = authoring.slingshotImpulse,
                    SwimSpeed = authoring.swimSpeed,
                    ZeroGDamping = authoring.zeroGDamping,
                    MouseSensitivity = authoring.mouseSensitivity,
                    MaxPitchDegrees = authoring.maxPitchDegrees,
                    GroundProbeDistance = Mathf.Max(0.1f, authoring.groundProbeDistance)
                });

                // Add input component
                AddComponent(entity, new PlayerInputComponent
                {
                    Move = float2.zero,
                    Look = float2.zero,
                    JumpPressed = false
                });

                // Add movement state
                AddComponent(entity, new PlayerMovementState
                {
                    Mode = PlayerMovementMode.Ground,
                    IsGrounded = false,
                    FallTime = 0f
                });

                // Add view component
                AddComponent(entity, new PlayerViewComponent
                {
                    YawDegrees = 0f,
                    PitchDegrees = 0f
                });

                float3 cameraOffset = new float3(0f, 1.6f, 0f);

                // Link camera if provided
                if (authoring.playerCamera != null)
                {
                    var cameraEntity = GetEntity(authoring.playerCamera.gameObject, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new PlayerCameraLink
                    {
                        CameraEntity = cameraEntity
                    });

                    cameraOffset = (float3)(authoring.playerCamera.transform.position - authoring.transform.position);
                }

                AddComponent(entity, new PlayerCameraSettings
                {
                    Offset = cameraOffset
                });
                
                // Physics components (PhysicsVelocity, PhysicsMass, PhysicsCollider, etc.) 
                // are automatically added by Unity's RigidbodyBaker from the Rigidbody component
            }
        }
    }
}
