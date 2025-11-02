using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Player.Test
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CameraOscillationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var time = (float)SystemAPI.Time.ElapsedTime;
            foreach (var (transform, settings) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<CameraOscillationSettings>>()
                              .WithAll<CameraOscillationTag>())
            {
                var position = transform.ValueRO.Position;
                position.x = math.sin(time * settings.ValueRO.Frequency * math.TAU) * settings.ValueRO.Amplitude;
                transform.ValueRW = transform.ValueRO.WithPosition(position);
            }
        }
    }
}