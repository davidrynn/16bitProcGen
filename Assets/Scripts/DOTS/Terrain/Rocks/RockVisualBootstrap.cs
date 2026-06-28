using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Scene-side bootstrap that registers the RockRenderConfig managed singleton.
    /// Assign a rock mesh and material in the inspector and place this in active bootstrap scenes.
    /// </summary>
    public class RockVisualBootstrap : MonoBehaviour, ISurfaceScatterVisualBootstrap
    {
        [Header("Bootstrap")]
        [Tooltip("Master switch for rock scatter. When off, the render config is never registered " +
                 "(nothing draws). Lives here with the scene wiring for discoverability; the system-level " +
                 "gate is ProjectFeatureConfig.EnableRockRenderSystem.")]
        [SerializeField] private bool featureEnabled = true;

        [Header("Rendering")]
        [SerializeField] private Mesh[] rockMeshVariants;
        [SerializeField] private Mesh rockMesh;
        [SerializeField] private Material rockMaterial;
        [SerializeField] private float rockScale = 1f;

        [Header("Distance LOD (see SURFACE_SCATTER_LOD_SPEC.md)")]
        [Tooltip("Optional low-poly far meshes, parallel by index to Rock Mesh Variants. Empty slots auto-pair with '<near mesh name>_Far' sub-assets from the same model file; manual assignments win. Leave a slot empty (and no _Far sibling) to keep that variant full-detail at all distances.")]
        [SerializeField] private Mesh[] rockLodMeshVariants;
        [Tooltip("Camera distance beyond which far meshes are drawn. 0 disables the swap.")]
        [SerializeField] private float rockLodSwapDistance = 60f;

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
                new RockRenderConfig
                {
                    MeshVariants = rockMeshVariants,
                    Mesh = rockMesh,
                    LodMeshVariants = rockLodMeshVariants,
                    Material = rockMaterial,
                    UniformScale = rockScale,
                    LodSwapDistance = rockLodSwapDistance,
                },
                nameof(RockRenderConfig));
        }

        // ISurfaceScatterVisualBootstrap — field-name metadata for the shared scatter inspector.
        string ISurfaceScatterVisualBootstrap.ScatterDisplayName => "Rock Scatter";
        string ISurfaceScatterVisualBootstrap.FeatureEnabledFieldName => nameof(featureEnabled);
        string ISurfaceScatterVisualBootstrap.NearMeshVariantsFieldName => nameof(rockMeshVariants);
        string ISurfaceScatterVisualBootstrap.LegacySingleMeshFieldName => nameof(rockMesh);
        string ISurfaceScatterVisualBootstrap.FarMeshVariantsFieldName => nameof(rockLodMeshVariants);
        string ISurfaceScatterVisualBootstrap.MaterialFieldName => nameof(rockMaterial);
        string ISurfaceScatterVisualBootstrap.LodSwapDistanceFieldName => nameof(rockLodSwapDistance);
        string ISurfaceScatterVisualBootstrap.RenderSystemConfigFlagName => "EnableRockRenderSystem";
        string ISurfaceScatterVisualBootstrap.DefaultModelAssetPath => "Assets/Models/Scatter/Boulders.fbx";

#if UNITY_EDITOR
        /// <summary>
        /// Auto-fills empty far-LOD slots from "&lt;near mesh&gt;_Far" sub-assets in the same
        /// model file (SURFACE_SCATTER_LOD_SPEC §4.2). Runs on inspector edits and via the
        /// component context menu; never overwrites manual assignments.
        /// </summary>
        [ContextMenu("Auto-Pair Far LOD Meshes")]
        private void AutoPairFarLodMeshes()
        {
            if (SurfaceScatterLodAuthoringUtility.TryAutoPair(rockMeshVariants, ref rockLodMeshVariants))
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