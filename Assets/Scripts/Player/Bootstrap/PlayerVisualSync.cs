using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Minimal hybrid approach: Syncs GameObject visual to ECS entity transform.
    /// This is a thin "view layer" - all game logic remains in pure ECS systems.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class PlayerVisualSync : MonoBehaviour
    {
        [Tooltip("The ECS entity this visual represents")]
        public Entity targetEntity;

        private EntityManager _entityManager;
        private bool _entityManagerValid;

        private void LateUpdate()
        {
            if (!TryResolveEntityManager(out var entityManager))
            {
                return;
            }

            if (targetEntity == Entity.Null)
            {
                return;
            }

            if (!entityManager.Exists(targetEntity))
            {
                Destroy(gameObject);
                return;
            }

            if (!entityManager.HasComponent<LocalTransform>(targetEntity))
            {
                return;
            }

            var entityTransform = entityManager.GetComponentData<LocalTransform>(targetEntity);
            var worldPosition = (Vector3)entityTransform.Position;
            var worldRotation = new Quaternion(entityTransform.Rotation.value.x, entityTransform.Rotation.value.y, entityTransform.Rotation.value.z, entityTransform.Rotation.value.w);

            transform.SetPositionAndRotation(worldPosition, worldRotation);

            var uniformScale = entityTransform.Scale;
            if (!Mathf.Approximately(transform.localScale.x, uniformScale) ||
                !Mathf.Approximately(transform.localScale.y, uniformScale) ||
                !Mathf.Approximately(transform.localScale.z, uniformScale))
            {
                transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            }
        }

        private bool TryResolveEntityManager(out EntityManager entityManager)
        {
            if (_entityManagerValid && _entityManager.WorldUnmanaged.IsCreated)
            {
                entityManager = _entityManager;
                return true;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                entityManager = default;
                _entityManagerValid = false;
                return false;
            }

            _entityManager = world.EntityManager;
            _entityManagerValid = true;
            entityManager = _entityManager;
            return true;
        }
    }
}

