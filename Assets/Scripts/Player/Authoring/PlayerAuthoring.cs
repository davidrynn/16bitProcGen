using Unity.Entities;
using UnityEngine;

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

        private class PlayerAuthoringBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

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

                AddComponent<PlayerInputComponent>(entity);

                AddComponent(entity, new PlayerMovementState
                {
                    Mode = PlayerMovementMode.Ground,
                    IsGrounded = false,
                    FallTime = 0f
                });

                AddComponent<PlayerViewComponent>(entity);

                if (authoring.playerCamera != null)
                {
                    var cameraEntity = GetEntity(authoring.playerCamera.gameObject, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new PlayerCameraLink
                    {
                        CameraEntity = cameraEntity
                    });
                }
            }
        }
    }
}
