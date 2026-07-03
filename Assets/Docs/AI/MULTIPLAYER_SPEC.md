# Multiplayer Architecture Spec

**Status:** DESIGN
**Last Updated:** 2026-04-30
**Supersedes:** `Assets/Docs/Archives/RootLegacy_2026/multiplayer_evaluation_spec.md` (archived 2026-07-02)
**Owner:** Architecture

---

## 1. Overview

This spec defines what to build now, what to hold, and what to defer so that multiplayer — specifically arena PvP — remains achievable post-MVP without a rewrite. It is not a plan to build multiplayer. It is a plan to avoid closing the door on it.

**Target multiplayer mode (future):** 2–8 player arena PvP on a deterministic, constrained map with limited terrain editing.

**Key principle:**
> Build systems as if they *could* be multiplayer later. Do not build multiplayer yet.

This means: commands instead of direct mutation, determinism instead of randomness, validation instead of trust — applied only where the cost is low and the benefit is structural.

---

## 2. Risk Assessment

These risks are real and independent of engine or architecture. They are not solved by DOTS — they are mitigated by careful design choices.

| Risk | Severity | Root Cause |
|---|---|---|
| Fast traversal (slingshot, chain, glide) | High | Short timesteps, additive velocity, latency sensitivity |
| Chain slingshot specifically | High | Compounding velocity means prediction error multiplies per chain |
| Terrain synchronisation | High | SDF density field is large; full sync is not viable |
| Destructible terrain in PvP | Very High | Conflict resolution + rollback on arbitrary geometry |
| Cheating surface area | High | Movement and terrain edits must be server-validated |
| Terrain streaming per player | Medium | Currently centered on one player position |

The combination of fast movement + destructible terrain + building in a PvP context is **one of the hardest multiplayer problem spaces**. The revised scope — deterministic arena + constrained terrain edits — is the only realistic path.

---

## 3. Architecture Assessment: Current State vs. Required

The existing DOTS architecture is significantly better positioned than a MonoBehaviour game. Several concerns from the original evaluation spec are already resolved.

### Already Correct — No Action Required

| Property | Status | Evidence |
|---|---|---|
| Per-entity data, no gameplay singletons | ✅ | All state in `IComponentData`, not static fields |
| Input decoupled from gameplay | ✅ | `PlayerInputComponent` separates reading from consumption |
| Simulation / Presentation group split | ✅ | SimulationSystemGroup vs PresentationSystemGroup is enforced |
| `[DisableAutoCreation]` + manual bootstrap | ✅ | Netcode for Entities expects exactly this |
| WFC deterministic seeding | ✅ | Fixed seed with `DebugSettings.UseFixedWFCSeed` |
| `TerrainModificationSystem` as sole edit entry point | ✅ | No direct density mutation outside this system |
| Camera and managed objects client-only | ✅ | All `Volume`, `Camera`, `UniversalAdditionalCameraData` in PresentationSystemGroup |

### Gaps That Need Addressing

| Gap | Phase | Effort |
|---|---|---|
| No `LocalPlayerTag` — input/camera process all `PlayerTag` entities | Pre-MVP | 30 min |
| `PlayerInputComponent` has no tick/frame counter | Pre-MVP | 15 min |
| `DotsSystemBootstrap` hardcodes `DefaultGameObjectInjectionWorld` | Pre-MVP | 5 min (comment) |
| Physics not opted into deterministic mode | Pre-MVP | 5 min |
| Terrain edits applied directly, not via command struct | MVP | 2–3 days |
| No validation layer for terrain edits | MVP | 1–2 days |
| Terrain generation version not tracked | MVP | 1 day |
| No dual-layer terrain model (base + edit overlay) | Post-MVP | 3–4 weeks |
| No Netcode for Entities integration | Post-MVP | Months |
| No input prediction / reconciliation | Post-MVP | Months |

---

## 4. Phase 0: Pre-MVP (Do Now)

**Total estimated effort: ~4 hours**
These items cost almost nothing and prevent structural debt. Do them before the codebase grows.

---

### 4.1 `LocalPlayerTag` Component

**Why:** `PlayerInputSystem` and `CameraEffectResolverSystem` currently process every entity with `PlayerTag`. In any multiplayer context — even local co-op — you need to distinguish "the entity I control" from "entities others control." Without this, every input system processes every player.

**Change:** Add to `PlayerComponents.cs`:

```csharp
/// <summary>
/// Marks the entity controlled by the local player on this machine.
/// In single-player, applied to the one player entity.
/// In multiplayer, applied only to the locally-owned entity — never to remote players.
/// </summary>
public struct LocalPlayerTag : IComponentData { }
```

Apply at bootstrap in `PlayerEntityBootstrap` alongside `PlayerTag`:

```csharp
entityManager.AddComponent<PlayerTag>(entity);
entityManager.AddComponent<LocalPlayerTag>(entity);
```

Update `PlayerInputSystem` and `CameraEffectResolverSystem` to query `LocalPlayerTag` instead of or in addition to `PlayerTag`.

---

### 4.2 Tick Counter on `PlayerInputComponent`

**Why:** Input prediction and replay require timestamped inputs. Adding a `Tick` field now costs nothing; retrofitting it later requires touching every system that reads `PlayerInputComponent`.

**Change:** Add to `PlayerInputComponent` in `PlayerComponents.cs`:

```csharp
public int Tick;   // simulation tick this input was captured on
```

Increment in `PlayerInputSystem` each frame:

```csharp
input.ValueRW.Tick = (int)(SystemAPI.Time.ElapsedTime / SystemAPI.Time.DeltaTime);
```

---

### 4.3 Mark the World Assumption in Bootstrap

**Why:** `DotsSystemBootstrap` uses `World.DefaultGameObjectInjectionWorld`. Netcode for Entities creates separate Client and Server worlds — this is the single point that must change when multiplayer is integrated. Mark it now so it's findable.

**Change:** Add a comment above the world lookup in `DotsSystemBootstrap.Awake()`:

```csharp
// MULTIPLAYER NOTE: In a Netcode for Entities setup, this splits into
// separate Client and Server worlds. All system registration below must
// be conditioned on WorldSystemFilterFlags (ClientSimulation / ServerSimulation).
// This is the primary refactor point when adding multiplayer.
var world = World.DefaultGameObjectInjectionWorld;
```

---

### 4.4 Enable Deterministic Physics

**Why:** Server-authoritative physics and client-side prediction require deterministic simulation. Enabling this now has no gameplay cost in single-player and avoids retrofitting later.

**Change:** In the Unity Physics settings (Project Settings > Physics > Unity Physics), set simulation type to `Deterministic`. Alternatively, set via `PhysicsStep` component:

```csharp
// In DotsSystemBootstrap or a physics settings bootstrap:
var physicsStep = PhysicsStep.Default;
physicsStep.SimulationType = SimulationType.UnityPhysics; // already default
// Ensure no non-deterministic Random usage in physics systems
```

Audit all terrain and player systems: any `UnityEngine.Random` call in a simulation system must be replaced with `Unity.Mathematics.Random` seeded from a deterministic source.

---

### 4.5 Document the Network Boundary

Create `Assets/Docs/AI/MULTIPLAYER_NETWORK_BOUNDARY.md` listing which components are:
- **Simulation-relevant** (will be replicated): `PlayerMovementState`, `PlayerInputComponent`, `PhysicsVelocity`, `SlingshotChargeState`, `ChainSlingshotState`
- **Client-only** (never replicated): `CameraEffectState`, `CameraEffectConfig`, `ScreenEffectState`, `GlideState` (visual only)
- **Server-authoritative** (server validates, broadcasts): terrain edits, position corrections

This is a living reference, not a binding contract. It prevents future confusion about what a server needs to know.

---

## 5. Phase 1: MVP (Demo-Ready, Multiplayer-Ready)

**Timing:** During or immediately after MVP development.
**Goal:** Single-player demo is the priority. These items are worth doing during MVP because they also improve single-player (undo, replay, serialisation) and cost significantly more to retrofit later.

---

### 5.1 Command-Based Terrain Edits

**Why:** Currently terrain edits call into `TerrainModificationSystem` directly with mutation parameters. This pattern cannot be validated, replicated, or rolled back. Wrapping in a command struct gives you undo for free, a replay log, and the exact shape the server validation layer needs.

**Target pattern:**

```csharp
/// <summary>
/// Discrete terrain edit request. All terrain modifications must go through
/// TerrainEditService.TryApply() — never directly mutate density fields.
/// </summary>
public struct TerrainEditCommand
{
    public int3 Cell;
    public TerrainEditType EditType;   // PlaceBlock, RemoveBlock, RaiseSurface, LowerSurface
    public int Tick;                   // simulation tick of request
    public Entity RequestingPlayer;    // for future server validation
}
```

`TerrainModificationSystem` becomes the sole consumer of these commands — no system bypasses it. This is the architectural change the original spec called "Centralized Terrain Edit Service."

---

### 5.2 Terrain Edit Validation Layer

**Why:** Server authority requires game rules to exist independent of the UI layer. Building the validation logic now (in single-player) means it can be enforced server-side later with zero redesign.

```csharp
public static class TerrainEditValidator
{
    public static bool CanApply(TerrainEditCommand cmd, PlayerMovementState player, TerrainEditSettings settings)
    {
        // Range limit: player must be within editing reach
        // Rate limit: max edits per second
        // Protected zones: cannot edit within X units of spawn
        // Type constraints: which edit types are allowed in which contexts
    }
}
```

In single-player, call this before applying. In multiplayer, the server calls this — clients get no special path.

---

### 5.3 Terrain Generation Version Tracking

**Why:** For deterministic arena reproduction, clients and server must agree on the exact terrain. If generation parameters change between builds, seeds produce different terrain. Versioning makes this mismatch detectable.

Add to `ProjectFeatureConfigSingleton`:

```csharp
public int TerrainGenerationVersion;   // bump when noise params or meshing change
public uint WorldSeed;                 // top-level seed for all generation
```

Log a warning if `TerrainGenerationVersion` doesn't match the expected value at runtime.

---

### 5.4 Chain Slingshot: Prediction Complexity Note

The chain slingshot mechanic (additive velocity on chained launches) is the highest-complexity movement feature for future prediction. Each chain inherits prediction error from the previous one, compounding it. This doesn't require action now, but document it:

- Chain count and window remaining must be part of the replicated state
- Reconciliation must replay the full chain sequence, not just the final velocity
- The chain window timer must be deterministic (tick-based, not `ElapsedTime`-based)

When implementing `ChainSlingshotState`, use `int TicksRemaining` instead of `float WindowRemaining` to keep it deterministic.

---

## 6. Phase 2: Post-MVP (Actual Multiplayer)

**Timing:** After MVP demo, if multiplayer is greenlit.
**Scope:** Arena PvP, 2–8 players, deterministic map, constrained terrain editing.

These items cannot be meaningfully pre-built without the Netcode for Entities package. They are listed here for planning, not implementation.

---

### 6.1 Netcode for Entities Integration

Add `com.unity.netcode` package. This restructures world initialization — the Phase 0 bootstrap comment marks the exact refactor point.

Client world: all Presentation systems, LocalPlayerTag entities, input reading.
Server world: all Simulation systems, physics, validation, terrain modification.
Shared: component definitions, game rules.

`DotsSystemBootstrap` splits into `ClientBootstrap` and `ServerBootstrap`, each registering only the systems appropriate for their world using `[WorldSystemFilter]`.

---

### 6.2 Ghost Component Authoring

Add `[GhostComponent]` attributes to simulation-relevant components identified in Phase 0:

```csharp
[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct PlayerMovementState : IComponentData { ... }

[GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
public struct ChainSlingshotState : IComponentData { ... }
```

Client-only components (`CameraEffectState`, `ScreenEffectState`) get no ghost attribute — they never cross the wire.

---

### 6.3 Input Prediction and Reconciliation

`PlayerInputSystem` sends `PlayerInputComponent` (with `Tick`) to the server each frame. The server processes inputs authoritatively. The client predicts locally using the same simulation systems and reconciles when the server returns authoritative state.

Movement systems must be fully deterministic and Burst-compiled (already true) for prediction to work. The chain slingshot requires special handling — see Section 5.4.

---

### 6.4 Dual-Layer Terrain Model

**Why:** Full SDF sync is not viable (bandwidth). Only player-driven edits need replication. Separate terrain into:

```
Base Terrain   — deterministic, seeded, generated identically on all clients from WorldSeed
+ Edit Layer   — sparse command log of player edits, replicated as events
= Final Terrain — composited at SDF sampling time
```

The `TerrainEditCommand` log from Phase 1 becomes the edit layer. The server holds the authoritative edit log and broadcasts accepted commands. Clients apply them locally.

This is a 3–4 week terrain pipeline change. The Phase 1 command architecture is the prerequisite.

---

### 6.5 Server-Authoritative Position Correction

For fast-moving players, the server corrects client position when divergence exceeds a threshold. The client snaps or lerps to the corrected position. Smoothing is critical — harsh snaps break immersion.

The chain slingshot makes this harder (see Section 5.4). Consider a slightly larger correction threshold for chained launches to avoid over-snapping during fast sequences.

---

### 6.6 Arena PvP Prototype

**Scope constraints (start here, expand later):**
- Map size: 200×200m max (limits streaming complexity)
- Player count: 2–4 (limits ghost bandwidth)
- Terrain edits: place/remove block only (no freeform SDF deformation)
- No persistent world (arena resets on match end)

These constraints make the first multiplayer build achievable. They can be relaxed as confidence grows.

---

## 7. System-by-System Multiplayer Readiness

| System | Current State | Pre-MVP Action | Post-MVP Work |
|---|---|---|---|
| `PlayerInputSystem` | Reads mouse/keyboard, writes component | Add `Tick`, filter to `LocalPlayerTag` | Serialise + send to server |
| `PlayerMovementSystem` | Burst, deterministic | None | Enable as predicted system |
| `SlingshotChargeSystem` | Burst, deterministic | None | Part of prediction loop |
| `SlingshotLaunchSystem` | Burst, deterministic | None | Server validates impulse |
| `ChainWindowSystem` (future) | Not built yet | Use `int TicksRemaining` | Replicate `ChainSlingshotState` |
| `MovementStateBookkeepingSystem` | Burst, deterministic | None | Runs on both client and server |
| `CameraSpeedFeedbackSystem` | Presentation-adjacent | None | Client-only, never replicated |
| `CameraEffectResolverSystem` | PresentationGroup, managed | None | Client-only world only |
| `ScreenEffectResolverSystem` | PresentationGroup, managed | None | Client-only world only |
| `TerrainModificationSystem` | Direct mutation | Phase 1: command struct | Server-authoritative consumer |
| `TerrainChunkStreamingSystem` | Single player origin | None until Phase 2 | Per-player streaming region |
| `GlideSystem` | Burst, deterministic | None | Part of prediction loop |
| `LandingDetectionSystem` | Burst, deterministic | None | Server fires authoritative event |

---

## 8. What Not to Build Until Post-MVP

Avoid these until Netcode for Entities is integrated:

- Networking layer of any kind
- Rollback / GGPO systems
- Matchmaking or lobbies
- Persistent world state across sessions
- Full SDF terrain sync
- Large-scale terrain deformation in multiplayer context
- `[GhostComponent]` attributes (premature without the package)
- Client/Server world split in bootstrap

Adding these before Phase 2 creates complexity with no benefit and couples the codebase to assumptions that may change.

---

## 9. Revised Phase Roadmap

| Phase | Scope | Key Deliverables | Effort |
|---|---|---|---|
| **Phase 0 (Pre-MVP)** | Structural hygiene | `LocalPlayerTag`, tick on input, bootstrap comment, deterministic physics | ~4 hours |
| **Phase 1 (MVP)** | Single-player demo, MP-ready | `TerrainEditCommand`, validation layer, generation versioning | 1–2 weeks alongside MVP |
| **Phase 2 (Post-MVP)** | Multiplayer prototype | Netcode integration, ghost authoring, input prediction, dual-layer terrain, arena prototype | 4–9 months |

---

## 10. Bottom Line

The DOTS architecture puts this project in a significantly better position than the original evaluation spec assumed. The "Architecture Mismatch" concerns (chunk streaming, runtime mesh rebuilds, lack of command-based updates) are largely resolved by the existing system design.

What remains is three things:

1. **Four hours of hygiene now** (Phase 0) that prevents structural debt.
2. **One to two weeks of command architecture during MVP** (Phase 1) that also improves single-player.
3. **Months of real multiplayer work post-MVP** (Phase 2) that cannot be meaningfully shortcut.

The honest assessment from the original spec stands:

> **Expensive but achievable later — not requires rewrite.**

With Phase 0 and Phase 1 done, that remains true regardless of how much the movement system grows between now and multiplayer greenlight.
