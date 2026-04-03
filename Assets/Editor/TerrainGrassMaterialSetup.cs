using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Terrain.Rendering;

namespace DOTS.Terrain.Editor
{
    public static class TerrainGrassMaterialSetup
    {
        private const string GrassMaterialPath = "Assets/Resources/Terrain/TerrainChunkGrassMaterial.mat";
        private const string ShaderName = "BruteForceURP/InteractiveGrassURP";

        // -------------------------------------------------------------------------
        // Create / update the grass material asset
        // -------------------------------------------------------------------------

        [MenuItem("DOTS Terrain/Create Grass Material")]
        public static void CreateGrassMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogError($"[TerrainGrassMaterialSetup] Shader '{ShaderName}' not found. Ensure BruteForce-GrassShader is imported correctly.");
                return;
            }

            // Update existing material if present; create new otherwise.
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            bool isNew = material == null;
            if (isNew)
            {
                material = new Material(shader);
                material.name = "TerrainChunkGrassMaterial";
            }
            else
            {
                material.shader = shader;
            }

            // --- Textures: copy from the bundled URP terrain demo material ---
            // The demo material ships with the BruteForce package and has all required
            // textures wired up. Without these the geometry shader produces solid-coloured
            // blobs rather than individual grass blades (_GrassTex drives blade silhouettes,
            // _Noise drives per-blade colour variation, _Distortion drives wind movement).
            const string demoMatPath = "Assets/BruteForce-GrassShader/Materials/URP/Terrain/URPBFGrassTerrain01.mat";
            var demoMat = AssetDatabase.LoadAssetAtPath<Material>(demoMatPath);
            if (demoMat != null)
            {
                material.SetTexture("_MainTex",    demoMat.GetTexture("_MainTex"));
                material.SetTexture("_GrassTex",   demoMat.GetTexture("_GrassTex"));
                material.SetTexture("_GroundTex",  demoMat.GetTexture("_GroundTex"));
                material.SetTexture("_Noise",      demoMat.GetTexture("_Noise"));
                material.SetTexture("_Distortion", demoMat.GetTexture("_Distortion"));
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[TerrainGrassMaterialSetup] Demo material not found at {demoMatPath}. " +
                                             "Grass will render as solid blobs until textures are assigned manually.");
            }

            // --- Keywords ---
            // USE_WC: World Coordinates — derives UVs from worldPos.xz / WorldScale.
            // Critical for Surface Nets meshes, which have no authored UV channel.
            // (The demo terrain material does not use USE_WC because Unity Terrain has UVs;
            // we need it here because Surface Nets meshes have no UV channel.)
            material.EnableKeyword("USE_WC");
            material.SetFloat("_UseWC", 1f);

            material.EnableKeyword("USE_S");
            material.SetFloat("_UseShadow", 1f);

            material.EnableKeyword("USE_SC");
            material.SetFloat("_UseShadowCast", 1f);

            // RT interactive effect disabled by default — requires the GrassInteractiveEffectsBootstrap
            // scene rig. Enable manually on the material once that rig is set up.
            material.DisableKeyword("USE_RT");
            material.SetFloat("_UseRT", 0f);

            // --- 16-bit retro grass colors (muted, earthy) ---
            // _Color is kept near-white so the grass texture's own colour shows through;
            // tinting happens via the other color properties.
            material.SetColor("_Color",                new Color(0.55f, 0.85f, 0.40f, 1f)); // mid-green
            material.SetColor("_GroundColor",          new Color(0.35f, 0.28f, 0.15f, 1f)); // earthy brown
            material.SetColor("_SelfShadowColor",      new Color(0.32f, 0.50f, 0.22f, 1f)); // lighter green shadow — reduces seam contrast
            material.SetColor("_ProjectedShadowColor", new Color(0.28f, 0.38f, 0.15f, 1f)); // lighter olive shadow
            material.SetColor("_RimColor",             new Color(0.20f, 0.38f, 0.12f, 1f)); // green rim

            // --- World-space UV scale (larger value = fewer texture repeats per world unit) ---
            material.SetFloat("_WorldScale",    8f);
            material.SetFloat("_WorldRotation", 0f);

            // --- Geometry / grass shape ---
            // _OffsetValue controls blade height. The geometry shader multiplies this by 0.01
            // internally, so the actual world-unit height is: (_OffsetValue * 0.01 * _NumberOfStacks).
            // At 80f with 17 stacks: 0.80 * 17 = ~13.6 world units of blade height.
            // Tune this relative to your terrain chunk scale.
            // _OffsetValue * 0.01 * _NumberOfStacks = total blade height in world units.
            // 12 * 0.01 * 12 = ~1.4 world units — short lawn-height grass.
            material.SetFloat("_OffsetValue",          3f);
            material.SetFloat("_NumberOfStacks",       12f);
            material.SetFloat("_MinimumNumberStacks",  2f);
            material.SetFloat("_FadeDistanceStart",    100f);
            material.SetFloat("_FadeDistanceEnd",      200f);
            // Higher thinness makes each triangle's grass slice expand outward, overlapping
            // adjacent patches and filling the gaps that create the visible grid from distance.
            material.SetFloat("_GrassThinness",             2.0f);
            // Thinness at the base — slightly wider than default so patches merge at ground level.
            material.SetFloat("_GrassThinnessIntersection", 0.6f);
            material.SetFloat("_GrassSaturation",      1.8f);
            // Low shading removes the dark shadow at each triangle base that aligns with the
            // Surface Nets grid and creates visible straight lines across the terrain.
            // 0.044 produced parallel dark seams; near-zero keeps subtle depth without grid lines.
            material.SetFloat("_GrassShading",         0.008f);
            // Moderate noise — enough variation between patches to break up the identical-tuft
            // repetition without causing the blotchy dark/light patches we had at 7.
            material.SetFloat("_NoisePower",           5f);
            material.SetFloat("_TilingN1",             6.0f);
            // More wind movement helps break the static geometric appearance at distance.
            material.SetFloat("_WindForce",    0.5f);
            material.SetFloat("_WindMovement", 0.3f);


            if (isNew)
                AssetDatabase.CreateAsset(material, GrassMaterialPath);
            else
                EditorUtility.SetDirty(material);

            AssetDatabase.SaveAssets();

            // NOTE: This material is NOT assigned to TerrainChunkRenderSettings.
            // The BruteForce grass shader uses a geometry shader and non-SRP-Batcher-compatible
            // cbuffers, making it incompatible with Entities Graphics / BatchRendererGroup.
            // GrassChunkRenderSystem renders it separately via Graphics.DrawMesh.
            // TerrainChunkRenderSettings should keep a standard URP Lit material for ECS rendering.
            UnityEngine.Debug.Log($"[TerrainGrassMaterialSetup] Grass material {(isNew ? "created" : "updated")} at {GrassMaterialPath}. " +
                                  "Add a GrassChunkRenderSystem scene object and run '[POC] Tag All Chunks as Grass Surface' to see grass.");

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }

        // -------------------------------------------------------------------------
        // POC: Tag all currently loaded terrain chunks as grass surfaces at runtime.
        //
        // Production replacement: TerrainChunkGrassSurface should be added during
        // terrain generation when a chunk column is identified as the topmost solid
        // layer, combined with biome/material data to determine surface type
        // (grass, sand, snow, etc.) and initial Density value.
        // -------------------------------------------------------------------------

        // Validate: only enable the menu item while in Play mode (ECS world must be running).
        [MenuItem("DOTS Terrain/[POC] Tag All Chunks as Grass Surface", validate = true)]
        private static bool TagAllChunksValidate() => Application.isPlaying;

        [MenuItem("DOTS Terrain/[POC] Tag All Chunks as Grass Surface")]
        public static void TagAllChunksAsGrassSurface()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                UnityEngine.Debug.LogWarning("[TerrainGrassMaterialSetup] No active DOTS world found.");
                return;
            }

            var em = world.EntityManager;

            // Find all terrain chunk entities. TerrainChunkMeshData is present on every
            // chunk entity regardless of whether its mesh has been uploaded yet.
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunkMeshData>());
            var entities = query.ToEntityArray(Allocator.Temp);
            query.Dispose();

            int tagged = 0;
            int alreadyTagged = 0;

            foreach (var entity in entities)
            {
                if (em.HasComponent<TerrainChunkGrassSurface>(entity))
                {
                    alreadyTagged++;
                    continue;
                }

                // Default density = 1 (full). Future: derive from biome/altitude data.
                em.AddComponentData(entity, TerrainChunkGrassSurface.Default);
                tagged++;
            }

            entities.Dispose();

            UnityEngine.Debug.Log($"[TerrainGrassMaterialSetup] [POC] Tagged {tagged} chunk(s) as grass surface " +
                                  $"({alreadyTagged} were already tagged). " +
                                  "GrassChunkRenderSystem will draw grass on uploaded chunks next frame.");
        }
    }
}
