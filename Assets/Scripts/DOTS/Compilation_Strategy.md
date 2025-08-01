# DOTS Compilation Strategy

## 🎯 **Compilation Control Strategy**

### **Test Systems (Editor Only)**
These systems are wrapped with `#if UNITY_EDITOR` and will **NOT** compile into final builds:

- `WFCSystemTest.cs` - WFC algorithm testing
- `SimpleRenderingTest.cs` - Simple rendering pipeline testing
- `SimpleTestManager.cs` - Test orchestration
- `WFCTestSetup.cs` - Test setup utilities
- `DebugTestController.cs` - Debug control testing

### **Production Systems (Always Compile)**
These systems compile into all builds but are **controlled at runtime**:

- `DungeonRenderingSystem.cs` - Renders dungeon entities
- `DungeonVisualizationSystem.cs` - Creates GameObjects from entities
- `HybridWFCSystem.cs` - Core WFC algorithm
- `DungeonManager.cs` - MonoBehaviour for dungeon control

## 🔧 **Runtime Control**

### **Debug Settings Control**
All systems use `DebugSettings` for runtime control:

```csharp
// Test systems only run when enabled
if (!DebugSettings.EnableTestSystems) return;

// Debug logging only when enabled
DebugSettings.LogTest("Message"); // Only logs if EnableTestDebug = true
```

### **Default State**
- `EnableTestSystems = false` - Test systems don't run
- `EnableDebugLogging = false` - No debug spam
- `EnableWFCDebug = false` - WFC debug off
- `EnableRenderingDebug = false` - Rendering debug off

## 📋 **What Happens When You Press Play**

### **With No GameObjects:**
1. **Test systems are OFF** - No WFCSystemTest output
2. **Production systems are ON** but controlled
3. **Dungeon systems only run when requested** via DungeonGenerationRequest
4. **Clean console** - No debug spam

### **With DebugTestController:**
1. **Press T** - Enables test systems, WFCSystemTest runs
2. **Press D** - Enables all debug logging
3. **Press X** - Disables everything

## 🏗️ **Architecture Summary**

```
┌─────────────────────────────────────────────────────────────┐
│                    COMPILATION LAYER                        │
├─────────────────────────────────────────────────────────────┤
│  #if UNITY_EDITOR                                           │
│  ├── WFCSystemTest.cs          (Editor Only)               │
│  ├── SimpleRenderingTest.cs    (Editor Only)               │
│  └── DebugTestController.cs    (Editor Only)               │
├─────────────────────────────────────────────────────────────┤
│  Production Systems (Always Compile)                        │
│  ├── DungeonRenderingSystem.cs (Runtime Controlled)        │
│  ├── DungeonVisualizationSystem.cs (Runtime Controlled)    │
│  ├── HybridWFCSystem.cs        (Runtime Controlled)        │
│  └── DungeonManager.cs         (Runtime Controlled)        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    RUNTIME CONTROL LAYER                    │
├─────────────────────────────────────────────────────────────┤
│  DebugSettings.EnableTestSystems = false (Default)         │
│  DebugSettings.EnableDebugLogging = false (Default)        │
│  DebugSettings.EnableWFCDebug = false (Default)            │
│  DebugSettings.EnableRenderingDebug = false (Default)      │
└─────────────────────────────────────────────────────────────┘
```

## ✅ **Expected Behavior**

### **Default Scene (No GameObjects):**
- ✅ **Clean console** - No debug output
- ✅ **No WFC generation** - Test systems disabled
- ✅ **Systems ready** - Production systems available but inactive

### **With DungeonManager:**
- ✅ **Press G** - Request dungeon generation
- ✅ **Systems activate** - Only when requested
- ✅ **Controlled output** - Only when debug enabled

### **With DebugTestController:**
- ✅ **Press T** - Test systems enable, WFCSystemTest runs
- ✅ **Press D** - All debug logging enabled
- ✅ **Press X** - Everything disabled

## 🚫 **What Should NOT Happen**

- ❌ **Automatic WFC generation** without request
- ❌ **Debug spam** in console by default
- ❌ **Test systems running** without explicit enable
- ❌ **Production systems** running when not needed

## 🔍 **Troubleshooting**

If you're still seeing output:

1. **Check DebugSettings** - Ensure all flags are false
2. **Check for GameObjects** - Remove any test controllers
3. **Check compilation** - Ensure `#if UNITY_EDITOR` is present
4. **Check runtime control** - Ensure systems check `EnableTestSystems` 