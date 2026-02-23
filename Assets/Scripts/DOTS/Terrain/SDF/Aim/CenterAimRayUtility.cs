using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// Shared helper for center-screen aiming.
    /// Keeps all center-ray consumers on the same viewport contract.
    /// </summary>
    public static class CenterAimRayUtility
    {
        public static readonly Vector2 CenterViewport = new Vector2(0.5f, 0.5f);

        public static bool TryGetCenterRay(float maxDistance, out Camera camera, out float3 origin, out float3 direction, out float3 end)
        {
            camera = Camera.main;
            origin = float3.zero;
            direction = float3.zero;
            end = float3.zero;

            if (camera == null)
            {
                return false;
            }

            var ray = camera.ViewportPointToRay(new Vector3(CenterViewport.x, CenterViewport.y, 0f));
            origin = (float3)ray.origin;
            direction = math.normalizesafe((float3)ray.direction, (float3)camera.transform.forward);
            end = origin + direction * maxDistance;
            return true;
        }
    }
}
