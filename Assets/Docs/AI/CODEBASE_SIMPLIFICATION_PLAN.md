# Codebase Simplification & Cleanup Plan

**Status:** PLANNED (Phase 1 not started)
**Last Updated:** 2026-07-02
**Owner:** David + AI assistant

**Purpose:** Living plan for simplifying and clarifying the codebase — correcting system names, archiving dead code, repairing structure, and ordering documentation — using a token-efficient AI workflow. The goal is a codebase the owner — who has limited Unity/ECS experience — can navigate and understand unaided: fewer moving parts, self-explanatory names and structure, modern ECS idiom, and documentation that reflects reality. All cleanup planning (current and future rounds) lives in this doc: the phase workflow is defined once in §2–§5, and each round's manifests, decisions, and batch logs accumulate in §6.

---

## 1. Goals, Scope & Non-Goals

**Goals** (every Phase 2 verdict should cite which goal the change serves):

1. **Understandable by a Unity/ECS newcomer** — names that explain themselves, folder structure that teaches the architecture, XML summaries on public types, why-comments on DOTS constraints. The bar is not just "matches the convention" but "a newcomer can infer what it does."
2. **Lighter** — less dead weight, fewer duplicate code paths, fewer stale docs competing for attention.
3. **Aligned with modern Unity 6 / DOTS best practices** — current ECS idiom per the `unity-ecs-patterns` skill, not just correct naming.

**Scope:**

- Renaming systems/components/files to match the conventions in `/CLAUDE.md` (naming table, one-class-per-file, unique class names).
- Identifying and archiving dead or superseded code (moved out of active namespaces, not deleted without review).
- Folder/namespace structure repair — misplaced files, namespace≠folder mismatches, junk-drawer folders — against `PROJECT_STRUCTURE_DOTS.md` and the CLAUDE.md namespace table.
- Detecting and consolidating functionally overlapping code (two systems/utilities doing the same job) where the decision log (§6.5) explicitly approves it.
- Documentation ordering: staleness review of `Assets/Docs/`, archive moves, index repair — executed per `DOCUMENTATION_SYSTEM_SPEC.md` rules.
- Flagging outdated ECS idiom (checked against the `unity-ecs-patterns` skill) into the manifests; *fixing* idiom requires a per-item decision-log verdict since it edges beyond pure renaming.

**Non-Goals:**

- No behavior changes. Every batch must be a pure refactor — identical runtime behavior, verified by compile + EditMode tests.
- No performance work (tracked separately, e.g. scatter LOD specs).
- No architectural migrations (e.g. heightmap → SDF consolidation) — those need their own specs.
- No touching `Assets/Docs/Archives/` content beyond moving things into it.

Out-of-scope observations are **recorded, not dropped**: when any phase surfaces an improvement that would change behavior or exceed this plan's scope (a worthwhile `SystemBase`→`ISystem` conversion, a performance smell, an architectural simplification), it goes into §6.7 as a suggestion for separate follow-up work — it is never silently applied inside a cleanup batch.

---

## 2. Strategy: Separate Judgment from Execution

The expensive part of AI cleanup is discovery and decisions, not edits. Three phases, paying for intelligence only where judgment happens:

| Phase | What | Who/What runs it | Cost profile |
|-------|------|------------------|--------------|
| 1. Inventory | Produce manifests (§6.1–§6.4), change nothing | Scripts + analyzers + agent sweeps | Near-free except the overlap audit (see §3) |
| 2. Decisions | Turn manifests into an approved rename map / archive list | Human + capable model, one sitting | The only "expensive" phase — one session |
| 3. Execution | Apply approved batches mechanically | Scripts + Haiku subagents, compile check per batch | Cheap |

Rules that keep this cheap:

- **Scripts over models.** Anything deterministic (`git mv`, project-wide rename, grep sweeps, file-age reports) runs as a script. The model writes the script once; the shell runs it for free.
- **Fresh, scoped sessions.** Each execution batch runs in a fresh context that loads only this doc — no mega-session accumulating history.
- **Cheap model for mechanical work.** Haiku subagents execute pre-decided batches; the orchestrator (capable model) only reads one-line verdicts and handles failures.
- **Manifest before mutation.** Nothing is renamed, moved, or archived unless it appears in an approved table in §6 with a decision-log entry.

## 3. Phase Definitions

### Phase 1 — Inventory (read-only)

Deliverables, written into §6:

1. **System naming + idiom audit** — every `ISystem`/`SystemBase` class vs. the CLAUDE.md naming convention; flag mismatches, non-`partial`, filename≠classname, duplicate-ish names. The auditor loads the `unity-ecs-patterns` skill first so idiom flags (legacy ECB patterns, `SystemBase` where `ISystem` fits, missed enableable components) cite correct modern syntax rather than memory.
2. **Dead-code candidates** — signals, not verdicts: zero inbound references (Roslyn/IDE analysis), superseded per spec docs, `[DisableAutoCreation]` systems nothing enables, files untouched since before major pivots (`git log` age report).
3. **Folder/namespace structure audit** — actual layout vs. `PROJECT_STRUCTURE_DOTS.md` and the CLAUDE.md namespace table. Script dumps `(path, namespace, class)` triples and diffs against the expected structure; the model reviews only the exceptions: misplaced files, namespace≠folder mismatches, junk-drawer folders holding unrelated code.
4. **Functional overlap audit** — the one judgment-priced part of Phase 1, kept bounded by a two-tier sweep: cheap agents produce a per-folder responsibility map (one line per class: what it does), then a single capable-model pass reads *only the summaries* (~350 one-liners, not 350 files) and flags overlap candidates — duplicate placement paths, parallel math/utility helpers, systems with the same purpose under different names.
5. **Doc staleness list** — Active docs whose status is stale (e.g. COMPLETE/IMPLEMENTED specs still in active folders), overlapping/duplicate topics, index drift vs. actual files.

Free tooling to prefer over model reads: Roslyn/Rider unused-symbol inspection, `git log --format= --name-only | sort | uniq -c` for churn, `git log -1 --format=%ci -- <path>` for age, grep for `Resources.Load`/kernel strings.

### Phase 2 — Decisions (human + model, one sitting)

- Review Phase 1 manifests together; every row gets a verdict: **rename to X / archive / keep / needs-investigation** (overlap rows: **merge into X / extract shared utility / keep both — intentional, document why**).
- Output: approved tables in §6.1–§6.4 + rationale entries in §6.5.
- No verdict → no action. "Needs-investigation" rows roll into a future round rather than blocking the batch.

### Phase 3 — Execution (batched, verified)

Batch protocol — every batch, no exceptions:

1. Batch = 5–10 related items from an approved table (one namespace or one doc folder at a time). **Exception: consolidations (§6.4 rows) are one per batch** — merging code is a real change, not a mechanical rename, and must be trivially bisectable.
2. Any worker touching system *code* (not just file moves) loads the `unity-ecs-patterns` skill before editing.
3. Renames/moves via `git mv` **with the `.meta` file** (or via Unity editor/MCP) — never orphan a `.meta`.
4. After code edits: grep for the **old name as a string literal** (`Resources.Load` paths, compute kernel names, animator params don't produce compile errors).
5. Unity compile + EditMode tests pass (`unity-test-runner` skill / CLI).
6. One commit per batch, message referencing this doc and the table rows applied.
7. Log the batch in §6.6. A failed batch is reverted, marked in the log, and its rows go back to needs-investigation.

## 4. Unity/DOTS Hazards Checklist

- `.meta` must move with its asset — orphaned `.meta` = broken GUID = lost scene/prefab references.
- MonoBehaviour class rename ⇒ filename must match; serialized refs survive only if the `.meta` (GUID) is preserved.
- Pure `ISystem` structs are the safest renames (nothing serializes them); MonoBehaviours and ScriptableObjects are the riskiest.
- String-based lookups are invisible to the compiler: `Resources.Load` paths, compute shader kernel/constant names, animator parameters. Grep after every batch (§3 step 3).
- Namespace changes can break `asmdef` references and source-generator output — keep namespace moves in their own batches.
- BlobAsset/dispose sites and `[UpdateBefore/After]` ordering comments must move with relocated code, not be dropped.

## 5. Execution Model (Orchestration)

- **Orchestrator:** capable model (current session tier) — dispatches batches, reads verdicts, resolves failures. Kept small: this doc makes each batch self-describing.
- **Workers:** Haiku subagents per batch — "apply rows N–M from §6.x, run the grep sweep, run tests, report pass/fail + anomalies in ≤5 lines."
- **Scripts:** inventory scans, renames, index updates — shell-priced, not model-priced.
- Escalation: a worker never makes judgment calls; anything ambiguous returns to the orchestrator, and anything scope-changing returns to the human via §6.5.

---

## 6. Living Sections (filled per round)

### 6.1 Rename Map

> Populated by Phase 1, approved in Phase 2. No renames outside this table.

| # | Current | Proposed | Kind | Verdict | Batch |
|---|---------|----------|------|---------|-------|
| — | *(pending Phase 1)* | | | | |

### 6.2 Archive List (code)

| # | Path | Signal (why suspected dead) | Verdict | Batch |
|---|------|------------------------------|---------|-------|
| — | *(pending Phase 1)* | | | |

### 6.3 Doc Reorder List

| # | Doc | Issue (stale status / duplicate / misplaced / unindexed) | Verdict | Batch |
|---|-----|-----------------------------------------------------------|---------|-------|
| — | *(pending Phase 1)* | | | |

### 6.4 Overlap / Consolidation Candidates

> From the functional overlap audit (§3 Phase 1 item 4) and structure audit exceptions. Verdicts: **merge into X / extract shared utility / keep both (intentional — record why)**. Approved rows execute one-per-batch (§3).

| # | Code A | Code B | Overlapping purpose | Verdict | Batch |
|---|--------|--------|---------------------|---------|-------|
| — | *(pending Phase 1)* | | | | |

### 6.5 Decision Log

> One entry per Phase 2 sitting or mid-execution judgment call. Newest first.

- **2026-07-02** — Goals made explicit per owner review: learnability for a Unity/ECS newcomer, lighter codebase, modern best practices (§1); verdicts must cite the goal served. Added §6.7 so behavior-changing improvement ideas are recorded for follow-up instead of dropped or silently applied.
- **2026-07-02** — Scope expanded per review: folder/namespace structure audit and functional overlap audit added to Phase 1; `unity-ecs-patterns` skill made mandatory for the idiom audit and any code-touching worker; consolidation candidates get their own table (§6.4) and run one-per-batch.
- **2026-07-02** — Plan created; workflow and batch protocol agreed. No inventory run yet.

### 6.6 Batch Log

| Batch | Date | Rows applied | Tests | Commit | Notes |
|-------|------|--------------|-------|--------|-------|
| — | | | | | |

### 6.7 Improvement Suggestions (out of scope — follow-up work)

> Behavior-changing or scope-exceeding improvements noticed during any phase (see §1 Non-Goals). Never applied in a cleanup batch; each row is a candidate for its own spec/ticket.

| # | Where | Suggestion | Goal served (§1) | Disposition (open / spun off to spec-ticket / rejected) |
|---|-------|------------|------------------|----------------------------------------------------------|
| — | *(none yet)* | | | |

---

## 7. Acceptance Criteria (per round)

- Every applied change traces to an approved row in §6.1–§6.4 and a §6.6 batch entry with passing tests.
- No orphaned `.meta` files (`git status` clean of unmatched meta churn).
- `DOCUMENT_INDEX.md` and folder indexes reflect all doc moves; superseded docs marked per `DOCUMENTATION_SYSTEM_SPEC.md`.
- Zero behavior change: EditMode suite green before and after the round; no new console errors on domain reload.
- **Learnability check:** a developer new to Unity/ECS can locate the system responsible for a given behavior from names and folder structure alone, without grepping — spot-check a few behaviors after each round.
- Out-of-scope improvements observed during the round are captured in §6.7, not lost and not silently applied.
- Another agent reading only this doc can answer: what was renamed, what was archived, what's still pending.

## Related Docs

- [/CLAUDE.md](/CLAUDE.md) — naming conventions and DOTS rules this plan enforces
- [DOCUMENT_INDEX.md](../DOCUMENT_INDEX.md) — discovery surface updated by doc batches
- [DOCUMENTATION_SYSTEM_SPEC.md](../DOCUMENTATION_SYSTEM_SPEC.md) — canonical-doc and archive rules for §6.3 work
- [PROJECT_STRUCTURE_DOTS.md](../PROJECT_STRUCTURE_DOTS.md) — target folder layout for code moves
