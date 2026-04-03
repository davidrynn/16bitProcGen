using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Bridges the DOTS player entity's world position to a scene Transform each frame.
    /// Use this as the link between ECS and BF_SetInteractiveShaderEffects.
    ///
    /// Scene setup:
    ///   1. Place this MonoBehaviour on any scene GameObject.
    ///   2. Create a child GameObject with an orthographic Camera and a
    ///      BF_SetInteractiveShaderEffects component (assign a RenderTexture to it).
    ///   3. Set BF_SetInteractiveShaderEffects.transformToFollow to the
    ///      "GrassEffectProxy" child that this script creates automatically,
    ///      OR assign your own Transform to the ProxyTransform field below.
    ///   4. Enable "Use RenderTexture Effect" on the grass material.
    ///
    /// The script gracefully does nothing until a player entity exists.
    /// </summary>
    public class GrassInteractiveEffectsBootstrap : MonoBehaviour
    {
        [Tooltip("Transform updated with the player's world position each frame. " +
                 "Assign as 'transformToFollow' on BF_SetInteractiveShaderEffects. " +
                 "Leave null to auto-create a child GameObject named 'GrassEffectProxy'.")]
        [SerializeField] private Transform proxyTransform;

        private EntityQuery _playerQuery;
        private bool _queryInitialized;

        public Transform ProxyTransform => proxyTransform;

        private void Start()
        {
            if (proxyTransform == null)
            {
                var go = new GameObject("GrassEffectProxy");
                go.transform.SetParent(transform, false);
                proxyTransform = go.transform;
            }

            AutoWireBFEffectsComponents();
        }

        // BF_SetInteractiveShaderEffects lives in Assembly-CSharp so we cannot reference
        // the type directly from this assembly. Use reflection to set transformToFollow
        // on any matching component found in children.
        private void AutoWireBFEffectsComponents()
        {
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "BF_SetInteractiveShaderEffects") continue;

                var field = mb.GetType().GetField("transformToFollow");
                field?.SetValue(mb, proxyTransform);
            }
        }

        private void Update()
        {
            if (proxyTransform == null) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            if (!_queryInitialized)
            {
                _playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerTag>(),
                    ComponentType.ReadOnly<LocalTransform>()
                );
                _queryInitialized = true;
            }

            if (_playerQuery.CalculateEntityCount() != 1) return;

            var lt = _playerQuery.GetSingleton<LocalTransform>();
            proxyTransform.position = new Vector3(lt.Position.x, lt.Position.y, lt.Position.z);
        }

        private void OnDestroy()
        {
            if (_queryInitialized)
            {
                _playerQuery.Dispose();
                _queryInitialized = false;
            }
        }
    }
}
