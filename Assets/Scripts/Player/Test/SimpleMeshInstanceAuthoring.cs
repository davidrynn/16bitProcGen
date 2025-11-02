using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Player.Test
{
    public struct SimpleMeshInstancePending : IComponentData
    {
    }

    public sealed class SimpleMeshInstanceReference : IComponentData
    {
        public Mesh Mesh;
        public Material Material;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public MotionVectorGenerationMode MotionVectorMode;
        public int Layer;
        public uint RenderingLayerMask;
        public LightProbeUsage LightProbeUsage;
        public bool IsStatic;
    }

    public class SimpleMeshInstanceAuthoring : MonoBehaviour
    {
        public Mesh Mesh;
        public Material Material;
        public Vector3 Position = new(0, 0.5f, 0);
        public float Scale = 1f;
        public ShadowCastingMode ShadowCastingMode = ShadowCastingMode.On;
        public bool ReceiveShadows = true;
        public MotionVectorGenerationMode MotionVectorMode = MotionVectorGenerationMode.Camera;
        public LightProbeUsage LightProbeUsage = LightProbeUsage.Off;

        private class Baker : Baker<SimpleMeshInstanceAuthoring>
        {
            public override void Bake(SimpleMeshInstanceAuthoring authoring)
            {
                // Make this entity renderable
                var entity = GetEntity(TransformUsageFlags.Renderable);

                if (authoring.Mesh == null || authoring.Material == null)
                {
                    return;
                }

                AddComponent(entity, LocalTransform.FromPositionRotationScale(
                    (float3)authoring.Position, quaternion.identity, math.max(0.0001f, authoring.Scale)));

                AddComponent<SimpleMeshInstancePending>(entity);
                AddComponentObject(entity, new SimpleMeshInstanceReference
                {
                    Mesh = authoring.Mesh,
                    Material = authoring.Material,
                    ShadowCastingMode = authoring.ShadowCastingMode,
                    ReceiveShadows = authoring.ReceiveShadows,
                    MotionVectorMode = authoring.MotionVectorMode,
                    Layer = authoring.gameObject.layer,
                    RenderingLayerMask = uint.MaxValue,
                    LightProbeUsage = authoring.LightProbeUsage,
                    IsStatic = authoring.gameObject.isStatic
                });
            }
        }
    }
}