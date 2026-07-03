# Third-Party Asset Evaluation Playbook

Date: 2026-04-13
Status: ACTIVE
Owner: Production and Art Integration

## Goal

Decide quickly whether a third-party Unity asset is a good fit for this project without destabilizing the main game repository.

## Recommended Project and Workspace Strategy

1. Keep the main game and testbed as separate Unity projects.
2. Use an isolated sandbox project for imports, upgrades, and package conflict checks.
3. Use a temporary VS Code multi-root workspace only when you are actively migrating a chosen asset from sandbox to main.
4. Do not rely on AI pre-screening alone; always run at least one sandbox validation pass.

This gives better signal quality than one permanently large workspace while still allowing controlled side-by-side integration when needed.

## Project Technical Profile (Copy/Paste Block)

Use this block when asking AI to pre-screen an asset:

- Project type: Unity procedural sandbox game
- Unity target: 6.2+ (2022 LTS only when explicitly needed)
- Runtime architecture: DOTS-first (Entities/ECS)
- MonoBehaviour usage: authoring, bootstrap, UI, and bridge code only
- Current package direction: Entities 1.3+, Entities Graphics (Hybrid Renderer v2), Burst, Jobs, Mathematics, Collections
- Terrain architecture: SDF + Surface Nets is primary for destructible terrain
- World constraints: chunk streaming, LOD transitions, deterministic generation expectations
- Logging constraints: system-level logs should use project debug settings patterns, not direct Debug.Log in DOTS systems
- Asset priorities: trees, rocks, NPCs, player body/character content
- Integration preference: data-oriented runtime behavior, minimal lock-in to large MonoBehaviour controller frameworks

## Evaluation Flow

### Phase A: AI Pre-Screen (Fast)

For each candidate asset, gather:

1. Unity version support
2. Render pipeline support (URP/Built-in/HDRP)
3. Last update date and maintenance activity
4. Dependency list (input, AI frameworks, custom shaders, animation packages)
5. Feature summary and known limitations

Then run the pre-screen prompt template in this document and score the result.

### Phase B: Sandbox Validation (Required)

Import only top candidates into the sandbox project and run this smoke pass:

1. Import success (no compiler errors)
2. Enter Play Mode with no critical console errors
3. Verify materials/shaders render correctly in your chosen pipeline
4. Verify prefab integrity (missing scripts, broken references, nested prefab warnings)
5. Confirm baseline performance in a representative scene
6. Confirm package/dependency compatibility with your intended main-project package set
7. For code assets, identify whether critical runtime logic can be bridged or rewritten for DOTS boundaries

Only assets that pass both phases should be considered for promotion to the main project.

## Hard Filters (Fail Fast)

Reject immediately if any of the following is true:

1. License terms conflict with your intended distribution model
2. Asset does not support your target Unity version and has no active maintenance
3. Pipeline/shader incompatibility requires major rework for basic rendering
4. Runtime architecture requires heavy MonoBehaviour control in gameplay-critical loops with no practical bridge plan
5. Total integration effort clearly exceeds replacement cost with an alternative asset

## Category-Specific Fit Criteria

### Trees and Rocks (Environment Props)

Prefer assets with:

1. LOD-ready prefabs and clean mesh topology
2. GPU instancing support and material consistency
3. Reasonable texture memory footprint and atlas-friendly materials
4. Wind/sway optionality that can be disabled or replaced
5. Minimal hard dependency on proprietary terrain systems

Primary risks:

1. Shader incompatibility in your render pipeline
2. Hidden runtime managers tied to non-DOTS terrain stack
3. High draw-call pressure due to poor material/LOD setup

### NPC Assets (Characters + AI Frameworks)

Prefer assets with:

1. Separation between visuals/animation and decision logic
2. Optional adapters rather than mandatory full-stack controller ownership
3. Clear extension points and source access for behavior changes
4. Navigation approach compatible with your world streaming model

Primary risks:

1. Deep framework lock-in that conflicts with ECS architecture
2. Heavy Update-driven behavior systems that are expensive to bridge
3. Package conflicts with input, animation, or AI dependencies

### Player Body / Character Packs

Prefer assets with:

1. Humanoid rig quality and retargeting reliability
2. Clean animation clips and predictable root motion setup
3. Material/shader setup compatible with your pipeline
4. Modular character parts if customization is expected

Primary risks:

1. Retargeting quality issues that are expensive to fix
2. Overly complex controller scripts tightly coupled to included assets
3. High material count that harms runtime batching

## Weighted Fit Scorecard

Score each criterion from 1 to 5 and apply weights:

| Criterion | Weight | Notes |
|---|---:|---|
| Technical compatibility | 25 | Unity version, pipeline, dependencies |
| Architecture fit | 25 | DOTS boundary friendliness, lock-in risk |
| Runtime performance | 20 | Draw calls, memory, CPU cost |
| Content quality and style fit | 15 | Art style alignment, rig/animation quality |
| Maintenance and support | 10 | Update cadence, docs, support signal |
| Licensing and cost risk | 5 | License clarity, long-term cost |

Weighted score formula:

- For each row: row score = (rating / 5) * weight
- Total score = sum of row scores
- Maximum = 100

Decision guideline:

1. 80-100: strong candidate, proceed to sandbox validation if not already done
2. 65-79: conditional candidate, proceed only with mitigation plan
3. Below 65: reject and evaluate alternatives

## AI Pre-Screen Prompt Template

Copy and fill this prompt:

You are evaluating a Unity asset for this project profile:
- [paste Project Technical Profile block]

Asset candidate:
- Name: [asset name]
- Link or source page: [url]
- Category: [trees | rocks | NPC | player body]

Tasks:
1. Rate technical compatibility, architecture fit, runtime performance expectation, content quality fit, maintenance risk, and licensing/cost risk on a 1-5 scale.
2. Provide a weighted score out of 100 using the scorecard in this prompt.
3. List hard blockers (if any).
4. List top 5 integration risks specific to this project.
5. Provide a minimal sandbox validation checklist tailored to this asset.
6. Give final recommendation: Adopt, Conditional, or Reject.

Constraints:
- Be explicit about assumptions.
- If data is missing, call it out as unknown instead of guessing.
- Favor low lock-in options that preserve DOTS-first gameplay architecture.

## Candidate Tracking Table Template

| Candidate | Category | Pre-screen score | Sandbox result | Integration risk | Decision | Notes |
|---|---|---:|---|---|---|---|
| Example Asset A | Trees | 84 | Pass | Medium | Adopt | Good LOD and instancing |
| Example Asset B | NPC | 61 | Pending | High | Reject | Strong framework lock-in |

## Related Docs

- Assets/Docs/DOCUMENT_INDEX.md
- Assets/Docs/MASTER_PLAN.md
- Assets/Docs/Process/ArtAndDOTS_Pipeline.md
- Assets/Docs/Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md
