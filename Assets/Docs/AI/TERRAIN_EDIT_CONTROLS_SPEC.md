# Terrain Edit Controls + Center-Screen Reticle Spec

**Date:** 2026-02-15
**Status:** IMPLEMENTED — awaiting runtime validation of terrain editing (reticle verified)

---

## 1. Problem Statement

The SDF terrain editing pipeline is fully wired (SDFEdit buffer -> density resampling -> Surface Nets meshing -> GPU upload -> collider rebuild), and `TerrainEditInputSystem` maps LMB=subtract, RMB=add with center-screen raycasting. However, it had two critical issues:

1. **Raycast never hits terrain:** Used `UnityEngine.Physics.Raycast` (legacy) which cannot hit DOTS physics colliders. Every raycast missed, falling back to a fixed point 8 units forward from the camera.
2. **No visual aiming indicator:** No crosshair or reticle existed, so the player had no feedback about where terrain edits would land.
3. **Legacy input API:** Used `Input.GetMouseButtonDown()` / `Input.GetKeyDown()` instead of the new Input System already used by all other player systems.

---

## 2. Changes

### 2.1 Fix TerrainEditInputSystem Raycasting

**File:** `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs`

**Bug:** `UnityEngine.Physics.Raycast` (line 68 in original) queries the legacy physics world, which contains zero DOTS colliders. Terrain chunks only have DOTS `PhysicsCollider` components registered via `PhysicsWorldSingleton`. The raycast always returned `false`.

**Fix — DOTS physics raycast:**
- Get `PhysicsWorldSingleton` in `OnUpdate` (same pattern as `PlayerGroundingSystem`)
- Pass `PhysicsWorld` into `TryGetBrushCommand` (method changed from `static` to accept `in PhysicsWorld` since `SystemAPI.GetSingleton` cannot be called from static methods)
- Replace `Physics.Raycast(ray, out hit, distance)` with `physicsWorld.CastRay(rayInput, out hit)`
- `RaycastInput` uses `Start`/`End` (not origin/direction), `CollisionFilter.Default`
- Added `state.RequireForUpdate<PhysicsWorldSingleton>()` so the system doesn't run before the physics world exists

**Fix — New Input System migration:**
- Replaced `Input.GetMouseButtonDown(0/1)` with `Mouse.current.leftButton/rightButton.wasPressedThisFrame`
- Replaced `Input.GetKeyDown(KeyCode.Q/E)` with `Keyboard.current.qKey/eKey.wasPressedThisFrame`
- Added null checks for `Mouse.current` and `Keyboard.current`
- Required adding `Unity.InputSystem` to `DOTS.Terrain.asmdef` references

**Fix — Edit cooldown:**
- Added `_lastEditTime` field and `EditCooldown = 0.15` seconds
- Prevents rapid-fire edits from held clicks or fast clicking

**Fix — Debug logging:**
- All raycast hits, misses, and edit operations log via `DebugSettings.LogTerrainEdit()`
- Controlled by `EnableTerrainEditDebug` flag (default `false`)

### 2.2 Add TerrainEdit Debug Logging

**File:** `Assets/Scripts/DOTS/Core/DebugSettings.cs`

- Added `EnableTerrainEditDebug` flag (default `false`)
- Added `LogTerrainEdit(string message, bool forceLog = false)` method
- Uses `[DOTS-TerrainEdit]` log prefix, following existing pattern

### 2.3 Create Center-Screen Reticle

**File:** `Assets/Scripts/Player/Bootstrap/ReticleBootstrap.cs` (NEW)

A MonoBehaviour bootstrap (per CLAUDE.md: "MonoBehaviours only for bootstrap and configuration") that creates a UI reticle at runtime:

- **Canvas:** `ScreenSpaceOverlay`, `sortingOrder=100` (renders above other UI)
- **Dot:** 8x8px `UI.Image` anchored to screen center
- **Texture:** Procedurally generated 16x16 white circle with anti-aliased edges
- **Color:** Semi-transparent white `(1, 1, 1, 0.6)` — visible on any terrain
- **Performance:** No `Update` loop. One-time setup in `Start()`, static overlay thereafter

**Activation:** Add `ReticleBootstrap` component to a GameObject in the scene alongside other bootstrap objects.

---

## 3. Assembly Reference Change

**File:** `Assets/Scripts/DOTS/DOTS.Terrain.asmdef`

Added `Unity.InputSystem` to the references array. Required because `TerrainEditInputSystem` now uses `UnityEngine.InputSystem.Mouse` and `Keyboard`, and the `DOTS.Terrain` assembly did not previously reference the Input System package.

---

## 4. Files Changed

| File | Action | Change |
|------|--------|--------|
| `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs` | Modified | DOTS raycast, new Input System, cooldown, debug logging |
| `Assets/Scripts/DOTS/Core/DebugSettings.cs` | Modified | Added `EnableTerrainEditDebug` flag + `LogTerrainEdit()` method |
| `Assets/Scripts/Player/Bootstrap/ReticleBootstrap.cs` | **Created** | Runtime reticle UI bootstrap |
| `Assets/Scripts/DOTS/DOTS.Terrain.asmdef` | Modified | Added `Unity.InputSystem` reference |

---

## 5. Key Patterns Referenced

| Pattern | Source File | Usage |
|---------|------------|-------|
| DOTS physics raycast | `PlayerGroundingSystem.cs:39-54` | `PhysicsWorldSingleton` + `RaycastInput` + `CastRay` |
| New Input System | `PlayerInputSystem.cs:29-30` | `Mouse.current`, `Keyboard.current`, `.wasPressedThisFrame` |
| Debug logging | `DebugSettings.cs` | Flag + method + prefix pattern |
| Bootstrap MonoBehaviour | `PlayerEntityBootstrap.cs` | Runtime-only object creation in `Start()` |

---

## 6. Verification

1. Add `ReticleBootstrap` to a scene GameObject
2. Enter Play mode
3. Confirm reticle dot appears at screen center
4. Left-click: terrain should subtract at the camera center ray hit point (or 8 units forward if no hit)
5. Right-click: terrain should add at the same point
6. Check Unity console for errors
7. Toggle `DebugSettings.EnableTerrainEditDebug = true` to verify edit logging (`[DOTS-TerrainEdit]` messages)

---

## 7. Known Limitations

- **Brush size is hardcoded** (`BrushRadius = 3f`, `BrushDistance = 8f`). Future work: expose via `TerrainGenerationSettings` or player input (scroll wheel).
- **No visual brush preview.** The reticle shows aim point but not the edit radius. Future work: projected decal or wireframe sphere at hit point.
- **Reticle is always visible.** No toggle or context-awareness (e.g., hide in menus). Acceptable for current debug/prototype stage.
- **Edit cooldown is fixed** at 0.15s. Could be made configurable.
