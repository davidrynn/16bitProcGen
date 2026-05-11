using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.Core;
using DOTS.Terrain.Rendering;

namespace DOTS.Impostors
{
    /// <summary>
    /// Scene-side MonoBehaviour bootstrap for the ground-plane impostor.
    /// On Start it generates the flat disc mesh, creates a runtime material,
    /// and spawns the single ECS entity that <see cref="GroundPlaneImpostorSystem"/>
    /// tracks each frame.
    ///
    /// Attach to a "GroundPlaneImpostor" GameObject in the scene.
    /// Disable <see cref="_enabled"/> or remove this component to suppress the feature
    /// (also disable <c>EnableGroundPlaneImpostor</c> in <c>DotsSystemBootstrap</c>
    /// so the system is not registered).
    /// </summary>
    public class GroundPlaneImpostorBootstrap : MonoBehaviour
    {
        private const string ShaderName = "Ground/GroundPlaneImpostor";
        private const int   GridSubdivisions = 64;
        private const float DefaultOuterRadius = 1500f;
        // Terrain chunks render to ~180 m (medium preset, 12 chunks × 15 m).
        // Start the fade slightly inside that boundary so there is no gap.
        // Terrain chunks (Opaque queue) depth-occlude the disc (Transparent queue) naturally,
        // so no manual inner fade is needed. Setting both to 0 makes the disc fully opaque
        // everywhere — terrain renders on top via the depth buffer.
        private const float DefaultInnerFadeStart = 0f;
        private const float DefaultInnerFadeEnd   = 0f;
        private const float DefaultWorldY = 0f;

        [Tooltip("Disable to suppress entity creation without removing the component.")]
        [SerializeField] private bool _enabled = true;

        [Header("Color Sync")]
        [Tooltip("Material to read _BaseColor from for the impostor grass color. " +
                 "If null, auto-loads from TerrainChunkRenderSettings.")]
        [SerializeField] private Material _terrainColorSource;

        [Header("Overrides (0 = use defaults)")]
        [SerializeField] private float _outerRadius;
        [SerializeField] private float _innerFadeStart;
        [SerializeField] private float _innerFadeEnd;
        [SerializeField] private float _worldY;

        private void Start()
        {
            if (!_enabled)
            {
                DebugSettings.LogRendering("GroundPlaneImpostorBootstrap: disabled via inspector flag.");
                return;
            }

            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                DebugSettings.LogWarning("GroundPlaneImpostorBootstrap: default world not ready, skipping.");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                DebugSettings.LogWarning($"GroundPlaneImpostorBootstrap: shader '{ShaderName}' not found. " +
                    "Ensure GroundPlaneImpostor.shader is in Assets/Shaders/.");
                return;
            }

            float outerRadius     = _outerRadius     > 0f ? _outerRadius     : DefaultOuterRadius;
            float innerFadeStart  = _innerFadeStart  > 0f ? _innerFadeStart  : DefaultInnerFadeStart;
            float innerFadeEnd    = _innerFadeEnd    > 0f ? _innerFadeEnd    : DefaultInnerFadeEnd;
            float worldY          = _worldY          != 0f ? _worldY          : DefaultWorldY;

            var material = new Material(shader) { name = "GroundPlaneImpostor_Runtime" };
            // RenderMeshInstanced throws if enableInstancing is false, even when the shader
            // has #pragma multi_compile_instancing. The Material flag and shader variant are
            // separate requirements.
            material.enableInstancing = true;
            material.SetFloat("_InnerFadeStart", innerFadeStart);
            material.SetFloat("_InnerFadeEnd",   innerFadeEnd);
            material.SetVector("_PlayerXZ", Vector4.zero);

            // Color sync disabled: shader defaults (linear-space) are tuned to visually match
            // the terrain. Re-enable once the correct linear↔gamma handling is confirmed.
            // SyncTerrainColor(material);

            var mesh = GenerateDiscMesh(outerRadius, GridSubdivisions);

            SpawnEntity(world, mesh, material, outerRadius, innerFadeStart, innerFadeEnd, worldY);

            DebugSettings.LogRendering(
                $"GroundPlaneImpostorBootstrap: entity spawned — outerRadius={outerRadius:0.0}, " +
                $"innerFade=[{innerFadeStart:0.0}, {innerFadeEnd:0.0}], worldY={worldY:0.0}",
                forceLog: true);
        }

        /// <summary>
        /// Reads _BaseColor from the terrain chunk material and applies it as the impostor's
        /// _GrassColor so the two stay visually matched as the terrain material evolves.
        /// If the terrain material has no _BaseColor (or can't be found), the impostor
        /// keeps its shader default.
        /// </summary>
        private void SyncTerrainColor(Material impostorMat)
        {
            var source = _terrainColorSource;

            if (source == null)
            {
                var renderSettings = TerrainChunkRenderSettingsProvider.GetOrLoad();
                source = renderSettings?.ChunkMaterial;
            }

            if (source == null || !source.HasProperty("_BaseColor"))
            {
                DebugSettings.LogWarning("GroundPlaneImpostorBootstrap: no terrain material found for color sync; " +
                    "assign TerrainMat.mat to the 'Terrain Color Source' field on this component.");
                return;
            }

            Color baseColor = source.GetColor("_BaseColor");

            // TerrainChunkRenderSettings falls back to a runtime white material when the
            // asset isn't assigned. Don't propagate white — keep shader defaults instead.
            float luminance = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
            if (luminance > 0.85f)
            {
                DebugSettings.LogWarning("GroundPlaneImpostorBootstrap: terrain material appears to be a " +
                    "white fallback (luminance > 0.85). Keeping shader default colors. " +
                    "Fix: assign TerrainMat.mat to the 'Terrain Color Source' field on this component.");
                return;
            }

            // _GrassColor and _RockColor are tagged [Gamma] in the shader, so Unity converts
            // gamma → linear automatically — pass the gamma-space value directly.
            //
            // _BaseColor is the pre-PBR albedo. The SDF terrain appears significantly darker
            // because URP Lit applies normal-based Lambert shading across rough geometry;
            // average apparent brightness is roughly 45% of the raw albedo. The 0.45 factor
            // here aligns the impostor's colour with the actual shaded terrain appearance.
            const float shadingScale = 0.55f;
            Color grassColor = new Color(baseColor.r * shadingScale, baseColor.g * shadingScale, baseColor.b * shadingScale, 1f);
            impostorMat.SetColor("_GrassColor", grassColor);

            // Derive rock color: darken and desaturate toward a neutral grey-brown.
            float grey = baseColor.r * 0.299f + baseColor.g * 0.587f + baseColor.b * 0.114f;
            var rockBase = new Color(grey * 1.05f, grey * 0.98f, grey * 0.88f, 1f);
            Color rockColor = Color.Lerp(baseColor * 0.65f, rockBase, 0.45f);
            rockColor = new Color(rockColor.r * shadingScale, rockColor.g * shadingScale, rockColor.b * shadingScale, 1f);
            impostorMat.SetColor("_RockColor", rockColor);

            DebugSettings.LogRendering(
                $"GroundPlaneImpostorBootstrap: synced colors from '{source.name}' — " +
                $"grass={grassColor} rock={rockColor}",
                forceLog: true);
        }

        private static void SpawnEntity(
            Unity.Entities.World world,
            Mesh mesh,
            Material material,
            float outerRadius,
            float innerFadeStart,
            float innerFadeEnd,
            float worldY)
        {
            var em = world.EntityManager;
            var entity = em.CreateEntity();

            em.AddComponent<GroundPlaneImpostorTag>(entity);

            // Rendering is handled by Graphics.RenderMeshInstanced in GroundPlaneImpostorSystem,
            // not by Entities.Graphics — this avoids the DOTS_INSTANCING_ON shader requirement
            // imposed by BatchRendererGroup on any custom shader.
            em.AddComponentObject(entity, new GroundPlaneImpostorConfig
            {
                WorldY           = worldY,
                InnerFadeStart   = innerFadeStart,
                InnerFadeEnd     = innerFadeEnd,
                OuterRadius      = outerRadius,
                ImpostorMaterial = material,
                ImpostorMesh     = mesh,
            });
        }

        /// <summary>
        /// Generates a flat XZ-plane disc mesh centred at the origin.
        /// Uses a uniform grid (no polar subdivision) so world-space noise
        /// sampling remains consistent across the disc.
        /// </summary>
        private static Mesh GenerateDiscMesh(float outerRadius, int subdivisions)
        {
            int vertsPerSide = subdivisions + 1;
            int vertCount    = vertsPerSide * vertsPerSide;
            int triCount     = subdivisions * subdivisions * 2;

            var positions = new Vector3[vertCount];
            var indices   = new int[triCount * 3];

            float step  = outerRadius * 2f / subdivisions;
            float start = -outerRadius;

            int vi = 0;
            for (int z = 0; z < vertsPerSide; z++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    positions[vi++] = new Vector3(start + x * step, 0f, start + z * step);
                }
            }

            // Winding order: v0, v2, v1 → front face normal +Y (visible from above)
            int ti = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int v0 = z * vertsPerSide + x;
                    int v1 = v0 + 1;
                    int v2 = v0 + vertsPerSide;
                    int v3 = v2 + 1;

                    indices[ti++] = v0; indices[ti++] = v2; indices[ti++] = v1;
                    indices[ti++] = v1; indices[ti++] = v2; indices[ti++] = v3;
                }
            }

            var mesh = new Mesh
            {
                name     = "GroundPlaneImpostor_Disc",
                vertices  = positions,
                triangles = indices,
            };
            // Explicit bounds so Unity culls correctly even before LocalToWorld is applied
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(outerRadius * 2f, 1f, outerRadius * 2f));
            return mesh;
        }
    }
}
