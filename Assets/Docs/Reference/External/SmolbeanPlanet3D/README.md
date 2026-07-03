# Pattern Transfer Pack: SmolbeanPlanet3D → 16bitProcGen

**This folder contains a curated knowledge transfer resource for reusing SmolbeanPlanet3D concepts in 16bitProcGen (DOTS/ECS crafting game).**

This is NOT a full codebase dump. Instead, it's a high-signal collection of schemas, patterns, and decisions that can be adapted for your target project without cloning the MonoBehaviour architecture.

## Use This Guide When

- Building procedural terrain generation in an ECS context
- Implementing Wave Function Collapse tile solver with constraints
- Migrating from MonoBehaviour singletons to DOTS
- Establishing deterministic seeded world generation
- Setting up augmented generation (pre-computed mesh compatibility tables)

## What's Inside

| File | Purpose |
|------|---------|
| [wfc-schema.md](wfc-schema.md) | Concrete data schemas for WFC (modules, sockets, constraints, config) |
| [procgen-pipeline.md](procgen-pipeline.md) | 5-stage procedural generation pipeline breakdown with entry points |
| [migration-notes.md](migration-notes.md) | 3-phase DOTS migration plan + identified bugs + risks |
| [salvage-priority.md](salvage-priority.md) | Top 10 tangible assets/concepts ranked by reuse ROI |

## Reference Audit

Full technical audit: [Tech-Analysis-For-16bitProcGen.md](../Tech-Analysis-For-16bitProcGen.md)

**Decision**: PARTIAL GO (84/100 confidence, medium-high effort)
- Procgen readiness: **8/10** ✅
- WFC potential: **8/10** ✅
- DOTS compatibility: **3/10** ⚠️ (requires rework)
- Visual fit: **8/10** ✅
- Reuse ROI: **7/10** ✅

## Quick Start

1. **If you have 30 minutes**: Read [procgen-pipeline.md](procgen-pipeline.md) to understand the 5 stages
2. **If you have 1 hour**: Also read [wfc-schema.md](wfc-schema.md) for implementable data structures
3. **If you have 2 hours**: Read all + [migration-notes.md](migration-notes.md) to understand phase breakdown
4. **If you need concrete priorities**: See [salvage-priority.md](salvage-priority.md)

## Key Takeaways

✅ **Viable for Reuse:**
- Wave Function Collapse tile constraint solver (unweighted; weights can be added)
- 5-stage procedural pipeline (noise → WFC → geometry → secondary placement → polish)
- Mesh boundary compatibility authoring tooling (proprietary but conceptually transferable)
- Texture-based wear/feedback system (efficient for runtime updates)
- Instanced grass rendering and wear-driven vegetation shading patterns

⚠️ **Requires Rework:**
- MonoBehaviour singleton architecture (rewrite for DOTS/Systems)
- Global Random usage (introduce per-stage deterministic RNG)
- Per-tile GameObject instantiation (switch to chunked meshes + Entities Graphics)
- Serialization layer (reimplement for ECS ComponentData)

❌ **Not Recommended:**
- Full controller class reuse (too tightly coupled)
- Save/load system as-is (incompatible with ECS)
- Physics setup (highly project-specific)

## Cross-Project Strategy

**Operating Principles:**
1. Keep repos separate (reduces context bloat)
2. Use this pack as concept reference, not copy-paste blueprint
3. Link back to source files only for deep dives
4. Guard your AI context budget (don't include full SmolbeanPlanet3D in future prompts)
5. Use dual-workspace mode only for short migration bursts

**Recommended Workflow:**
- Reference this pack in your AI system prompts (concise)
- For questions needing detail, link to specific files in SmolbeanPlanet3D GitHub
- Avoid including full SmolbeanPlanet3D repo in context unless debugging a specific algorithm

---

**Date Generated**: April 9, 2026  
**Source Project**: SmolbeanPlanet3D (Unity 6.0.1.12f1, URP, MonoBehaviour/OOP)  
**Target Project**: 16bitProcGen (DOTS/ECS, low-poly crafting)
