using Unity.Entities;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Authoring
{
    [DisallowMultipleComponent]
    public class MainCameraAuthoring : MonoBehaviour
    {
        private class Baker : Baker<MainCameraAuthoring>
        {
            public override void Bake(MainCameraAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                // Tag this as our main camera entity
                AddComponent<MainCameraTag>(e);

                // Attach the managed Camera so hybrid systems can access it via ManagedAPI
                var cam = authoring.GetComponent<Camera>();
                if (cam != null)
                {
                    AddComponentObject(e, cam);
                }
            }
        }
    }
}
