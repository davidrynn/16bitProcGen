# Documentation System Spec

**Status:** ACTIVE
**Last Updated:** 2026-04-11
**Owner:** Project-wide

---

## 1. Purpose

Define the project's documentation structure, metadata, and linking rules so humans and AI tools can find the authoritative document for a topic quickly without broad repo scans.

This document is about discoverability and canonicality, not content style alone.

---

## 2. Core Principles

1. Markdown files committed in the repo are the source of truth.
2. Every active topic should have one clearly identifiable canonical document.
3. Discovery should begin from an index, not from unconstrained full-folder search.
4. Document type and lifecycle must be obvious from the filename, top metadata, and index entry.
5. Obsolete material should be archived or explicitly marked superseded, not left ambiguous beside active docs.
6. AI-friendly documentation must work without editor-specific graph views, local databases, or extension-only metadata.

---

## 3. Canonical Discovery Flow

When looking for documentation, use this order:

1. Start with [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md).
2. From the index, move to the relevant folder or canonical topic document.
3. Prefer docs marked `ACTIVE`, `CURRENT`, `IMPLEMENTED`, `PROPOSED`, or similar active statuses.
4. Use `Archives/` only when:
   - an active doc links to it for history, or
   - historical context is explicitly needed.

This flow is intended for both humans and AI agents.

---

## 4. Required Metadata for Active Docs

Every active or current doc should expose enough information in the first 10-20 lines to be selected without reading the full file.

Minimum recommended metadata:

- `Status`
- `Last Updated`
- `Owner` or owning area
- short statement of purpose

For design or implementation docs, also include:

- `Scope`
- `Non-Goals`
- `Related Docs`
- `Acceptance Criteria` or equivalent completion signal

Optional but useful fields:

- `Supersedes`
- `Superseded By`
- `Audience`
- `Phase`
- `Keywords`
- `Canonical: yes`

The exact formatting can be plain markdown. It does not need YAML frontmatter, but it must be consistent and committed.

---

## 5. Document Types and Naming Rules

Use stable suffixes so search and indexing remain predictable.

Recommended types:

- `*_PLAN.md` or `*_PLAN_*.md`: sequencing, rollout, prioritization
- `*_SPEC.md`: behavior or implementation contract
- `*_SCHEMA.md`: data model or runtime structure
- `*_CHECKLIST.md`: concrete execution and validation steps
- `*_REPORT.md`: findings, audit, or post-implementation summary
- `*_INDEX.md` or `README.md`: folder map / entry point
- `*_NOTES.md`: working notes only when intentionally non-canonical

Guidelines:

- Prefer one canonical topic doc over multiple similarly named partial docs.
- Avoid vague names like `notes.md`, `new-plan.md`, or `ideas2.md` in active areas.
- If a topic grows beyond 3-5 tightly related files, place them in a dedicated subfolder.

Example already in use:

- `TerrainHeightMaps/TERRAIN_STRATEGY_PLAN.md`
- `TerrainHeightMaps/TERRAIN_BIOME_NOISE_SPEC.md`
- `TerrainHeightMaps/TERRAIN_BIOME_NOISE_SCHEMA.md`
- `TerrainHeightMaps/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md`

---

## 6. Folder-Level Rules

### 6.1 Root Index

[DOCUMENT_INDEX.md](DOCUMENT_INDEX.md) is the root documentation entry point.

Expectations:

- It should list active/canonical docs first.
- It should group by area or purpose.
- It should make historical or archived material clearly secondary.

### 6.2 Area Indexes

Each high-churn or high-volume folder should eventually have a local entry file.

Recommended examples:

- `Assets/Docs/AI/README.md`
- `Assets/Docs/AI/TerrainHeightMaps/README.md`
- `Assets/Docs/WFC/README.md`

Area indexes should summarize:

- what the folder covers
- which docs are canonical
- which docs are historical or deferred

### 6.3 Archives

Archive material should live under `Assets/Docs/Archives/` or otherwise be explicitly marked obsolete.

Archived docs must not look current.

Every superseded doc should say one of:

- `Superseded By: ...`
- `Obsolete`
- `Historical Reference Only`

---

## 7. Linking Rules

Every active canonical doc should be discoverable from at least one index.

Recommended rules:

1. Add every new active doc to [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md).
2. Add it to an area index when the folder has one.
3. Add a small `Related Docs` section to connect plan/spec/schema/checklist/report siblings.
4. When replacing a doc, update both sides:
   - old doc points to new doc
   - new doc states what it replaces when relevant

Relative markdown links are preferred so docs remain portable inside the repo.

---

## 8. AI Discovery Rules

These rules are specifically intended to reduce expensive broad search and improve retrieval quality.

1. Start at [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md) before scanning the full docs tree.
2. Prefer canonical documents over notes, traces, or archives.
3. Prefer the most recent active doc when multiple docs overlap.
4. If a document is not indexed, assume discoverability is incomplete and fix the index when practical.
5. If a topic has plan/spec/schema/checklist siblings, select based on intent:
   - behavior question -> spec
   - data model question -> schema
   - rollout/prioritization question -> plan
   - execution state question -> checklist or report
6. Avoid creating a new doc when an existing canonical doc should be updated instead.

---

## 9. Tooling Guidance

### 9.1 FOAM and Similar Extensions

Extensions like FOAM can be helpful for humans, but they are optional tooling, not the documentation system itself.

FOAM is useful when it encourages:

- consistent note templates
- backlinks between related docs
- aliases and tags that are also visible in markdown

FOAM is not sufficient when:

- important structure exists only in extension state
- graph view replaces maintained index files
- many overlapping notes are created without a canonical doc

Rule:

- If a human can only find a document through extension UI, the repo documentation structure is not strong enough yet.

### 9.3 Documentation Governance Skill

This repo may use a workspace skill for repeatable documentation maintenance tasks:

- `.agents/skills/documentation-governance/SKILL.md`

Use it for:

- creating new canonical docs
- deciding whether to update or create
- updating indexes
- reorganizing docs for discoverability
- marking docs superseded or archival

The skill complements this spec. It does not replace the repo-based documentation structure.

### 9.2 Machine-Readable Catalogs

If the docs set grows further, a small committed catalog file can help tooling and AI.

Recommended future file:

- `Assets/Docs/DOC_CATALOG.json`

Suggested fields:

- `path`
- `title`
- `area`
- `status`
- `kind`
- `canonical`
- `supersedes`
- `keywords`

This is optional. The markdown index remains the primary source of truth.

---

## 10. Recommended Template for New Active Docs

```md
# Title

**Status:** PROPOSED | ACTIVE | IMPLEMENTED | COMPLETE | DEFERRED
**Last Updated:** YYYY-MM-DD
**Owner:** area or team

---

## 1. Purpose

Short explanation of why this document exists.

## 2. Scope

What is included.

## 3. Non-Goals

What is intentionally excluded.

## 4. Related Docs

- sibling plan/spec/schema/checklist/report docs

## 5. Acceptance Criteria

How to tell when the work or design is complete.
```

The exact section order may vary by doc type, but the top metadata should remain stable.

---

## 11. Maintenance Workflow

When adding or significantly changing an active doc:

1. Choose the correct document type and stable file name.
2. Add or update metadata at the top.
3. Add the doc to [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md).
4. Add or update `Related Docs` links.
5. Mark replaced docs as superseded or move them to archives.
6. Update `Last Updated` only when meaningful content changed.

When a topic becomes crowded or hard to navigate:

1. create a subfolder
2. move related docs together
3. add a local `README.md` or `INDEX.md`
4. update the root index

---

## 12. Practical Standard for This Repo

For this repository, the minimum acceptable documentation structure is:

- one root entry point: [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md)
- clear active vs archive separation
- stable doc-type naming
- explicit metadata on active docs
- index updates whenever a new active doc is added

That is the baseline needed for reliable AI and human discovery.