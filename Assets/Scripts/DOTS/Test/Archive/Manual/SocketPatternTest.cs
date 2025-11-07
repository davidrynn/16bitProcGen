using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test system to spawn models with specific socket patterns and show expected rotations
    /// </summary>
    public class SocketPatternTest : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool runTest = false;
        public float spacing = 3.0f;
        public float yOffset = 0.0f;
        
        [Header("Prefab References")]
        public GameObject doorPrefab;
        public GameObject corridorPrefab;
        public GameObject cornerPrefab;

        private void Start()
        {
            if (runTest)
            {
                RunSocketPatternTest();
            }
        }

        private void RunSocketPatternTest()
        {
            Debug.Log("=== SOCKET PATTERN TEST STARTING ===");
            Debug.Log("Spawning models with specific socket patterns to show expected rotations");
            
            float xPosition = 0f;
            int modelIndex = 0;

            // Test Door/DeadEnd patterns
            if (doorPrefab != null)
            {
                Debug.Log("=== DOOR/DEADEND PATTERNS ===");
                
                // FWWW - North open (should face +Z, rotation 0°)
                SpawnModelWithRotation(doorPrefab, xPosition, "Door FWWW", "North open", Quaternion.identity, "Should face +Z (forward)");
                xPosition += spacing;
                modelIndex++;

                // WFWW - East open (should face +X, rotation 90°)
                SpawnModelWithRotation(doorPrefab, xPosition, "Door WFWW", "East open", Quaternion.Euler(0, 90, 0), "Should face +X (right)");
                xPosition += spacing;
                modelIndex++;

                // WWFW - South open (should face -Z, rotation 180°)
                SpawnModelWithRotation(doorPrefab, xPosition, "Door WWFW", "South open", Quaternion.Euler(0, 180, 0), "Should face -Z (backward)");
                xPosition += spacing;
                modelIndex++;

                // WWWF - West open (should face -X, rotation 270°)
                SpawnModelWithRotation(doorPrefab, xPosition, "Door WWWF", "West open", Quaternion.Euler(0, 270, 0), "Should face -X (left)");
                xPosition += spacing;
                modelIndex++;
            }

            // Test Corridor patterns
            if (corridorPrefab != null)
            {
                Debug.Log("=== CORRIDOR PATTERNS ===");
                
                // FWFW - North-South open (should face +Z/-Z, rotation 0°)
                SpawnModelWithRotation(corridorPrefab, xPosition, "Corridor FWFW", "North-South open", Quaternion.identity, "Should face +Z and -Z");
                xPosition += spacing;
                modelIndex++;

                // WFW - East-West open (should face +X/-X, rotation 90°)
                SpawnModelWithRotation(corridorPrefab, xPosition, "Corridor WFW", "East-West open", Quaternion.Euler(0, 90, 0), "Should face +X and -X");
                xPosition += spacing;
                modelIndex++;
            }

            // Test Corner patterns
            if (cornerPrefab != null)
            {
                Debug.Log("=== CORNER PATTERNS ===");
                
                // FFWW - North-East open (should face +Z/+X, rotation 0°)
                SpawnModelWithRotation(cornerPrefab, xPosition, "Corner FFWW", "North-East open", Quaternion.identity, "Should face +Z and +X");
                xPosition += spacing;
                modelIndex++;

                // WFFW - East-South open (should face +X/-Z, rotation 90°)
                SpawnModelWithRotation(cornerPrefab, xPosition, "Corner WFFW", "East-South open", Quaternion.Euler(0, 90, 0), "Should face +X and -Z");
                xPosition += spacing;
                modelIndex++;

                // WWFF - South-West open (should face -Z/-X, rotation 180°)
                SpawnModelWithRotation(cornerPrefab, xPosition, "Corner WWFF", "South-West open", Quaternion.Euler(0, 180, 0), "Should face -Z and -X");
                xPosition += spacing;
                modelIndex++;

                // FWWF - West-North open (should face -X/+Z, rotation 270°)
                SpawnModelWithRotation(cornerPrefab, xPosition, "Corner FWWF", "West-North open", Quaternion.Euler(0, 270, 0), "Should face -X and +Z");
                xPosition += spacing;
                modelIndex++;
            }

            Debug.Log("=== SOCKET PATTERN TEST COMPLETE ===");
            Debug.Log($"Spawned {modelIndex} models with specific socket patterns and rotations");
            Debug.Log("Check the scene view to verify:");
            Debug.Log("1. Base model orientations (first row)");
            Debug.Log("2. Applied rotations make models face correct directions");
            Debug.Log("3. Socket patterns match visual geometry");
        }

        private void SpawnModelWithRotation(GameObject prefab, float xPosition, string label, string socketPattern, Quaternion rotation, string expected)
        {
            Vector3 position = new Vector3(xPosition, yOffset, 0f);
            GameObject instance = Instantiate(prefab, position, rotation);
            
            // Add a label to identify the model
            instance.name = $"{label} ({socketPattern})";
            
            Debug.Log($"Spawned {label} at ({xPosition}, {yOffset}, 0) with rotation {rotation.eulerAngles} - {expected}");
        }

        [ContextMenu("Run Socket Pattern Test")]
        public void RunTestFromContextMenu()
        {
            RunSocketPatternTest();
        }
    }
}
