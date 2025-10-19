# AI Assistant Instructions (Cursor-Only Version)

## 1. Purpose & Overview
This document defines how **Cursor AI Agent** collaborates on this **Unity DOTS 16-bit Crafting Game** project.  
It establishes behavioral and technical standards for all AI-assisted edits, ensuring safe, incremental, and educational code contributions.

---

## 2. Working Style & Core Principles

### 2.1 Development Philosophy
- **Work incrementally** – Small, reversible, testable changes only.
- **Stay scoped** – Implement exactly what’s in the SPEC, no extra features.
- **Ask first** – Pause and clarify when requirements, APIs, or behavior are uncertain.
- **Respect removals** – Never restore deleted components or code paths.
- **Maintain architectural intent** – Follow established project conventions.

### 2.2 Communication Style
- Be **collaborative but concise** – explain the “what” and “why” of each change.
- Avoid assumptions beyond the SPEC.
- Ask questions when uncertain; never fabricate details.

---

## 3. Technical Standards

### 3.1 Unity DOTS Practices
- All **Systems** must be `partial`.
- **One class per file**; filename matches class name.
- Maintain unique class names and namespaces.
- After large refactors, **clear Library, Temp, and obj** and restart Unity.
- Do not define systems/components in Markdown or non-code files.

### 3.2 Code Quality
- Remove legacy paths and redundant logic.
- Prefer **type-safe constructs** (e.g., enums vs. ints).
- Wrap verbose logs and features in debug flags.
- Keep output silent unless debugging.
- No unrelated formatting or refactors.

### 3.3 Build & Editor Hygiene
- Wrap editor-only systems with `#if UNITY_EDITOR`.
- Use `DebugSettings` for runtime toggles.
- Maintain a clean console.
- No automatic code generation without request.

---

## 4. Debug & Testing

### 4.1 Defaults
| Setting | Default | Description |
|----------|----------|-------------|
| `EnableTestSystems` | false | Disable test systems by default |
| `EnableDebugLogging` | false | No console spam |
| `EnableWFCDebug` | false | WFC tracing off |
| `EnableRenderingDebug` | false | Rendering debug off |

### 4.2 Testing Guidelines
- Always **write or extend tests before code**.
- Use isolated, configurable test cases.
- Wrap editor-only tests with `#if UNITY_EDITOR`.
- Keep test naming clear and consistent.

---

## 5. Agent Contract

### 5.1 Task Lifecycle (Always)
1. **SPEC (delta)** – Write 8–10 bullets describing: Objective, Scope, Inputs/Outputs, Files Touched, Edge Cases, and Definition of Done.  
2. **TEST FIRST** – Add or modify tests to confirm expected and current behaviors.  
3. **CODE** – Implement only enough to make the tests pass.  
4. **REVIEW** – Summarize changes (files/symbols), note untested branches, TODOs, and performance notes.

### 5.2 Change Budget & Scope
- Max **50 lines** and **3 files** per edit cycle.  
- Exceeding budget → **pause and propose** updated SPEC/TEST plan.  
- Only modify explicitly listed files.  
- Do not reformat or refactor unrelated code.

### 5.3 Unknowns & Safety
- If any symbol/API/path is unclear → **stop and ask up to 5 questions**.  
- Never invent APIs. Cite file/line references.  
- Public signatures are **pinned**; modifications require a SPEC delta and `MIGRATION_NOTES.md`.

### 5.4 Diff Gate (Before Apply)
Each proposed edit must include:
- File list
- Hunk count
- 3–5 bullet rationale linking each change → SPEC objectives

### 5.5 Testing & Debug
- Generate a **TEST_PLAN.md** before implementing code.  
- Wrap verbose logs under debug flags.  
- `#if UNITY_EDITOR` for all editor-specific systems.

---

## 6. Cursor Prompts

### 6.1 SPEC Clarifier (Ask Mode)
> Produce a SPEC **delta only** for the following task.  
> Include: Objective, Scope, Inputs/Outputs, Files Touched, Edge Cases, DoD, Risks.  
> Ask up to 5 clarifying questions.  
> **Task:** <paste task>

### 6.2 Constrained Edit (Agent Mode)
> Implement **only** what’s necessary to make the new tests pass.  
> Follow the **Agent Contract** (change limits, safety rules, no formatting).  
> If additional edits are needed, stop and propose an updated TEST_PLAN.md.

---

## 7. Maintenance
- Treat this file as the behavioral contract.  
- Update quarterly or after major ECS/system changes.  
- This replaces `.cursorrules` for all Cursor agent operations.

---

*File purpose: Defines mandatory behavioral and safety standards for Cursor AI in this Unity DOTS project.*
