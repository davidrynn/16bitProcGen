using DOTS.Player.Components;
using DOTS.Terrain.Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Impostors
{
    /// <summary>
    /// Renders the ground-plane impostor disc each frame via
    /// <see cref="Graphics.RenderMeshInstanced"/>, bypassing Entities.Graphics and
    /// its BatchRendererGroup. This avoids the DOTS_INSTANCING_ON shader variant
    /// requirement that BatchRendererGroup imposes on every custom shader it uses.
    ///
    /// Each frame the system:
    ///   1. Reads the player world position.
    ///   2. Pushes <c>_PlayerXZ</c>, <c>_InnerFadeStart</c>, <c>_InnerFadeEnd</c>
    ///      to the runtime material so the radial fade tracks the player.
    ///   3. Issues a single <see cref="Graphics.RenderMeshInstanced"/> draw call
    ///      centred on the player XZ.
    ///
    /// Uses SystemBase (managed) because Material property setters and
    /// Graphics.RenderMeshInstanced are managed APIs Burst cannot call.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GroundPlaneImpostorSystem : SystemBase
    {
        private EntityQuery _playerQuery;
        private EntityQuery _impostorQuery;

        // Cached single-element array prevents per-frame GC allocation.
        private readonly Matrix4x4[] _matrixBuffer = new Matrix4x4[1];

        protected override void OnCreate()
        {
            _playerQuery   = GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalToWorld>());
            _impostorQuery = GetEntityQuery(ComponentType.ReadOnly<GroundPlaneImpostorTag>());

            RequireForUpdate<GroundPlaneImpostorTag>();
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            if (_playerQuery.IsEmpty || _impostorQuery.IsEmpty)
                return;

            var playerLtw = _playerQuery.GetSingleton<LocalToWorld>();
            float playerX = playerLtw.Position.x;
            float playerZ = playerLtw.Position.z;

            var impostorEntity = _impostorQuery.GetSingletonEntity();
            var config = EntityManager.GetComponentObject<GroundPlaneImpostorConfig>(impostorEntity);

            if (config?.ImpostorMaterial == null || config.ImpostorMesh == null)
                return;

            // Keep the fade centred on the player so the inner clip boundary
            // always aligns with the terrain chunk radius around the player.
            config.ImpostorMaterial.SetVector("_PlayerXZ",       new Vector4(playerX, playerZ, 0f, 0f));
            config.ImpostorMaterial.SetFloat("_InnerFadeStart",  config.InnerFadeStart);
            config.ImpostorMaterial.SetFloat("_InnerFadeEnd",    config.InnerFadeEnd);

            _matrixBuffer[0] = Matrix4x4.Translate(new Vector3(playerX, config.WorldY, playerZ));

            var renderParams = new RenderParams(config.ImpostorMaterial)
            {
                // worldBounds guides GPU culling; Y range covers full sky-drop altitude.
                worldBounds      = new Bounds(
                    new Vector3(playerX, config.WorldY, playerZ),
                    new Vector3(config.OuterRadius * 2f, 1200f, config.OuterRadius * 2f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows    = false,
            };

            Graphics.RenderMeshInstanced(renderParams, config.ImpostorMesh, 0, _matrixBuffer);

            DebugSettings.LogRendering(
                $"GroundPlaneImpostorSystem: rendered at ({playerX:0.0}, {playerZ:0.0})");
        }
    }
}
