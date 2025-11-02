# SubScene Troubleshooting - Analysis of Current Issues

**Date:** 2025-10-13  
**Issue:** SubScene created but DeadEnd rotation still not working  
**Status:** 🔍 Root cause identified

---

## 🚨 **Root Cause: Entity Component Missing**

### The Problem

**Console Evidence (Line 3442):**
```
[DOTS-Rendering] DungeonVisualizationSystem (Macro-only): No element entities. Visualizing WFCCells directly.
```

**AND repeated errors (Lines 2520+):**
```
[DOTS] DungeonRenderingSystem: Spawned entity missing DungeonElementComponent at (0, 0)
[DOTS] DungeonRenderingSystem: Spawned entity missing DungeonElementComponent at (1, 0)
... (25 times total)
```

### What This Means

1. **SubScene IS Working** - `DungeonRenderingSystem` is now running (not waiting for registry)
2. **Registry IS Found** - No "Waiting for registry" messages
3. **BUT:** The spawned entities are missing the `DungeonElementComponent`
4. **Result:** `DungeonVisualizationSystem` falls back to direct WFCCell visualization
5. **Visual Problem:** All cells spawn at (0,0,0) - same position = overlapping geometry

---

## 🔍 **Technical Analysis**

### The Missing Component Chain

**Expected Flow:**
```
DungeonRenderingSystem → Spawns Entity Prefabs → Adds DungeonElementComponent → DungeonVisualizationSystem finds them
```

**Actual Flow:**
```
DungeonRenderingSystem → Spawns Entity Prefabs → Missing DungeonElementComponent → DungeonVisualizationSystem skips them → Falls back to WFCCells
```

### Why DungeonElementComponent Is Missing

Looking at `Scripts/Authoring/DungeonElementBaker.cs`:

```csharp
public override void Bake(DungeonElementAuthoring authoring)
{
    var entity = GetEntity(TransformUsageFlags.Renderable);
    
    AddComponent(entity, new DungeonElementComponent
    {
        elementType = authoring.elementType
    });
    
    // This prefab is intended for runtime instantiation as an entity prefab
    AddComponent<Prefab>(entity);
}
```

**The Issue:** The prefab references in `DungeonPrefabRegistry` are pointing to GameObjects that:
1. **Don't have `DungeonElementAuthoring` components**, OR
2. **Are not in the SubScene**, OR  
3. **Have `DungeonElementAuthoring` but it's not configured properly**

---

## 🎯 **Diagnosis Steps**

### Step 1: Check Prefab References

**In Unity Editor:**
1. Open your SubScene (expand the arrow)
2. Select the `DungeonPrefabRegistry` GameObject inside
3. In Inspector, check all 5 prefab slots:
   - `corridorPrefab`
   - `cornerPrefab` 
   - `roomEdgePrefab`
   - `roomFloorPrefab`
   - `doorPrefab`

**Expected:** All should have prefab references assigned  
**If Any Are "None":** That's the problem!

### Step 2: Check Prefab Components

**For each assigned prefab:**
1. Click the prefab reference (opens Prefab Mode)
2. Check if the root GameObject has:
   - `DungeonElementAuthoring` component
   - `elementType` field set correctly

**Expected Components:**
```
Corridor Prefab → DungeonElementAuthoring → elementType = Corridor
Corner Prefab → DungeonElementAuthoring → elementType = Corner  
Door Prefab → DungeonElementAuthoring → elementType = Door
```

### Step 3: Check SubScene Baking

**In Unity Editor:**
1. Window → Entities → Hierarchy
2. Look for `DungeonPrefabRegistry` entity
3. Expand it and check the 5 prefab entity references

**Expected:** All 5 should show Entity references (not "None")

---

## 🔧 **Most Likely Solutions**

### Solution A: Missing DungeonElementAuthoring Components

**Problem:** Prefabs don't have `DungeonElementAuthoring` components

**Fix:**
1. For each prefab (Corridor, Corner, Door, etc.):
   - Open Prefab Mode
   - Add `DungeonElementAuthoring` component to root GameObject
   - Set `elementType` to match (Corridor, Corner, Door, etc.)
   - Save Prefab

### Solution B: Prefab References Lost

**Problem:** `DungeonPrefabRegistry` has "None" in prefab slots

**Fix:**
1. Re-assign all 5 prefab references in `DungeonPrefabRegistry`
2. Make sure they point to the correct prefab assets
3. Close SubScene to re-bake

### Solution C: Wrong Prefab Types

**Problem:** Prefabs exist but don't match expected types

**Fix:** Check that you're using the correct prefab assets:
- Should be from `Assets/Prefabs/WFCPlaceholders/` 
- Should have proper geometry (not just cubes)

---

## 🎯 **Quick Diagnostic Commands**

**In Unity Console, run these to check:**

```csharp
// Check if registry exists
Debug.Log("Registry exists: " + SystemAPI.HasSingleton<DungeonPrefabRegistry>());

// Check registry contents  
var registry = SystemAPI.GetSingleton<DungeonPrefabRegistry>();
Debug.Log("Corridor prefab: " + registry.corridorPrefab);
Debug.Log("Door prefab: " + registry.doorPrefab);
```

---

## 📊 **Expected Results After Fix**

### Console Messages (Success):
```
[DOTS-Rendering] DungeonRenderingSystem: Found DungeonPrefabRegistry
[DOTS-Rendering] DungeonRenderingSystem: Spawning Door at (2,3) with rotation 90°
[DOTS-Rendering] DungeonRenderingSystem: Spawning Corridor at (1,2) with rotation 0°
... (25 spawn messages, no "missing component" errors)
```

### Visual Results (Success):
- 25 separate objects in Scene View
- Each positioned at correct grid coordinates
- DeadEnd tiles rotated based on socket patterns
- Proper prefab geometry (not just cubes)

---

## 🚨 **Current Status**

**What's Working:**
- ✅ SubScene created and baking
- ✅ `DungeonRenderingSystem` running (not waiting for registry)
- ✅ Registry found by system

**What's Broken:**
- ❌ Prefab entities missing `DungeonElementComponent`
- ❌ Fallback system creates overlapping geometry at (0,0,0)
- ❌ No rotation applied (using fallback system)

**Next Action:** Check prefab references and components per diagnostic steps above.

---

## 📞 **Need Help?**

If you're still stuck after checking the above:

1. **Share Screenshot:** `DungeonPrefabRegistry` Inspector showing all 5 prefab slots
2. **Share Screenshot:** One of the prefab's Inspector showing components
3. **Share Console:** Any new error messages after following fixes

This will help pinpoint the exact missing piece.

---

## 🎯 **Why This Approach**

**We're debugging the proper DOTS flow** (not band-aiding the fallback) because:
1. Your DeadEnd rotation code is in the right place (`DungeonRenderingSystem`)
2. The system is running (SubScene worked)
3. Just need to fix the component chain to complete the pipeline
4. This will test the actual production code path

**Once fixed:** TC3-TC5 will work perfectly with proper rotation and positioning.
