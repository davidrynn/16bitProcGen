# World Persistence Spec
_Status: DESIGN — staged rollout (Layer 2 baseline required before structure realizers)_  
_Last updated: 2026-04-15_

---

## Overview

World persistence answers one question: **what do we save, and what do we regenerate?**

The core principle is **deterministic regeneration + sparse deltas**:

```
Loaded World State =
    Deterministic generation from (seed + chunk coords)
  + Terrain edit journal       (CSG ops, SDF modifications)
    + Structure placement record (seeded anchors/WFC + player builds)  
  + Entity state deltas        (trees, chests, interactive objects)
  + NPC history records        (only divergence from seeded defaults)
  + Player data                (inventory, progression, position)
```

If a chunk has never been visited or modified, **zero bytes are stored for it**. It is regenerated identically every time from the world seed.

---

## Storage Layout

```
/PersistentData/
  world.dat                    ← seed, world name, global tick, time of day
  /chunks/
    {regionX}_{regionZ}/
      {chunkX}_{chunkY}_{chunkZ}.edits    ← terrain edit journal (binary)
      {chunkX}_{chunkY}_{chunkZ}.structs  ← structure placements
      {chunkX}_{chunkY}_{chunkZ}.entities ← entity state deltas
  /npcs/
    {npcId}.dat                ← per-NPC history record
  /players/
    {playerId}.dat             ← inventory, stats, progression, last position
```

Only files that contain actual deltas are created. Empty/untouched chunks produce no files.

---

## Layer 1 — Terrain Shape (Edit Journal)

### What It Stores
Every SDF modification applied to a chunk — carve-outs, fills, and material changes — stored as an **append-only list of CSG operations**.

### Data Model
```csharp
// Component on each active chunk entity
public struct ChunkEditJournal : IComponentData
{
    public BlobAssetReference<EditJournalBlob> Journal;
}

public struct EditJournalBlob
{
    public BlobArray<EditOp> Ops;
}

public struct EditOp
{
    public EditOpType Type;       // RemoveSDF, AddSDF, SetMaterial
    public float3     WorldPos;
    public float      Radius;
    public byte       MaterialId;
    public uint       WorldTick;
}

public enum EditOpType : byte
{
    RemoveSDF   = 0,   // carve out terrain (player destruction)
    AddSDF      = 1,   // add terrain (player building/filling)
    SetMaterial = 2,   // change surface material only
}
```

### On Save
Serialize `EditOp[]` as a flat binary array to `chunk.edits`. No compression needed until op count exceeds ~10k per chunk — compact then.

### On Load
1. Regenerate chunk density field from world seed + chunk coords  
2. Replay all `EditOp` entries in `WorldTick` order  
3. Rebuild Surface Nets mesh from resulting density field  

### Compaction
When a journal exceeds **256 ops**, take a density field snapshot and replace the journal with a single `SetSnapshot` op. This bounds load time.

### Phase 1 Stub
Even before Phase 4, add `ChunkEditJournal` as an **empty optional component** on chunk entities so Phase 4 has a clean attachment point:
```csharp
// Applied in TerrainChunkDensitySamplingSystem to all new chunks
em.AddComponent<ChunkEditJournalDirty>(entity); // dirty flag, no journal yet
```

---

## Layer 2 — Structure Placements

### What It Stores
The outputs of deterministic structure anchor generation (including WFC realizers) and all player-placed structures.

Records must include deterministic identity and lock-state metadata so modified/discovered structures do not silently reroll.

### Data Model
```csharp
public enum StructureFamilyId : byte
{
    Dungeon = 0,
    Relic = 1,
    // Village and Ruin deferred to post-MVP
}

/// Persistence-layer view of a structure anchor. Fields are a strict subset
/// of StructureAnchorRecord (defined in STRUCTURE_PLACEMENT_SPEC); only
/// records that diverge from seeded defaults are serialized.
public struct StructurePlacementRecord
{
    public uint               StableAnchorId;    // deterministic candidate identity
    public uint               GenerationVersion; // structure generation version
    public StructureFamilyId  Family;
    public FixedString64Bytes TemplateId;        // prefab key or generator template key
    public float3             WorldPosition;     // canonical field name (matches anchor record)
    public quaternion         Rotation;
    public StructurePlacementSource Source;      // SeededAnchor, WFC, PlayerBuilt
    public StructurePersistenceFlags Flags;      // Locked/Modified/Destroyed/Discovered
    public uint               PlacedAtTick;
    public uint               ModifiedAtTick;
}

/// Canonical enum — shared with STRUCTURE_PLACEMENT_SPEC.
public enum StructurePlacementSource : byte
{
    SeededAnchor = 0,
    WFC = 1,
    PlayerBuilt = 2,
}

[Flags]
public enum StructurePersistenceFlags : byte
{
    None = 0,
    Locked = 1 << 0,
    Modified = 1 << 1,
    Destroyed = 1 << 2,
    Discovered = 1 << 3,
}
```

### On Save
- Save generated seeded and WFC structures only when they diverge from seeded defaults.
- Persist `StableAnchorId`, `GenerationVersion`, `Source`, and `Flags` for every divergent structure record.
- Player-built structures are always saved and should be marked `Locked`.
- Phase 1-2 baseline: maintain this record contract in ECS apply/record systems even if disk writes are still stubbed.

### On Load
- Regenerate anchors deterministically from seed and generation version.
- Apply structure placement records by `StableAnchorId`.
- If a record is `Locked` or `Modified`, do not reroll or relocate that structure during regeneration.
- Instantiate player-built structures directly from placement records.

### Phase Sequencing Requirement
Structure persistence identity and apply/record hooks are a prerequisite for structure family realizers.

Family realization systems should not ship before this Layer 2 baseline is active.

---

## Layer 3 — Entity State Deltas (Trees, Chests, Doors)

### What It Stores
Any interactive world object whose state can diverge from its seeded default — trees (growth stage), chests (contents), doors (open/closed), ore nodes (depleted), etc.

Only **divergence from the generated default** is saved. A healthy untouched tree = zero bytes.

### Data Model
```csharp
public struct PersistentEntityState : IComponentData
{
    public FixedString64Bytes EntityId;      // deterministic: "tree_{chunkId}_{localIdx}"
    public EntityStateType    Type;
    public byte               Stage;         // growth stage, depletion level, etc.
    public uint               ModifiedAtTick;
    public uint               NextChangeTick; // when offline simulation advances stage
}

public enum EntityStateType : byte
{
    Tree       = 0,   // stages: Full(0) Damaged(1) Stump(2) Sapling(3) Growing(4)
    OreNode    = 1,   // stages: Full(0) Partial(1) Depleted(2)
    Chest      = 2,   // contents stored separately in entity blob
    Door       = 3,   // byte = 0 closed, 1 open
    Campfire   = 4,   // byte = 0 unlit, 1 lit
}
```

### Offline Simulation
When a chunk is loaded, advance all time-based entities:

```csharp
uint ticksElapsed = currentWorldTick - state.ModifiedAtTick;
if (ticksElapsed >= state.NextChangeTick)
    state.Stage = AdvanceStage(state.Type, state.Stage);
```

This means trees grow, ore nodes partially replenish, and fires go out while the player is away — with zero cost until the chunk is loaded.

### Tree Regrowth Schedule (Design)
| Stage | Duration |
|-------|---------|
| Full → Stump (chopped) | Instant (player action) |
| Stump → Sapling | 3600 ticks (~1 in-game day) |
| Sapling → Growing | 7200 ticks |
| Growing → Full | 7200 ticks |
| Full (partial damage) → Full | 1800 ticks |

---

## Layer 4 — NPC History

### What It Stores
Only the **delta from a NPC's seeded default state**. A bandit that is alive, hostile, and has full loot stores nothing. Only deaths, dispositions changes, loot taken, and quest flags need saving.

### Data Model
```csharp
public struct NPCHistoryRecord
{
    public FixedString64Bytes NpcId;          // "bandit_{chunkId}_{localIdx}"
    public NPCStateDelta      StateDelta;
}

[Flags]
public enum NPCStateDelta : byte
{
    None            = 0,
    Dead            = 1 << 0,
    LootTaken       = 1 << 1,
    DispositionChanged = 1 << 2,
    QuestFlagsSet   = 1 << 3,
    Fled            = 1 << 4,
}

public struct NPCHistoryBlob
{
    public NPCHistoryRecord Record;
    public byte             DispositionValue;  // if DispositionChanged
    public BlobArray<byte>  QuestFlags;        // if QuestFlagsSet
}
```

### On Load
Generate NPC from seed → apply `StateDelta` flags → result is the correct NPC state.

---

## Layer 5 — Player Data

### What It Stores
Everything player-owned that cannot be regenerated from a seed.

### Data Model
```csharp
public struct PlayerSaveData
{
    public float3       LastPosition;
    public float        LastYaw;
    public uint         WorldTick;               // saved to sync offline sim
    public BlobAssetReference<InventoryBlob>    Inventory;
    public BlobAssetReference<ProgressionBlob>  Progression;
}

public struct InventoryBlob
{
    public BlobArray<ItemStack> Stacks;
}

public struct ItemStack
{
    public FixedString32Bytes ItemId;
    public int Quantity;
    public int Durability;    // -1 = no durability
}

public struct ProgressionBlob
{
    public BlobArray<FixedString32Bytes> UnlockedAbilities;
    public BlobArray<FixedString32Bytes> UnlockedRecipes;
    public int TotalResourcesCollected;
    public int TotalChunksExplored;
}
```

---

## Systems Overview

| System | Group | Purpose |
|--------|-------|---------|
| `PersistenceRecordSystem` | `LateSimulationSystemGroup` | Detects dirty chunks/entities, writes delta records |
| `PersistenceApplySystem` | `InitializationSystemGroup` | On chunk load, replays journal + applies state deltas |
| `OfflineSimulationSystem` | `InitializationSystemGroup` | Advances time-based entity stages on chunk load |
| `PlayerSaveSystem` | `LateSimulationSystemGroup` | Writes player data on pause/quit/interval |
| `WorldTickSystem` | `SimulationSystemGroup` | Increments global `WorldTick` singleton each frame |

```csharp
// WorldTick singleton — needed by all persistence systems
public struct WorldTickSingleton : IComponentData
{
    public uint Tick;
    public float SecondsPerTick; // default: 1/60
}
```

---

## Phase 1 Hooks (Add Now, Implement Phase 4)

These stubs keep Phase 4 from requiring invasive refactors:

1. **`ChunkEditJournalDirty` tag** — added to chunks when any SDF edit occurs (TerrainModificationSystem already fires; just tag the chunk entity)
2. **`WorldTickSingleton`** — create this in any bootstrap; all systems read it for offline simulation
3. **`PersistentEntityState` component** — add to trees, ore nodes, chests when they spawn; leave `Stage = 0` until Phase 4 fills in the logic
4. **Deterministic entity IDs** — when spawning any persistent entity, assign an `EntityId` string derived from `"{type}_{chunkId}_{spawnSeed}"` — required for delta lookup on load

---

## What We Explicitly Do NOT Save

| Thing | Why |
|-------|-----|
| Full chunk density fields | Too large; regenerate from seed + journal instead |
| Mesh data | Always rebuilt from density field on load |
| Physics simulation state | Non-deterministic; let physics reconstruct on load |
| Particle effects | Ephemeral, no save needed |
| Weather state | Regenerated from world seed + time of day |
| NPC paths / navigation | Recalculated on load |

---

## Open Design Questions

- [ ] **Compaction trigger:** 256 ops per chunk is a guess — profile once terrain destruction is implemented
- [ ] **Multi-save slots:** One world directory per slot, or a manifest + shared chunk data?
- [ ] **Cloud sync:** Out of scope for now, but flat binary files are easy to sync
- [ ] **Chunk unload threshold:** How far from player before a dirty chunk is flushed to disk?
- [ ] **Tree regrowth rate:** 3-day cycle is a starting point — needs playtest
- [ ] **NPC respawn:** Does a dead bandit respawn? After how long? Different rules per NPC type?
