# Player Pit Mudiness Hypothesis Test Plan

Date: 2026-04-08
Status: ACTIVE
Owner: Player/Terrain DOTS

## Goal

Validate root cause of pit-wall movement mudiness before applying additional gameplay changes.

## Scope

- In scope: runtime diagnostics and targeted test observations.
- Out of scope: changing movement/grounding/edit behavior in this plan.

## Hypotheses

1. H1: Ground support loss after pit dig-through causes frequent ungrounded transitions, which routes movement into low air-control.
2. H2: Wall probe clamp is over-reducing horizontal velocity while hugging pit walls.
3. H3: Collider rebuild latency around edited pits creates temporary support gaps that amplify H1/H2.

## Instrumentation Inputs

1. Enable in config:
- EnablePlayerFallThroughDiagnosticSystem = true
- EnableTerrainColliderTimingSystem = true
2. Enable runtime debug flags:
- DebugSettings.EnableFallThroughDebug = true
- DebugSettings.EnableTerrainColliderPipelineDebug = true
3. Wall clamp telemetry (already added):
- [DOTS-FallThrough] WallClamp logs from PlayerMovementSystem

## Test Cases

### T1: Pit Depth Support Check

1. Create a pit with repeated subtract edits at one location.
2. Enter pit center and stand still.
3. Observe logs for:
- Ungrounded transitions with low fallTime increments
- Below-surface snapshots from PlayerFallThroughDiagnosticSystem
4. Pass criteria:
- If ungrounded persists while inside pit floor zone, H1 is supported.

### T2: Wall-Hug Strafe in Pit

1. Stand inside pit near vertical wall.
2. Hold lateral movement parallel to wall for 5-10 seconds.
3. Observe logs for:
- WallClamp speedBefore/speedAfter deltas
- grounded/useGroundControl/fallTime values during clamp events
4. Pass criteria:
- If speedAfter is repeatedly much lower than targetGround while grounded/useGroundControl is true, H2 is supported.

### T3: Edit-Then-Move Latency Correlation

1. Perform terrain edits to form pit wall.
2. Immediately move along pit wall.
3. Observe logs for:
- Terrain collider backlog/timing events
- simultaneous ungrounded or strong WallClamp reductions
4. Pass criteria:
- If muddy intervals align with collider backlog windows, H3 is supported.

## Decision Matrix

1. H1 true, H2 false, H3 false:
- Prioritize pit floor support/depth constraints before movement tuning.
2. H2 true, H1 false:
- Tune wall-clamp gating or projection policy.
3. H1 and H2 true:
- Fix support stability first, then clamp tuning.
4. H3 true:
- Raise collider throughput/timing guarantees before movement retune.

## Notes

- This plan intentionally validates behavior first.
- No additional gameplay logic should be merged until one hypothesis is confirmed with logs.