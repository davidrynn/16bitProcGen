# SubScene Setup Guide - Enable Proper DOTS Rendering

**Purpose:** Enable `DungeonRenderingSystem` (the proper DOTS path) so DeadEnd rotation code actually executes  
**Current Issue:** `DungeonPrefabRegistry` needs to be baked, requiring a SubScene

---

## 🚨 Why This Is Needed

**Current State:**
```
Test Scene → DungeonPrefabRegistryAuthoring (GameObject)
                    ↓ (NOT BAKED - regular scene)
             DungeonRenderingSystem: "Waiting for baked registry..."
                    ↓ (EXITS EARLY)
             DungeonVisualizationSystem (FALLBACK): Spawns plain cubes
                    ↓
             NO ROTATION APPLIED ❌
```

**After SubScene Setup:**
```
Test Scene + SubScene → DungeonPrefabRegistryAuthoring (in SubScene)
                            ↓ (BAKED by Unity)
                         DungeonPrefabRegistry (ECS Entity)
                            ↓
                         DungeonRenderingSystem: "Found registry!"
                            ↓
                         Spawns entity prefabs with DetermineDeadEndRotation() ✅
```

---

## 📋 Setup Instructions

### Step 1: Create SubScene

1. In Unity, right-click in **Hierarchy** → `New SubScene` → `Empty Scene...`
2. Name it: `DungeonPrefabs_SubScene`
3. Unity will create:
   - A SubScene GameObject in your hierarchy
   - A `.unity` scene file in `Assets/SubScenes/`

### Step 2: Move DungeonPrefabRegistry

1. In **Hierarchy**, find your existing `DungeonPrefabRegistry` GameObject
   - (The one with `DungeonPrefabRegistryAuthoring` script attached)
   - (Should have 5 prefab references: Corridor, Corner, Door, etc.)

2. **Drag it into the SubScene** in the hierarchy
   - It should now be a child of the SubScene container

3. **Important:** Verify all prefab references are still assigned
   - Select the `DungeonPrefabRegistry` GameObject
   - In Inspector, check that all 5 prefab slots still have references

### Step 3: Close SubScene for Baking

1. In **Hierarchy**, click the **arrow/chevron** next to `DungeonPrefabs_SubScene`
2. **Close** the SubScene (arrow points right when closed)
3. You should see a white box icon appear - this means it's baked

**What Happens:**
- Unity's baking system converts the GameObject prefabs to Entity prefabs
- The `DungeonPrefabRegistryBaker` runs (see `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs`)
- Creates a singleton `DungeonPrefabRegistry` component with baked Entity references

### Step 4: Verify Baking

**In Console**, look for baking messages:
```
EntityBaking: Baking for 'DungeonPrefabs_SubScene'
...
Baking completed: X entities created
```

**In Entity Inspector:**
1. Window → Entities → Hierarchy
2. Look for `DungeonPrefabRegistry` singleton entity
3. Should have `DungeonPrefabRegistry` component with 5 entity references

---

## ✅ Verification - Run Test Again

### What Should Change

**Before SubScene (Current):**
```
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): Waiting for DungeonPrefabRegistry (not baked yet)
[DOTS-Rendering] DungeonVisualizationSystem (Macro-only): No element entities. Visualizing WFCCells directly.
```

**After SubScene (Expected):**
```
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): Found DungeonPrefabRegistry
[DOTS-Rendering] DungeonRenderingSystem: Spawning element at (0,0) - Pattern 6
[DOTS-Rendering] DungeonRenderingSystem: Applied rotation for DeadEnd: 90°  ← NEW!
[DOTS-Rendering] DungeonRenderingSystem: Spawning element at (0,1) - Pattern 3
...
```

### Visual Verification

**Before:**
- All cubes facing same direction
- Placeholder geometry (cubes/primitives)

**After:**
- DeadEnd prefabs rotated correctly
- Actual prefab geometry (from your FBX models)
- Corridors connect properly
- Corners rotate appropriately

---

## 🔧 Troubleshooting

### Issue: "SubScene won't close"
**Solution:** Make sure no errors in console. SubScenes won't bake if there are compile errors.

### Issue: "Prefab references lost when moved to SubScene"
**Solution:** 
1. Open the SubScene (click arrow to expand)
2. Select `DungeonPrefabRegistry` inside
3. Re-assign the 5 prefab references
4. Close SubScene again

### Issue: "Still seeing 'Waiting for baked registry'"
**Check:**
1. SubScene is **closed** (baking only happens when closed)
2. No console errors preventing baking
3. `DungeonPrefabRegistry` GameObject is **inside** the SubScene
4. All 5 prefab slots are assigned (not None)

### Issue: "Entity Inspector shows no DungeonPrefabRegistry"
**Solution:**
1. Window → Entities → Hierarchy
2. Make sure you're looking at the correct World (usually "Default World")
3. The registry entity should appear at top level (it's a singleton)

---

## 📊 Expected Test Results After Setup

Once SubScene is properly baked:

### TC3: Visual DeadEnd Rotation
- ✅ Doors/DeadEnds should face their open socket direction
- ✅ Each DeadEnd can be 0°/90°/180°/270° based on socket pattern

### TC4: DeadEnd Rotation Logs
Look for in console:
```
[DOTS-Rendering] DungeonRenderingSystem: Spawning Door at (X,Y)
[DOTS-Rendering] Applied DeadEnd rotation: Y=0° (socket pattern: FWWW)
[DOTS-Rendering] Applied DeadEnd rotation: Y=90° (socket pattern: WFWW)
```

### TC5: Regression - Corridor/Corner
- ✅ Corridors still rotate to match connections
- ✅ Corners still rotate to match perpendicular openings
- ✅ No change to existing rotation logic

---

## 🎯 Why This Is The Right Approach

1. **Production Code:** Tests the actual system that will be used in builds
2. **SPEC Compliant:** We modified `DungeonRenderingSystem` as requested
3. **DOTS Best Practice:** SubScenes are the standard way to convert GameObjects to Entities
4. **Already Implemented:** The baking infrastructure exists, just needs to be activated

---

## 📝 Alternative: Quick Test (Not Recommended)

If you **can't** set up SubScene for some reason, we could add rotation to the fallback system (`DungeonVisualizationSystem`) temporarily. But this would:
- Not test the production code path
- Require duplicating rotation logic
- Need to be removed later

**Recommendation:** Set up the SubScene properly - it's 5 minutes of work in Unity Editor.

---

## 📞 Need Help?

If you encounter issues:
1. Share screenshot of Hierarchy showing SubScene structure
2. Copy console messages during baking
3. Screenshot of Entity Inspector showing (or not showing) DungeonPrefabRegistry

This will help diagnose any baking issues.

