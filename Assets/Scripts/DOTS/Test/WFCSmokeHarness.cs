using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Minimal, visual WFC smoke test. Attach to any scene GameObject and press Play.
    /// It will:
    /// - Create a WFC entity configured for a grid
    /// - Create WFCCell entities for each grid cell
    /// - Issue a DungeonGenerationRequest so rendering kicks in
    /// Prefab selection will use the baked DungeonPrefabRegistry if present (FBX macro tiles).
    /// </summary>
    public class WFCSmokeHarness : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int gridWidth = 12;
        public int gridHeight = 12;
        public float cellSize = 1.0f;

        [Header("Execution")]
        public bool runOnStart = true;
        public bool enableDebugLogs = false;
        public bool enableWFCDebug = false;
        public bool enableRenderingDebug = false;

        private Entity wfcEntity;
        private Entity requestEntity;
        private World dotsWorld;

        void Start()
        {
            if (runOnStart)
            {
                Run();
            }
        }

        [ContextMenu("Run WFC Smoke Test")]
        public void Run()
        {
            // Apply debug settings based on inspector toggles
            DebugSettings.EnableDebugLogging = enableDebugLogs;
            DebugSettings.EnableWFCDebug = enableWFCDebug;
            DebugSettings.EnableRenderingDebug = enableRenderingDebug;

            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                DebugSettings.LogError("[WFCSmokeHarness] No DOTS world found.");
                return;
            }

            var em = dotsWorld.EntityManager;

            // Create WFC controller entity
            if (wfcEntity == Entity.Null)
            {
                wfcEntity = em.CreateEntity();
            }

            // WFC component (patterns/constraints will be initialized lazily by HybridWFCSystem)
            var wfc = WFCBuilder.CreateWFCComponent(new int2(gridWidth, gridHeight), 1, cellSize);
            wfc.maxIterations = math.max(1, gridWidth * gridHeight * 2);
            em.AddComponentData(wfcEntity, wfc);

            // Generation settings
            var settings = WFCBuilder.CreateDefaultSettings();
            settings.maxIterations = wfc.maxIterations;
            em.AddComponentData(wfcEntity, settings);

            // Optional performance data container
            if (!em.HasComponent<WFCPerformanceData>(wfcEntity))
            {
                em.AddComponentData(wfcEntity, new WFCPerformanceData());
            }

            // Create WFCCell entities for grid
            CreateCells(em, gridWidth, gridHeight);

            // Ensure rendering systems are gated ON via request
            if (requestEntity == Entity.Null)
            {
                requestEntity = em.CreateEntity();
                em.AddComponentData(requestEntity, new DOTS.Terrain.WFC.DungeonGenerationRequest
                {
                    isActive = true,
                    position = float3.zero,
                    size = new int2(gridWidth, gridHeight),
                    cellSize = cellSize
                });
            }

            DebugSettings.Log($"[WFCSmokeHarness] Initialized {gridWidth}x{gridHeight} WFC with cellSize={cellSize}. Waiting for collapse and rendering...");
        }

        private static void CreateCells(EntityManager em, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cellEntity = em.CreateEntity();
                    var cell = new WFCCell
                    {
                        position = new int2(x, y),
                        collapsed = false,
                        entropy = 0f,
                        selectedPattern = -1,
                        patternCount = 0,
                        needsUpdate = true,
                        visualized = false,
                        possiblePatternsMask = 0u // HybridWFCSystem will initialize this based on pattern blob
                    };
                    em.AddComponentData(cellEntity, cell);
                }
            }
        }
    }
}



