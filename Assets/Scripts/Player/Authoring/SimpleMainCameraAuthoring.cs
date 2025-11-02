// MainCameraAuthoring.cs
using Unity.Entities;
using UnityEngine;

public class MainCameraAuthoring : MonoBehaviour
{
    class Baker : Baker<MainCameraAuthoring>
    {
        public override void Bake(MainCameraAuthoring authoring)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<MainCameraTag>(e);
            // LocalTransform will be baked automatically from the GameObject’s Transform.
        }
    }
}
