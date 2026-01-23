using DOTS.Terrain.Core;
using DOTS.Terrain.WFC;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test system to spawn all dungeon models in a line without rotation
    /// to verify their base orientations match expected socket patterns
    /// </summary>
    public class ModelAlignmentTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool runTest = false;
        public float spacing = 2.0f;
        public float yOffset = 0.0f;
        public float cellSize = 1.0f;

        [Header("Prefab References")]
        public GameObject doorPrefab;
        public GameObject corridorPrefab;
        public GameObject cornerPrefab;
        public GameObject floorPrefab;
        public GameObject wallPrefab;

        private List<GameObject> spawnedModels = new List<GameObject>();

        private void Start()
        {
            if (runTest)
            {
                RunModelAlignmentTest();
            }
        }

        private void RunModelAlignmentTest()
        {
            DebugSettings.LogTest("=== MODEL ALIGNMENT TEST STARTING ===");
            DebugSettings.LogTest("Spawning all models in a line without rotation to check base orientations");

            float xPosition = 0f;
            int modelIndex = 0;
            float adjustedSpacing = spacing + cellSize;
            // Test Door/DeadEnd models with different socket patterns
            if (doorPrefab != null)
            {
                SpawnModelWithLabel(doorPrefab, xPosition, "Door (Base)", "Expected: One open side should face +Z");
                xPosition += adjustedSpacing;
                modelIndex++;
            }

            // Test Corridor models
            if (corridorPrefab != null)
            {
                SpawnModelWithLabel(corridorPrefab, xPosition, "Corridor (Base)", "Expected: Two opposite open sides should face +Z and -Z");
                xPosition += adjustedSpacing;
                modelIndex++;
            }

            // Test Corner models
            if (cornerPrefab != null)
            {
                SpawnModelWithLabel(cornerPrefab, xPosition, $"Corner (Base) -{cornerPrefab.transform.rotation.ToString()}", "Expected: Two adjacent open sides should face +Z and +X");
                xPosition += adjustedSpacing;
                modelIndex++;
            }

            // Test Floor models
            if (floorPrefab != null)
            {
                SpawnModelWithLabel(floorPrefab, xPosition, "Floor (Base)", "Expected: All sides open (FFFF)");
                xPosition += adjustedSpacing;
                modelIndex++;
            }

            // Test Wall models
            if (wallPrefab != null)
            {
                SpawnModelWithLabel(wallPrefab, xPosition, "Wall (Base)", "Expected: All sides closed (WWWW)");
                xPosition += adjustedSpacing;
                modelIndex++;
            }

            DebugSettings.LogTest("=== MODEL ALIGNMENT TEST COMPLETE ===");
            DebugSettings.LogTest($"Spawned {modelIndex} models in a line at Y={yOffset}");
            DebugSettings.LogTest("Check the scene view to verify model orientations match expected socket patterns");
            DebugSettings.LogTest("Expected orientations:");
            DebugSettings.LogTest("- Door: One open side should face +Z (forward)");
            DebugSettings.LogTest("- Corridor: Two opposite open sides should face +Z and -Z");
            DebugSettings.LogTest("- Corner: Two adjacent open sides should face +Z and +X");
            DebugSettings.LogTest("- Floor: All sides should be open");
            DebugSettings.LogTest("- Wall: All sides should be closed");
        }

        private void SpawnModelWithLabel(GameObject prefab, float xPosition, string label, string expected)
        {
            Vector3 position = new Vector3(xPosition, yOffset, 0f);
             GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            // Add to tracking list
            spawnedModels.Add(instance);

            // Add a label to identify the model
            instance.name = label;

            DebugSettings.LogTest($"Spawned {label} at position ({xPosition}, {yOffset}, 0) - {expected}");
        }

        [ContextMenu("Run Model Alignment Test")]
        public void RunTestFromContextMenu()
        {
            RunModelAlignmentTest();
        }

        [ContextMenu("Clear models")]
        public void ClearTestModels()
        {
            // Destroy all tracked models
            foreach (GameObject model in spawnedModels)
            {
                if (model != null)
                {
                    DestroyImmediate(model);
                }
            }

            // Clear the list
            spawnedModels.Clear();

            DebugSettings.LogTest($"Cleared {spawnedModels.Count} test models spawned by ModelAlignmentTest.");
        }
    }
}
