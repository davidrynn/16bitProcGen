using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Authoring
{
    /// <summary>
    /// Ensures the camera GameObject is baked into an entity with a LocalTransform component
    /// that can be controlled by DOTS systems like PlayerCameraSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraAuthoring : MonoBehaviour
    {
        private class CameraAuthoringBaker : Baker<CameraAuthoring>
        {
            public override void Bake(CameraAuthoring authoring)
            {
                // Get the entity for this camera with Dynamic transform usage
                // This ensures it has a LocalTransform component that can be modified at runtime
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // LocalTransform is automatically added by Unity when using TransformUsageFlags.Dynamic
                // The Camera component is also automatically added if present on the GameObject
            }
        }
    }
}

