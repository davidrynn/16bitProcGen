# Project Notes — Scratchpad

**Last Updated:** 2026-04-03

Lightweight scratchpad for current session notes, quick TODOs, and observations that don't yet belong in a spec or bug report. Anything durable should migrate to the appropriate document below.

---

## Quick Links

| What | Where |
|------|-------|
| Project vision, status, roadmap | [MASTER_PLAN.md](MASTER_PLAN.md) |
| Bug/issue tracker | [KNOWN_ISSUES.md](KNOWN_ISSUES.md) |
| Full document index | [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md) |
| SDF terrain architecture | [SDF_SurfaceNets_ECS_Overview.md](SDF_SurfaceNets_ECS_Overview.md) |
| Terrain next-steps spec | [AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md](AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md) |
| Claude/AI instructions | [/CLAUDE.md](/CLAUDE.md) |

---

## Current Session (2026-04-03)

- Fixed BUG-009: reversed normals at CSG seams after terrain edits (full 3D gradient in Surface Nets)
- Observed possible collision issue on vertical planes after edits — needs investigation (likely collider rebuild lag from `MaxCollidersPerFrame = 4`, not winding-related)

## Parking Lot

_Items noticed during work that deserve follow-up but aren't urgent._

- BUG-008 (edit buffer grows without bound) will become noticeable at high edit counts; consider clear-after-bake or per-chunk accumulated density
- BUG-009 remaining minor factors: `minDensity == 0f` symmetric case, `hasSurface` fallback vertex when `crossingCount == 0`
- BUG-010 (grass not removed after edits) — straightforward fix outlined in KNOWN_ISSUES, blocked on GRASS_ECS_SPEC Phase 3

## Human Notes

- Need to figure out how to keep interest for finding treasure — too much, not enough, too random, not exciting
- Movement: WASD should change relative to camera facing, not world axes (partially addressed by `PlayerMovementSystem` yaw-relative input)
- Need to pare down code and get a deeper connection to movement system
