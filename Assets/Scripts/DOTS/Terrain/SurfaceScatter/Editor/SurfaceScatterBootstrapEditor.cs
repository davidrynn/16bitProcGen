using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DOTS.Terrain.Pebbles;
using DOTS.Terrain.Rocks;
using DOTS.Terrain.SurfaceScatter;
using DOTS.Terrain.Trees;
using UnityEditor;
using UnityEngine;

namespace DOTS.Terrain.SurfaceScatter.EditorTools
{
    // Thin type bindings — Unity needs one [CustomEditor] per inspected type; all behaviour
    // lives in the shared base, driven by ISurfaceScatterVisualBootstrap metadata.
    [CustomEditor(typeof(TreeVisualBootstrap))]
    public sealed class TreeVisualBootstrapEditor : SurfaceScatterBootstrapEditor { }

    [CustomEditor(typeof(RockVisualBootstrap))]
    public sealed class RockVisualBootstrapEditor : SurfaceScatterBootstrapEditor { }

    [CustomEditor(typeof(PebbleVisualBootstrap))]
    public sealed class PebbleVisualBootstrapEditor : SurfaceScatterBootstrapEditor { }

    /// <summary>
    /// Shared inspector for the scatter visual bootstraps (tree / rock / pebble). Surfaces three
    /// things the default inspector hides: a prominent enable toggle, a "what's wired" status box
    /// (mesh / material / far-LOD / render-system config), and a one-click button that wires the
    /// near (and matching <c>_Far</c>) meshes straight from the bootstrap's default model.
    /// All field access goes through <see cref="ISurfaceScatterVisualBootstrap"/> so this one editor
    /// serves every scatter type without knowing their concrete field names.
    /// </summary>
    public class SurfaceScatterBootstrapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (target is not ISurfaceScatterVisualBootstrap meta)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            var enabledProp = serializedObject.FindProperty(meta.FeatureEnabledFieldName);
            if (enabledProp != null)
            {
                EditorGUILayout.PropertyField(
                    enabledProp, new GUIContent($"Enable {meta.ScatterDisplayName}"));
            }

            DrawStatusBox(meta, enabledProp);
            DrawAutoWireButton(meta);

            EditorGUILayout.Space();
            // Draw the remaining serialized fields normally; the toggle is already drawn above.
            DrawPropertiesExcluding(serializedObject, "m_Script", meta.FeatureEnabledFieldName);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatusBox(ISurfaceScatterVisualBootstrap meta, SerializedProperty enabledProp)
        {
            var near = serializedObject.FindProperty(meta.NearMeshVariantsFieldName);
            var far = serializedObject.FindProperty(meta.FarMeshVariantsFieldName);
            var material = serializedObject.FindProperty(meta.MaterialFieldName);
            var swap = serializedObject.FindProperty(meta.LodSwapDistanceFieldName);
            var legacy = string.IsNullOrEmpty(meta.LegacySingleMeshFieldName)
                ? null
                : serializedObject.FindProperty(meta.LegacySingleMeshFieldName);

            int nearCount = near is { isArray: true } ? near.arraySize : 0;
            bool legacySet = legacy != null && legacy.objectReferenceValue != null;
            bool meshWired = nearCount > 0 || legacySet;
            bool materialWired = material != null && material.objectReferenceValue != null;
            int farCount = CountNonNull(far);
            float swapDistance = swap != null ? swap.floatValue : 0f;
            bool? configEnabled = ReadConfigFlag(meta.RenderSystemConfigFlagName);
            bool featureEnabled = enabledProp == null || enabledProp.boolValue;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Wired Status", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string meshDetail = nearCount > 0
                    ? $"{nearCount} variant(s)"
                    : (legacySet ? "legacy single mesh" : "NONE");
                StatusLine("Mesh", meshWired, meshDetail);
                StatusLine("Material", materialWired, materialWired ? "OK" : "NONE");

                string farDetail = farCount > 0
                    ? $"{farCount} far mesh(es), swap @ {swapDistance:0.#}m"
                    : (swapDistance > 0f ? "none — swap set but no far mesh" : "none — LOD off");
                EditorGUILayout.LabelField("Far LOD", farDetail);

                string configDetail = configEnabled switch
                {
                    null => "unknown (no ProjectFeatureConfig asset found)",
                    true => "ENABLED",
                    false => "disabled",
                };
                EditorGUILayout.LabelField(
                    $"Render system ({meta.RenderSystemConfigFlagName})", configDetail);
            }

            if (featureEnabled && !meshWired)
            {
                EditorGUILayout.HelpBox(
                    $"{meta.ScatterDisplayName} is enabled but no mesh is wired — nothing will render. " +
                    "Assign a mesh below, or use the auto-wire button.", MessageType.Warning);
            }

            if (featureEnabled && configEnabled == false)
            {
                EditorGUILayout.HelpBox(
                    $"This bootstrap is enabled, but ProjectFeatureConfig.{meta.RenderSystemConfigFlagName} " +
                    "is OFF — the render system is never created, so nothing draws.", MessageType.Warning);
            }

            if (swapDistance > 0f && farCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "LOD swap distance is set but no far meshes exist; every instance pays the distance " +
                    "check and still draws the near mesh. Set swap distance to 0, or assign far meshes.",
                    MessageType.Info);
            }
        }

        private void DrawAutoWireButton(ISurfaceScatterVisualBootstrap meta)
        {
            string path = meta.DefaultModelAssetPath;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            bool assetExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
            using (new EditorGUI.DisabledScope(!assetExists))
            {
                string label = assetExists
                    ? $"Auto-wire meshes from {Path.GetFileName(path)}"
                    : $"(missing) {Path.GetFileName(path)}";
                if (GUILayout.Button(label))
                {
                    AutoWireFromModel(meta, path);
                }
            }

            if (!assetExists)
            {
                EditorGUILayout.HelpBox($"Default model not found at {path}.", MessageType.None);
            }
        }

        private void AutoWireFromModel(ISurfaceScatterVisualBootstrap meta, string path)
        {
            // Split sub-meshes into near vs far by the "_Far" suffix convention (SURFACE_SCATTER_LOD_SPEC
            // §4.2), then write parallel arrays so far[i] matches near[i] by base name.
            var nearMeshes = new List<Mesh>();
            var farByBaseName = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Mesh mesh)
                {
                    continue;
                }

                if (mesh.name.EndsWith("_Far", StringComparison.OrdinalIgnoreCase))
                {
                    farByBaseName[mesh.name[..^4]] = mesh;
                }
                else
                {
                    nearMeshes.Add(mesh);
                }
            }

            if (nearMeshes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Auto-wire scatter meshes",
                    $"No mesh sub-assets found in {Path.GetFileName(path)}.\n\n" +
                    "If the model was just added, let Unity finish importing it and try again.",
                    "OK");
                return;
            }

            var near = serializedObject.FindProperty(meta.NearMeshVariantsFieldName);
            near.arraySize = nearMeshes.Count;
            for (int i = 0; i < nearMeshes.Count; i++)
            {
                near.GetArrayElementAtIndex(i).objectReferenceValue = nearMeshes[i];
            }

            var far = serializedObject.FindProperty(meta.FarMeshVariantsFieldName);
            if (far != null)
            {
                far.arraySize = nearMeshes.Count;
                for (int i = 0; i < nearMeshes.Count; i++)
                {
                    far.GetArrayElementAtIndex(i).objectReferenceValue =
                        farByBaseName.TryGetValue(nearMeshes[i].name, out var farMesh) ? farMesh : null;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void StatusLine(string label, bool ok, string detail)
        {
            EditorGUILayout.LabelField($"{(ok ? "✔" : "✘")} {label}", detail);
        }

        private static int CountNonNull(SerializedProperty arrayProp)
        {
            if (arrayProp is not { isArray: true })
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool? ReadConfigFlag(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:ProjectFeatureConfig");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            var config = AssetDatabase.LoadAssetAtPath<ProjectFeatureConfig>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            if (config == null)
            {
                return null;
            }

            var field = typeof(ProjectFeatureConfig).GetField(
                flagName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(bool))
            {
                return null;
            }

            return (bool)field.GetValue(config);
        }
    }
}
