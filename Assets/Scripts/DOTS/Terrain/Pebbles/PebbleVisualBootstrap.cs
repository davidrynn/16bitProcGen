using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Scene-side bootstrap that registers the PebbleRenderConfig managed singleton and the
    /// PebblePlacementParams tuning singleton. Unlike the rock/tree bootstraps, placement
    /// frequency and distribution are inspector data here (the R2 target pattern) — assign
    /// cluster meshes, then tune zone size/coverage and density without code changes.
    /// </summary>
    public class PebbleVisualBootstrap : MonoBehaviour, ISurfaceScatterVisualBootstrap
    {
        [Header("Bootstrap")]
        [Tooltip("Master switch for pebble scatter. When off, the render config is never registered " +
                 "(nothing draws). Lives here with the scene wiring for discoverability; the system-level " +
                 "gate is ProjectFeatureConfig.EnablePebbleRenderSystem.")]
        [SerializeField] private bool featureEnabled = true;

        [Header("Rendering")]
        [SerializeField] private Mesh[] pebbleMeshVariants;
        [SerializeField] private Material pebbleMaterial;
        [SerializeField] private float pebbleScale = 1f;

        [Header("Distance LOD (see SURFACE_SCATTER_LOD_SPEC.md)")]
        [Tooltip("Optional far meshes, parallel by index to Pebble Mesh Variants. Empty slots auto-pair with '<near mesh name>_Far' sub-assets from the same model file; manual assignments win. Pebbles ship without far meshes by design (TICKETS B2) — they cull with chunk LOD instead.")]
        [SerializeField] private Mesh[] pebbleLodMeshVariants;
        [Tooltip("Camera distance beyond which far meshes are drawn. 0 disables the swap (pebble default).")]
        [SerializeField] private float pebbleLodSwapDistance = 0f;

        [Header("Placement (deterministic — edits apply on chunk regeneration)")]
        [Tooltip("World-space frequency of the rocky-zone mask. Lower = larger, rarer zones (~0.012 ≈ 80m features).")]
        [SerializeField] private float zoneNoiseFrequency = 0.012f;
        [Tooltip("Noise threshold [-1..1] above which terrain counts as a rocky zone. Higher = less coverage. Default ≈ 15-20% of area.")]
        [SerializeField] private float zoneThreshold = 0.55f;
        [Tooltip("Accept probability per candidate inside a rocky zone. Zero outside zones — that is what clusters the distribution.")]
        [Range(0f, 1f)]
        [SerializeField] private float inZoneProbability = 0.15f;
        [Tooltip("Minimum distance between cluster origins (also candidate cell size).")]
        [SerializeField] private float minSpacing = 3.0f;
        [Tooltip("Reject slopes: minimum surface-normal Y.")]
        [Range(0f, 1f)]
        [SerializeField] private float minGroundNormalY = 0.70f;
        [SerializeField] private float minUniformScale = 0.8f;
        [SerializeField] private float maxUniformScale = 1.3f;

        private void Start()
        {
            // Master switch — skip all registration so the feature draws nothing when disabled.
            if (!featureEnabled)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return;
            }

            var em = world.EntityManager;
            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                em,
                new PebbleRenderConfig
                {
                    MeshVariants = pebbleMeshVariants,
                    LodMeshVariants = pebbleLodMeshVariants,
                    Material = pebbleMaterial,
                    UniformScale = pebbleScale,
                    LodSwapDistance = pebbleLodSwapDistance,
                },
                nameof(PebbleRenderConfig));

            var placementParams = new PebblePlacementParams
            {
                ZoneNoiseFrequency = zoneNoiseFrequency,
                ZoneThreshold = zoneThreshold,
                InZoneProbability = inZoneProbability,
                MinSpacing = minSpacing,
                MinGroundNormalY = minGroundNormalY,
                MinUniformScale = minUniformScale,
                MaxUniformScale = maxUniformScale,
                VariantCount = (byte)Mathf.Clamp(
                    pebbleMeshVariants != null ? pebbleMeshVariants.Length : 1, 1, 255),
            };

            using var query = em.CreateEntityQuery(typeof(PebblePlacementParams));
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = em.CreateEntity();
                em.SetName(entity, "PebblePlacementParams");
                em.AddComponentData(entity, placementParams);
            }
            else
            {
                em.SetComponentData(query.GetSingletonEntity(), placementParams);
            }
        }

        // ISurfaceScatterVisualBootstrap — field-name metadata for the shared scatter inspector.
        string ISurfaceScatterVisualBootstrap.ScatterDisplayName => "Pebble Scatter";
        string ISurfaceScatterVisualBootstrap.FeatureEnabledFieldName => nameof(featureEnabled);
        string ISurfaceScatterVisualBootstrap.NearMeshVariantsFieldName => nameof(pebbleMeshVariants);
        string ISurfaceScatterVisualBootstrap.LegacySingleMeshFieldName => null; // pebbles have no single-mesh fallback
        string ISurfaceScatterVisualBootstrap.FarMeshVariantsFieldName => nameof(pebbleLodMeshVariants);
        string ISurfaceScatterVisualBootstrap.MaterialFieldName => nameof(pebbleMaterial);
        string ISurfaceScatterVisualBootstrap.LodSwapDistanceFieldName => nameof(pebbleLodSwapDistance);
        string ISurfaceScatterVisualBootstrap.RenderSystemConfigFlagName => "EnablePebbleRenderSystem";
        string ISurfaceScatterVisualBootstrap.DefaultModelAssetPath => "Assets/Models/Scatter/PebbleClusters.fbx";

#if UNITY_EDITOR
        /// <summary>
        /// Auto-fills empty far-LOD slots from "&lt;near mesh&gt;_Far" sub-assets in the same
        /// model file (SURFACE_SCATTER_LOD_SPEC §4.2). Runs on inspector edits and via the
        /// component context menu; never overwrites manual assignments.
        /// </summary>
        [ContextMenu("Auto-Pair Far LOD Meshes")]
        private void AutoPairFarLodMeshes()
        {
            if (SurfaceScatterLodAuthoringUtility.TryAutoPair(pebbleMeshVariants, ref pebbleLodMeshVariants))
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private void OnValidate()
        {
            AutoPairFarLodMeshes();
        }
#endif
    }
}
