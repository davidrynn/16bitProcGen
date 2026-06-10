using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DOTS.Test
{
    /// <summary>
    /// Comprehensive tool for discovering and debugging DOTS systems
    /// Helps identify system registration issues and provides ongoing debugging capabilities
    /// </summary>
    public class SystemDiscoveryTool : MonoBehaviour
    {
        [Header("Discovery Settings")]
        [Tooltip("Automatically run discovery on start")]
        public bool autoDiscoverOnStart = true;
        [Tooltip("Log detailed system information")]
        public bool enableDetailedLogging = true;
        [Tooltip("Filter systems by name (leave empty for all)")]
        public string nameFilter = "";
        
        [Header("System Categories")]
        [Tooltip("Show managed systems (SystemBase)")]
        public bool showManagedSystems = true;
        [Tooltip("Show unmanaged systems (ISystem)")]
        public bool showUnmanagedSystems = true;
        [Tooltip("Show system groups")]
        public bool showSystemGroups = true;
        
        [Header("Player System Debug")]
        [Tooltip("Specifically check for player movement systems")]
        public bool checkPlayerSystems = true;
        [Tooltip("Check for systems in DOTS.Player namespace")]
        public bool checkPlayerNamespace = true;
        
        [Header("Debug Output")]
        [Tooltip("Show system discovery results in GUI")]
        public bool showGUI = true;
        [Tooltip("Log results to console")]
        public bool logToConsole = true;
        
        private World dotsWorld;
        private List<SystemInfo> discoveredSystems = new List<SystemInfo>();
        private List<SystemInfo> playerSystems = new List<SystemInfo>();
        private bool discoveryComplete = false;
        
        private struct SystemInfo
        {
            public string name;
            public string fullName;
            public string namespaceName;
            public SystemType type;
            public bool isPlayerSystem;
            public string systemGroup;
            public bool isActive;
        }
        
        private enum SystemType
        {
            Managed,    // SystemBase
            Unmanaged,  // ISystem
            Group,      // SystemGroup
            Unknown
        }
        
        void Start()
        {
            if (autoDiscoverOnStart)
            {
                // Wait for DOTS world to initialize
                Invoke(nameof(RunSystemDiscovery), 1f);
            }
        }
        
        [ContextMenu("Run System Discovery")]
        public void RunSystemDiscovery()
        {
            Debug.Log("=== STARTING SYSTEM DISCOVERY ===");
            
            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                Debug.LogError("DOTS World not available for system discovery");
                return;
            }
            
            discoveredSystems.Clear();
            playerSystems.Clear();
            
            DiscoverAllSystems();
            AnalyzePlayerSystems();
            LogDiscoveryResults();
            
            discoveryComplete = true;
            
            Debug.Log("=== SYSTEM DISCOVERY COMPLETE ===");
        }
        
        private void DiscoverAllSystems()
        {
            var systems = dotsWorld.Systems;
            Debug.Log($"Found {systems.Count} total systems in world");
            
            foreach (var system in systems)
            {
                var systemInfo = AnalyzeSystem(system);
                discoveredSystems.Add(systemInfo);
                
                if (enableDetailedLogging)
                {
                    LogSystemDetails(systemInfo);
                }
            }
        }
        
        private SystemInfo AnalyzeSystem(ComponentSystemBase system)
        {
            var systemType = system.GetType();
            var systemInfo = new SystemInfo
            {
                name = systemType.Name,
                fullName = systemType.FullName,
                namespaceName = systemType.Namespace ?? "Global",
                isActive = system.Enabled
            };
            
            // Determine system type
            if (systemType.IsSubclassOf(typeof(ComponentSystemGroup)))
            {
                systemInfo.type = SystemType.Group;
                systemInfo.systemGroup = "SystemGroup";
            }
            else if (systemType.IsSubclassOf(typeof(SystemBase)))
            {
                systemInfo.type = SystemType.Managed;
                systemInfo.systemGroup = GetSystemGroup(system);
            }
            else if (systemType.GetInterfaces().Any(i => i.Name == "ISystem"))
            {
                systemInfo.type = SystemType.Unmanaged;
                systemInfo.systemGroup = GetSystemGroup(system);
            }
            else
            {
                systemInfo.type = SystemType.Unknown;
                systemInfo.systemGroup = "Unknown";
            }
            
            // Check if it's a player system
            systemInfo.isPlayerSystem = IsPlayerSystem(systemInfo);
            
            return systemInfo;
        }
        
        private string GetSystemGroup(ComponentSystemBase system)
        {
            // Try to determine which system group this system belongs to
            var systemType = system.GetType();
            
            // Check for UpdateInGroup attributes
            var updateInGroupAttributes = systemType.GetCustomAttributes(typeof(UpdateInGroupAttribute), false);
            if (updateInGroupAttributes.Length > 0)
            {
                var attribute = (UpdateInGroupAttribute)updateInGroupAttributes[0];
                return attribute.GroupType.Name;
            }
            
            return "Default";
        }
        
        private bool IsPlayerSystem(SystemInfo systemInfo)
        {
            if (checkPlayerNamespace && systemInfo.namespaceName != null)
            {
                return systemInfo.namespaceName.Contains("Player") || 
                       systemInfo.namespaceName.Contains("DOTS.Player");
            }
            
            if (checkPlayerSystems)
            {
                return systemInfo.name.Contains("Player") ||
                       systemInfo.name.Contains("Input") ||
                       systemInfo.name.Contains("Movement") ||
                       systemInfo.name.Contains("Camera") ||
                       systemInfo.name.Contains("Grounding");
            }
            
            return false;
        }
        
        private void AnalyzePlayerSystems()
        {
            playerSystems = discoveredSystems.Where(s => s.isPlayerSystem).ToList();
            
            Debug.Log($"=== PLAYER SYSTEM ANALYSIS ===");
            Debug.Log($"Found {playerSystems.Count} player-related systems:");
            
            foreach (var system in playerSystems)
            {
                Debug.Log($"  - {system.name} ({system.type}) - {system.systemGroup} - Active: {system.isActive}");
            }
            
            // Check for expected player systems
            var expectedSystems = new[] { "PlayerInputSystem", "PlayerGroundingSystem", "PlayerMovementSystem", "PlayerCameraSystem" };
            var foundSystems = playerSystems.Select(s => s.name).ToArray();
            
            Debug.Log($"=== EXPECTED VS FOUND ===");
            foreach (var expected in expectedSystems)
            {
                bool found = foundSystems.Contains(expected);
                Debug.Log($"  {expected}: {(found ? "✅ FOUND" : "❌ MISSING")}");
            }
        }
        
        private void LogSystemDetails(SystemInfo systemInfo)
        {
            if (!string.IsNullOrEmpty(nameFilter) && !systemInfo.name.Contains(nameFilter))
                return;
                
            if (systemInfo.type == SystemType.Group && !showSystemGroups)
                return;
                
            if (systemInfo.type == SystemType.Managed && !showManagedSystems)
                return;
                
            if (systemInfo.type == SystemType.Unmanaged && !showUnmanagedSystems)
                return;
            
            var status = systemInfo.isActive ? "Active" : "Inactive";
            var playerTag = systemInfo.isPlayerSystem ? " [PLAYER]" : "";
            
            Debug.Log($"  {systemInfo.name} ({systemInfo.type}) - {systemInfo.systemGroup} - {status}{playerTag}");
        }
        
        private void LogDiscoveryResults()
        {
            if (!logToConsole) return;
            
            Debug.Log($"=== SYSTEM DISCOVERY SUMMARY ===");
            Debug.Log($"Total systems: {discoveredSystems.Count}");
            Debug.Log($"Managed systems: {discoveredSystems.Count(s => s.type == SystemType.Managed)}");
            Debug.Log($"Unmanaged systems: {discoveredSystems.Count(s => s.type == SystemType.Unmanaged)}");
            Debug.Log($"System groups: {discoveredSystems.Count(s => s.type == SystemType.Group)}");
            Debug.Log($"Player systems: {playerSystems.Count}");
            Debug.Log($"Active systems: {discoveredSystems.Count(s => s.isActive)}");
            
            // Group by namespace
            var namespaceGroups = discoveredSystems.GroupBy(s => s.namespaceName);
            Debug.Log($"=== SYSTEMS BY NAMESPACE ===");
            foreach (var group in namespaceGroups.OrderBy(g => g.Key))
            {
                Debug.Log($"  {group.Key}: {group.Count()} systems");
            }
        }
        
        [ContextMenu("Check Player Systems Only")]
        public void CheckPlayerSystemsOnly()
        {
            if (dotsWorld == null)
            {
                Debug.LogError("DOTS World not available");
                return;
            }
            
            Debug.Log("=== PLAYER SYSTEM CHECK ===");
            
            var playerSystemTypes = new[]
            {
                "DOTS.Player.Systems.PlayerInputSystem",
                "DOTS.Player.Systems.PlayerGroundingSystem", 
                "DOTS.Player.Systems.PlayerMovementSystem",
                "DOTS.Player.Systems.PlayerCameraSystem"
            };
            
            foreach (var systemTypeName in playerSystemTypes)
            {
                try
                {
                    var systemType = System.Type.GetType(systemTypeName);
                    if (systemType != null)
                    {
                        var system = dotsWorld.GetExistingSystem(systemType);
                        bool found = system != SystemHandle.Null;
                        Debug.Log($"  {systemTypeName}: {(found ? "✅ FOUND" : "❌ NOT FOUND")}");
                    }
                    else
                    {
                        Debug.Log($"  {systemTypeName}: ❌ TYPE NOT FOUND");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"  {systemTypeName}: ❌ ERROR - {e.Message}");
                }
            }
        }
        
        void OnGUI()
        {
            if (!showGUI || !discoveryComplete) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label("System Discovery Tool", GUI.skin.box);
            
            GUILayout.Label($"Total Systems: {discoveredSystems.Count}");
            GUILayout.Label($"Player Systems: {playerSystems.Count}");
            GUILayout.Label($"Active Systems: {discoveredSystems.Count(s => s.isActive)}");
            
            if (GUILayout.Button("Run Discovery"))
            {
                RunSystemDiscovery();
            }
            
            if (GUILayout.Button("Check Player Systems"))
            {
                CheckPlayerSystemsOnly();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("Player Systems Found:", GUI.skin.box);
            foreach (var system in playerSystems)
            {
                var status = system.isActive ? "✅" : "❌";
                GUILayout.Label($"  {status} {system.name}");
            }
            
            GUILayout.EndArea();
        }
    }
}
