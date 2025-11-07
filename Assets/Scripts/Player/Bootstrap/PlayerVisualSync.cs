using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Minimal hybrid approach: Syncs GameObject visual to ECS entity transform.
    /// This is a thin "view layer" - all game logic remains in pure ECS systems.
    /// </summary>
    public class PlayerVisualSync : MonoBehaviour
    {
        [Tooltip("The ECS entity this visual represents")]
        public Entity targetEntity;

        private bool _hasLoggedFirstSync;

        private void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            
            if (world == null)
            {
                return;
            }
            
            if (targetEntity == Entity.Null)
            {
                return;
            }
            
            if (!world.EntityManager.Exists(targetEntity))
            {
                // Entity was destroyed - destroy visual too
                Destroy(gameObject);
                return;
            }
            
            if (!world.EntityManager.HasComponent<LocalTransform>(targetEntity))
            {
                return;
            }
            
            // Sync GameObject transform with ECS entity transform
            var transform = world.EntityManager.GetComponentData<LocalTransform>(targetEntity);
            this.transform.position = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);
            this.transform.rotation = new Quaternion(transform.Rotation.value.x, transform.Rotation.value.y, transform.Rotation.value.z, transform.Rotation.value.w);
            this.transform.localScale = new Vector3(transform.Scale, transform.Scale, transform.Scale);
            
            if (!_hasLoggedFirstSync)
            {
                Debug.Log($"[PlayerVisualSync] First successful sync! Entity {targetEntity.Index} at {transform.Position}");
                _hasLoggedFirstSync = true;
            }
        }
    }
}

