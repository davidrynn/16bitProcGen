using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Player.Test
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SimpleMeshOscillationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var t = (float)SystemAPI.Time.ElapsedTime;
            foreach (var lt in SystemAPI.Query<RefRW<LocalTransform>>())
            {
                var pos = lt.ValueRO.Position;
                pos.x = math.sin(t) * 2f;
                lt.ValueRW = lt.ValueRO.WithPosition(pos);
            }
        }
    }
}