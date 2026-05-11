# Multiplayer Evaluation & Future-Proofing Spec

## Context

Game features under consideration:
- High-speed traversal (slingshot, glide, teleport)
- Procedural terrain (SDF + Surface Nets)
- Destructible environment
- Potential building mechanics

Goal:
- Evaluate feasibility of multiplayer PvP
- Define a **low-risk path** to support multiplayer later
- Avoid derailing MVP development

---

## High-Level Evaluation

### Initial Assumption
> "This would be an easy sell for multiplayer PvP"

### Reality
This combination is **not easy**. It is one of the more complex multiplayer problem spaces due to:

| System | Multiplayer Impact |
|------|-------------------|
| Fast movement | Requires prediction, reconciliation, anti-cheat |
| Terrain modification | Requires world-state synchronization |
| Building | Adds player-driven state changes |

These systems multiply complexity rather than add to it.

---

## Key Risks

### 1. Movement Complexity

Fast traversal introduces:
- Hit detection issues
- Latency sensitivity
- Prediction/reconciliation requirements

Without mitigation, PvP will feel:
- Unfair
- Unreadable
- Inconsistent

---

### 2. Terrain Synchronization

Full destructibility requires syncing:
- Density field or mesh changes
- Across chunk boundaries
- With LOD considerations

Hard problems:
- Conflict resolution
- Rollback
- Bandwidth cost

---

### 3. Architecture Mismatch

Current systems (likely):
- Chunk streaming
- Procedural generation
- Runtime mesh rebuilds

Missing for multiplayer:
- Deterministic generation guarantees
- Command-based state updates
- Validation layer
- Versioning of world state

---

### 4. Cheating Risk

Without server authority:
- Movement exploits
- Terrain manipulation exploits
- Teleport abuse

Multiplayer **must assume server authority**, even if not implemented yet.

---

## Recommended Multiplayer Scope (Future)

### Mode: Arena PvP

Constraints:
- Small map
- Deterministic generation
- Limited player count (2–8)

---

### Terrain Editing (Constrained)

Allowed:
- Place block
- Remove block
- Raise/lower one cell

Disallowed (initially):
- Freeform SDF deformation
- Large-scale terrain edits

Rationale:
- Keeps edits discrete
- Enables event-based sync
- Simplifies validation

---

### Authority Model (Future)

Target model:
- Server authoritative
- Clients send commands
- Server validates
- Server broadcasts accepted changes

---

## Core Architectural Decisions (Do Now)

### 1. Command-Based Gameplay

Avoid direct mutation.

Instead of:

```csharp
terrain.SetBlock(x, y, z, BlockType.Stone);
```

Use:

```csharp
struct PlaceBlockCommand {
    int3 cell;
    BlockType blockType;
    int tick;
    EntityId playerId;
}
```

Applies to:
- Movement inputs
- Terrain edits
- Ability usage

---

### 2. Deterministic Terrain Generation

All arena maps must be reproducible via:

```csharp
ArenaSeed
TerrainGenerationVersion
ArenaRules
```

Avoid:
- Hidden randomness
- Non-seeded noise calls

---

### 3. Dual-Layer Terrain Model

Separate terrain into:

```
Base Terrain (deterministic)
+ Edit Layer (small diffs)
= Final Terrain
```

Only the **edit layer** needs synchronization.

---

### 4. Centralized Terrain Edit Service

All edits go through one system:

```csharp
TerrainEditService.TryApply(editCommand)
```

Future evolution:

```csharp
Client → request
Server → validate
Server → broadcast
Clients → apply
```

---

### 5. Input-Based Movement Model

Store inputs instead of only positions:

```csharp
struct MovementInput {
    float2 move;
    float2 look;
    bool jump;
    bool slingshotPressed;
    bool slingshotReleased;
    int tick;
}
```

Benefits:
- Enables replay
- Enables prediction later

---

### 6. Validation Layer (Critical)

Gameplay rules must exist independent of UI:

```csharp
bool CanApplyEdit(PlayerState player, EditCommand cmd, ArenaState state)
```

Examples:
- Range limits
- Cooldowns
- Protected zones
- Max edit rate

---

## What NOT to Build Now

Avoid premature complexity:

- Networking layer
- Rollback systems
- Matchmaking
- Persistent worlds
- Full SDF sync
- Large-scale terrain deformation in multiplayer

---

## Practical MVP Path

### Phase 1 (Current)
- Single-player
- Focus on movement feel
- Stabilize terrain system

### Phase 2
- Introduce command-based architecture
- Add edit layer abstraction

### Phase 3
- Local simulation (no networking)
- Replay inputs

### Phase 4 (Optional)
- Minimal multiplayer prototype
- Arena only
- Limited terrain edits

---

## Final Assessment

### Original Idea
Fast traversal + teleport + destructible terrain + building + PvP

**Verdict:**
- Potentially very fun
- Extremely high complexity
- Not appropriate for current stage

---

### Revised Approach
Deterministic arena + constrained terrain edits

**Verdict:**
- Realistic
- Scalable
- Compatible with current architecture if planned early

---

## Key Principle

> Build systems as if they *could* be multiplayer later, but do not build multiplayer yet.

This means:
- Commands instead of direct mutation
- Determinism instead of randomness
- Validation instead of trust

---

## End State Vision (If Successful)

- Fast traversal PvP arena
- Tactical terrain manipulation
- Predictable, fair interactions
- Low bandwidth sync via event stream

---

## Bottom Line

Multiplayer is not an "easy add".

But with the constraints and architecture above, it becomes:

**"expensive but achievable later" instead of "requires rewrite."**

