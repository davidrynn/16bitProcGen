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
            }

            return cached;
        }

        public static void ResetCache()
        {
            cached = null;
            OverrideSettings = null;
        }
    }
}
