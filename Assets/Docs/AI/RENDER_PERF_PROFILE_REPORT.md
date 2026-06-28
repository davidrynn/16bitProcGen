# Render Performance Profile Report — Basic Terrain Scene

**Status:** CURRENT
**Last Updated:** 2026-06-10
**Owner:** Rendering / Terrain
**Scene:** `Assets/Scenes/Basic Terrain Scene.unity`
**Tooling:** Unity MCP profiler (`manage_profiler`, `manage_graphics`), Unity 6000.4.1f1, URP, quality level `PC` (`Assets/Settings/PC_RPAsset.asset`)

---

## 1. Purpose

Record a profiling snapshot of the Basic Terrain Scene and its headline conclusion: **the scene is vertex/geometry-bound, not fill-rate bound.** This exists to stop the project re-litigating "render at low resolution (240p) for performance" — that idea does not help here — and to point optimization effort at the real bottleneck (tree/rock geometry).

## 2. Method

- Entered Play mode, sampled FrameTimingManager GPU *busy* time (excludes VSync wait) across multiple frames to average out noise.
- A/B test: `renderScale` 1.0 vs 0.34 (≈240p), all else equal.
- Pulled `Render` category counters and a Frame Debugger pass dump (59 events) while paused.
- Caveat: GPU *timing* was contended by other GPU apps running in the background; structural counts and CPU-side nanosecond counters below are robust against that.

## 3. Headline Findings

### 3.1 Not fill-rate bound (resolution is irrelevant to perf)

| Setting | Resolution | GPU busy time |
|---|---|---|
| renderScale 1.0 | native | ~11.0 ms |
| renderScale 0.34 | ~1/9 the pixels | ~10.9 ms |

Cutting pixel count ~9× produced **no change** in GPU time. The 240p retro look is therefore a **purely aesthetic** choice (use Render Scale + **Point** upscaling filter on the URP asset) — it is not a performance lever.

### 3.2 Vertex/geometry-bound, dominated by trees & rocks

Per-frame geometry:

| Metric | Value |
|---|---|
| Vertices | **1,046,309** (~1.05M) |
| Triangles | 471,509 |
| SetPass calls | 33 *(low — good)* |
| Total draw calls | ~1,180 |

**Instanced trees & rocks alone = 959,527 vertices (92% of total)** across 674 instances (19 batches, ~1,420 verts each). DOTS terrain (BatchRendererGroup: 504 draw calls / 509 instances) contributes only ~85K vertices — cheap per chunk.

Vertices outnumber triangles **2.2 : 1** — heavy vertex duplication inherent to the flat-shaded low-poly art (faceted faces cannot share vertices). The aesthetic costs ~4× the vertices of smooth shading, and trees pay most of it.

### 3.3 Pipeline is lean (Frame Debugger, 59 events)

- GPU skinning for the player (12 skinned submeshes)
- **1** Main Light Shadowmap pass (52 shadow casters, ~0.5 ms CPU)
- Color-grading LUT blit (cheap)
- `DrawOpaqueObjects` — the bulk
- No transparent pass, no SSAO/SSR/heavy screen effects

### 3.4 Editor artifact in CPU timing

`UIR.DrawChain` measured ~12.6 ms — this is the **Unity Editor's own UI Toolkit repaint, not the game.** It inflated the in-editor "CPU frame 13–16 ms" reading. Actual game main-thread work ≈ **7.7 ms**; a standalone build sheds this overhead. Trust the editor's CPU frame time accordingly.

## 4. Recommendations (prioritized)

1. **Tree/rock LODs — highest value by far.** 674 instances × ~1,420 verts is >90% of frame geometry; most are mid/far from camera. A `LODGroup` (or distance mesh-swap) dropping distant instances to low-poly/billboard cuts vertex load dramatically with no visible change. The relic LOD/impostor work is a working precedent — see Related Docs.
2. Lower base poly on tree meshes if even the near LOD is heavy.
3. Reduce scatter density / draw distance — also trims the 52-caster shadow pass.
4. Terrain draw calls (504 BRG) and shadows (~0.5 ms) are fine — leave until trees are handled.

## 5. Related Docs

- [TerrainHeightMaps/SURFACE_SCATTER_LOD_SPEC.md](TerrainHeightMaps/SURFACE_SCATTER_LOD_SPEC.md) — **the fix**: distance-based mesh swap implementing recommendation #1
- [TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md](TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md) — trees/rocks scatter runtime contract (the geometry profiled here)
- [STRUCTURE PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md](STRUCTURE%20PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md) — implemented distance LOD/impostor precedent to mirror for scatter
- [DOTS_Terrain_LOD_Implementation_Checklist.md](DOTS_Terrain_LOD_Implementation_Checklist.md) — terrain LOD execution checklist

## 6. Acceptance / Follow-up

This is a point-in-time snapshot; re-profile after any scatter/LOD change. The finding is actionable once a `LODGroup`/distance-swap path exists for scatter trees & rocks — at which point re-measure vertices/frame and confirm the drop.
