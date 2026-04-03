using System.Diagnostics;
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
        [Tooltip("World-space visual offset from the ECS entity origin. Use Y=1 for feet-origin capsule bodies.")]
        public Vector3 visualOffset = new Vector3(0f, 1f, 0f);

        private EntityManager _entityManager;
        private World _cachedWorld;
        private bool _entityManagerValid;
        private bool _isDestroyed;
        private bool _warnedMissingEntityManager;

        private void LateUpdate()
        {
            if (_isDestroyed)
            {
                return;
            }

            if (!TryResolveEntityManager(out var entityManager))
            {
                if (!_warnedMissingEntityManager)
                {
                    UnityEngine.Debug.LogWarning("EntityManager could not be resolved. Ensure that the DOTS world is initialized.");
                    _warnedMissingEntityManager = true;
                }
                return;
            }

            _warnedMissingEntityManager = false;

            if (targetEntity == Entity.Null)
            {
                DestroyVisual("Target entity is not set. Destroying visual.");
                return;
            }

            if (!entityManager.Exists(targetEntity))
            {
                DestroyVisual("Target entity does not exist. Destroying visual.");
                return;
            }

            if (!entityManager.HasComponent<LocalTransform>(targetEntity))
            {
                DestroyVisual("Target entity does not have a LocalTransform component. Destroying visual.");
                return;
            }

            var entityTransform = entityManager.GetComponentData<LocalTransform>(targetEntity);
            var worldPosition = (Vector3)entityTransform.Position;
            var worldRotation = new Quaternion(entityTransform.Rotation.value.x, entityTransform.Rotation.value.y, entityTransform.Rotation.value.z, entityTransform.Rotation.value.w);
            var rotatedOffset = worldRotation * visualOffset;
            transform.SetPositionAndRotation(worldPosition + rotatedOffset, worldRotation);

            var uniformScale = entityTransform.Scale;
            if (!Mathf.Approximately(transform.localScale.x, uniformScale) ||
                !Mathf.Approximately(transform.localScale.y, uniformScale) ||
                !Mathf.Approximately(transform.localScale.z, uniformScale))
            {
                transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            }
        }

        private void DestroyVisual(string reason)
        {
            if (_isDestroyed)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(reason);
            _isDestroyed = true;

            // Disable to prevent another LateUpdate from running before destruction is processed.
            enabled = false;

            if (gameObject != null)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private bool TryResolveEntityManager(out EntityManager entityManager)
        {
            if (_entityManagerValid && _cachedWorld != null && _cachedWorld.IsCreated)
            {
                _entityManager = _cachedWorld.EntityManager;
                entityManager = _entityManager;
                return true;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                _entityManagerValid = false;
                _cachedWorld = null;
                _entityManager = default;
                return false;
            }

            _cachedWorld = world;
            _entityManager = world.EntityManager;
            _entityManagerValid = true;
            entityManager = _entityManager;
            return true;
        }
    }
}

