using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Player.Test
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CameraOscillationCompanionSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            foreach (var (localToWorld, entity) in
                     SystemAPI.Query<RefRO<LocalToWorld>>()
                              .WithEntityAccess()
                              .WithAll<CameraOscillationTag>())
            {
                if (!entityManager.HasComponent<Transform>(entity))
                {
                    continue;
                }

                var companion = entityManager.GetComponentObject<Transform>(entity);
                companion.position = localToWorld.ValueRO.Position;
                companion.rotation = localToWorld.ValueRO.Rotation;
            }
        }
    }
}