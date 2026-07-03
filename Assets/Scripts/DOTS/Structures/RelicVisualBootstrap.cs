using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Core;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Structures
{
    /// <summary>
    /// Scene-side bootstrap that registers the RelicRenderConfig managed singleton
    /// with one or more relic templates. Each template maps a TemplateId to a
    /// mesh/material/scale set. The anchor planning system assigns a TemplateId
    /// to each relic anchor deterministically from the world seed.
    ///
    /// Also emits authoring-trap diagnostics for relics whose world-space
    /// bounding radius approaches the camera far clip.
    /// </summary>
    public class RelicVisualBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public class TemplateInspectorEntry
        {
            [Tooltip("Unique identifier matching StructureFamilyRuleset.AvailableTemplateIds")]
            public string templateId;
            public Mesh mesh;
            public Material material;

            [Tooltip("Uniform scale multiplier for this relic type.")]
            [Min(0.1f)]
            public float scale = 15f;

            [Tooltip("Vertical offset from terrain surface.")]
            public float yOffset;

            [Header("Impostor (optional)")]
            public Mesh impostorMesh;
            public Material impostorMaterial;

            [Tooltip("Target world-space half-extent of the impostor.")]
            public float impostorScale = 30f;
        }

        [SerializeField] private List<TemplateInspectorEntry> templates = new();

        [Header("LOD / Impostor (shared)")]
        [Tooltip("Camera distance at which relics swap to impostor. 0 = auto-derive from far clip and mesh extents.")]
        [SerializeField] private float lodSwapDistance = 0f;

        [Tooltip("Hysteresis band around lodSwapDistance to prevent flicker. 0 = auto-derive (~5% of swap distance).")]
        [SerializeField] private float lodHysteresis = 0f;

        private const float SafetyMarginWorldUnits = 100f;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            if (templates.Count == 0)
            {
                DebugSettings.LogRendering(
                    "RelicVisualBootstrap: No templates configured — relic realization will have no visual data.",
                    forceLog: true);
            }

            var configTemplates = new List<RelicTemplateEntry>();
            float maxWorldRadius = 0f;

            foreach (var t in templates)
            {
                if (t.mesh == null || t.material == null)
                {
                    DebugSettings.LogRendering(
                        $"RelicVisualBootstrap: Template '{t.templateId}' has null mesh or material — skipping.",
                        forceLog: true);
                    continue;
                }

                if (t.mesh.subMeshCount > 1)
                {
                    DebugSettings.LogRendering(
                        $"RelicVisualBootstrap: Template '{t.templateId}' mesh '{t.mesh.name}' has " +
                        $"{t.mesh.subMeshCount} submeshes. Only submesh 0 will be rendered.",
                        forceLog: true);
                }

                if (!t.material.enableInstancing)
                {
                    t.material.enableInstancing = true;
                    DebugSettings.LogRendering(
                        $"RelicVisualBootstrap: Forced enableInstancing=true on '{t.templateId}' material.",
                        forceLog: true);
                }

                if (t.impostorMaterial != null && !t.impostorMaterial.enableInstancing)
                {
                    t.impostorMaterial.enableInstancing = true;
                }

                // Track largest mesh for LOD derivation
                float scale = Mathf.Max(0.01f, Mathf.Abs(t.scale));
                var extents = t.mesh.bounds.extents;
                float worldRadius = math.length(new float3(extents.x, extents.y, extents.z)) * scale;
                if (worldRadius > maxWorldRadius)
                    maxWorldRadius = worldRadius;

                configTemplates.Add(new RelicTemplateEntry
                {
                    TemplateId = t.templateId,
                    Mesh = t.mesh,
                    Material = t.material,
                    UniformScale = t.scale,
                    YOffset = t.yOffset,
                    ImpostorMesh = t.impostorMesh,
                    ImpostorMaterial = t.impostorMaterial,
                    ImpostorScale = t.impostorScale,
                });
            }

            DeriveLodParameters(
                maxWorldRadius,
                out float resolvedSwap,
                out float resolvedHysteresis,
                out float farClipPlane);

            var em = world.EntityManager;
            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                em,
                new RelicRenderConfig
                {
                    Templates = configTemplates,
                    LodSwapDistance = resolvedSwap,
                    LodHysteresis = resolvedHysteresis,
                },
                nameof(RelicRenderConfig));

            DebugSettings.LogRendering(
                $"RelicVisualBootstrap: {configTemplates.Count} template(s) registered, " +
                $"farClip={farClipPlane:0.0}, maxWorldRadius={maxWorldRadius:0.0}, " +
                $"LodSwapDistance={resolvedSwap:0.0}, LodHysteresis={resolvedHysteresis:0.0}",
                forceLog: true);

            if (maxWorldRadius > farClipPlane * 0.5f)
            {
                DebugSettings.LogWarning(
                    $"RelicVisualBootstrap: Largest relic world-space bounding radius ({maxWorldRadius:0.0}) " +
                    $"exceeds half the camera far clip ({farClipPlane:0.0}). Consider authoring the FBX at " +
                    "a smaller size or increasing the camera far clip.");
            }
        }

        private void DeriveLodParameters(
            float fullMeshWorldRadius,
            out float resolvedSwap,
            out float resolvedHysteresis,
            out float farClipPlane)
        {
            var camera = Camera.main;
            farClipPlane = camera != null ? camera.farClipPlane : 2000f;

            float safeViewDistance = Mathf.Max(100f, farClipPlane - fullMeshWorldRadius - SafetyMarginWorldUnits);

            if (lodSwapDistance > 0f)
            {
                resolvedSwap = lodSwapDistance;
            }
            else
            {
                resolvedSwap = Mathf.Clamp(safeViewDistance * 0.8f, 200f, farClipPlane * 0.6f);
            }

            if (lodHysteresis > 0f)
            {
                resolvedHysteresis = lodHysteresis;
            }
            else
            {
                resolvedHysteresis = Mathf.Max(resolvedSwap * 0.05f, 20f);
            }
        }
    }
}
