using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Scene-side bootstrap that registers the TreeRenderConfig managed singleton.
    /// Assign a placeholder mesh (Unity Capsule is fine for MVP) and an unlit material.
    /// Place this MonoBehaviour in the same scene as TerrainBootstrapAuthoring.
    /// </summary>
    public class TreeVisualBootstrap : MonoBehaviour, ISurfaceScatterVisualBootstrap
    {
        [Header("Bootstrap")]
        [Tooltip("Master switch for tree scatter. When off, the render config is never registered " +
                 "(nothing draws). Lives here with the scene wiring for discoverability; the system-level " +
                 "gate is ProjectFeatureConfig.EnableTreeRenderSystem.")]
        [SerializeField] private bool     featureEnabled = true;

        [Header("Rendering")]
        [SerializeField] private Mesh[]   treeMeshVariants;
        [SerializeField] private Mesh     treeMesh;
        [SerializeField] private Material treeMaterial;
        [SerializeField] private float    treeScale = 1f;

        [Header("Distance LOD (see SURFACE_SCATTER_LOD_SPEC.md)")]
        [Tooltip("Optional low-poly far meshes, parallel by index to Tree Mesh Variants. Empty slots auto-pair with '<near mesh name>_Far' sub-assets from the same model file; manual assignments win. Leave a slot empty (and no _Far sibling) to keep that variant full-detail at all distances.")]
        [SerializeField] private Mesh[]   treeLodMeshVariants;
        [Tooltip("Camera distance beyond which far meshes are drawn. 0 disables the swap.")]
        [SerializeField] private float    treeLodSwapDistance = 60f;

        private void Start()
        {
            // Master switch — skip all registration so the feature draws nothing when disabled.
            if (!featureEnabled) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                em,
                new TreeRenderConfig
                {
                    MeshVariants = treeMeshVariants,
                    Mesh = treeMesh,
                    LodMeshVariants = treeLodMeshVariants,
                    Material = treeMaterial,
                    UniformScale = treeScale,
                    LodSwapDistance = treeLodSwapDistance,
                },
                nameof(TreeRenderConfig));
        }

        // ISurfaceScatterVisualBootstrap — field-name metadata for the shared scatter inspector.
        string ISurfaceScatterVisualBootstrap.ScatterDisplayName => "Tree Scatter";
        string ISurfaceScatterVisualBootstrap.FeatureEnabledFieldName => nameof(featureEnabled);
        string ISurfaceScatterVisualBootstrap.NearMeshVariantsFieldName => nameof(treeMeshVariants);
        string ISurfaceScatterVisualBootstrap.LegacySingleMeshFieldName => nameof(treeMesh);
        string ISurfaceScatterVisualBootstrap.FarMeshVariantsFieldName => nameof(treeLodMeshVariants);
        string ISurfaceScatterVisualBootstrap.MaterialFieldName => nameof(treeMaterial);
        string ISurfaceScatterVisualBootstrap.LodSwapDistanceFieldName => nameof(treeLodSwapDistance);
        string ISurfaceScatterVisualBootstrap.RenderSystemConfigFlagName => "EnableTreeRenderSystem";
        string ISurfaceScatterVisualBootstrap.DefaultModelAssetPath => "Assets/Models/Trees/Tree_Oak_LowPoly_01.fbx";

#if UNITY_EDITOR
        /// <summary>
        /// Auto-fills empty far-LOD slots from "&lt;near mesh&gt;_Far" sub-assets in the same
        /// model file (SURFACE_SCATTER_LOD_SPEC §4.2). Runs on inspector edits and via the
        /// component context menu; never overwrites manual assignments.
        /// </summary>
        [ContextMenu("Auto-Pair Far LOD Meshes")]
        private void AutoPairFarLodMeshes()
        {
            if (SurfaceScatterLodAuthoringUtility.TryAutoPair(treeMeshVariants, ref treeLodMeshVariants))
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
