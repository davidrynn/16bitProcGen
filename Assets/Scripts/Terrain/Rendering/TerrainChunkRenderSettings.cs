using UnityEngine;

namespace DOTS.Terrain.Rendering
{
    [CreateAssetMenu(menuName = "DOTS Terrain/Terrain Chunk Render Settings", fileName = "TerrainChunkRenderSettings")]
    public class TerrainChunkRenderSettings : ScriptableObject
    {
        [SerializeField]
        private Material chunkMaterial;

        public Material ChunkMaterial => chunkMaterial;

        public void SetChunkMaterial(Material material)
        {
            chunkMaterial = material;
        }
    }

    public static class TerrainChunkRenderSettingsProvider
    {
        private const string ResourcePath = "Terrain/TerrainChunkRenderSettings";

        private static TerrainChunkRenderSettings cached;
        private static bool loggedMissing;
        private static bool loggedStandardFix;

        /// <summary>
        /// Optional override used by tests to inject settings without touching Resources.
        /// </summary>
        public static TerrainChunkRenderSettings OverrideSettings { get; set; }

        public static TerrainChunkRenderSettings GetOrLoad()
        {
            if (OverrideSettings != null)
            {
                return OverrideSettings;
            }

            if (cached == null)
            {
                cached = Resources.Load<TerrainChunkRenderSettings>(ResourcePath);

                if (cached == null)
                {
                    cached = CreateRuntimeFallbackSettings();

                    if (!loggedMissing)
                    {
                        loggedMissing = true;
                        Debug.LogWarning($"[DOTS Terrain] Missing TerrainChunkRenderSettings at Resources/{ResourcePath}. Using a runtime fallback material. Create Assets/Resources/{ResourcePath}.asset and assign a material for proper control.");
                    }
                }
            }

#if UNITY_ENTITIES_GRAPHICS
            if (cached != null)
            {
                TryFixStandardShaderInPlace(cached);
            }
#endif

            return cached;
        }

        public static void ResetCache()
        {
            cached = null;
            OverrideSettings = null;
            loggedMissing = false;
            loggedStandardFix = false;
        }

        private static TerrainChunkRenderSettings CreateRuntimeFallbackSettings()
        {
            var settings = ScriptableObject.CreateInstance<TerrainChunkRenderSettings>();
            settings.hideFlags = HideFlags.DontSave;
            settings.name = "TerrainChunkRenderSettings (Runtime Fallback)";

            // Entities Graphics uses BatchRendererGroup; prefer SRP shaders.
            // Do NOT fall back to the Built-in "Standard" shader (not BRG compatible).
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                // Leave material null; upload system will warn and skip until user assigns a compatible material.
                return settings;
            }

            var material = new Material(shader)
            {
                name = "TerrainChunkMaterial (Runtime Fallback)",
                hideFlags = HideFlags.DontSave
            };
            settings.SetChunkMaterial(material);

            return settings;
        }

#if UNITY_ENTITIES_GRAPHICS
        private static void TryFixStandardShaderInPlace(TerrainChunkRenderSettings settings)
        {
            if (settings == null || settings.ChunkMaterial == null)
            {
                return;
            }

            var mat = settings.ChunkMaterial;
            var shader = mat.shader;
            if (shader == null || shader.name != "Standard")
            {
                return;
            }

            // Prefer URP shaders (BRG/SRP-batcher compatible) if available.
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                urpShader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (urpShader == null)
            {
                if (!loggedStandardFix)
                {
                    loggedStandardFix = true;
                    Debug.LogWarning("[DOTS Terrain] Terrain chunk material uses Built-in 'Standard' shader, but no URP shader was found to auto-fix it. Assign a URP shader (URP Lit/Unlit) to your TerrainChunkRenderSettings material to render with Entities Graphics.");
                }
                return;
            }

            // Switch shader in-place so any existing RenderMeshArray batches pick it up without needing a re-upload.
            var color = Color.white;
            if (mat.HasProperty("_Color"))
            {
                color = mat.GetColor("_Color");
            }

            mat.shader = urpShader;

            // Try to preserve base color when moving from Standard -> URP.
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            else if (mat.HasProperty("_BaseMap") && mat.HasProperty("_Color"))
            {
                // No-op; keep whatever URP default is.
            }

            if (!loggedStandardFix)
            {
                loggedStandardFix = true;
                Debug.LogWarning("[DOTS Terrain] Auto-switched TerrainChunkRenderSettings material from Built-in 'Standard' to a URP shader for Entities Graphics compatibility. Consider updating the material asset to a URP shader permanently.");
            }
        }
#endif
    }
}
