# Project Structure – DOTS-First Overview

## Purpose
This document describes the high-level structure of the 16bitProcGen project as it relates to Unity DOTS (Entities/ECS), including folder layout, assembly definitions, and best practices for modular, scalable DOTS development.

---

## Folder Structure (Key Areas)

```
Assets/
  Docs/                # Project documentation (this file, migration plans, specs)
  Scripts/
    Authoring/         # MonoBehaviours, ScriptableObjects, and DOTS bootstraps
    DOTS/              # Pure DOTS systems, components, jobs, and ECS logic
      Core/            # Core ECS utilities, debug, and shared logic
      WFC/             # Wave Function Collapse systems and data
      Terrain/         # Terrain generation, SDF, and mesh systems
      ...              # Other DOTS feature areas
    Player/            # Player systems, input, camera, and bootstrap logic
    ...                # Other gameplay or feature modules
  ScriptableObjects/   # (Legacy) ScriptableObject assets (migrating to Authoring/)
  ...
```

---

## Assembly Definitions (.asmdef)
- **Modular assemblies** for each major feature (e.g., DOTS.Terrain, DOTS.Player.Bootstrap, Core, etc.)
- **Authoring**: MonoBehaviours and ScriptableObjects for scene setup and configuration
- **DOTS**: Pure ECS systems and components, grouped by feature
- **Tests**: Separate assemblies for PlayMode and EditMode tests

---

## DOTS-First Principles
- **All runtime logic in ECS systems/components**
- **MonoBehaviours only for authoring, bootstrapping, or hybrid visualization**
- **ScriptableObjects for config, referenced by bootstraps/authoring**
- **No cross-feature dependencies except via Core or explicit references**
- **[DisableAutoCreation]** on all systems not meant to run by default; enable via bootstrap/config

---

## Refactoring Guidance
- As you refactor, update this document to reflect new assemblies, folders, or architectural changes.
- Keep DOTS systems and authoring/config separate for clarity and modularity.
- Document any new feature area with a short section here.

---

_Last updated: 2025-12-23_
