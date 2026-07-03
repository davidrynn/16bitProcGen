# Player Camera Bootstrap - Pure Code DOTS

This folder contains a **pure-code DOTS bootstrap** that spawns player and camera entities at runtime without requiring authoring components or subscenes.

## 📖 Complete Documentation

**See [`BOOTSTRAP_GUIDE.md`](BOOTSTRAP_GUIDE.md) for the comprehensive guide covering:**
- Quick start (30 seconds)
- Which bootstrap version to use
- Scene setup checklist
- Rendering explained (why you don't see anything)
- Physics integration
- Movement controls
- Customization options
- Troubleshooting
- Next steps

## ⚡ Quick Start

1. **Create Empty GameObject** in scene
2. **Add Component** → `PlayerCameraBootstrap_WithVisuals`
3. **Press Play** ▶️
4. **Controls:** WASD to move, Space to jump

## 📂 What's Included

### Core Scripts

- **`PlayerCameraBootstrap.cs`** - Minimal bootstrap (entity data only)
- **`PlayerCameraBootstrap_WithVisuals.cs`** - Full bootstrap (visuals + physics)
- **`SimplePlayerMovementSystem.cs`** - Basic WASD + Space movement

### Documentation

- **`BOOTSTRAP_GUIDE.md`** - Complete comprehensive guide (read this first!)
- **`README.md`** - This file (quick reference)

## 🎯 Which Bootstrap?

| Version | Use When |
|---------|----------|
| `PlayerCameraBootstrap` | Automated tests, data-only scenarios |
| `PlayerCameraBootstrap_WithVisuals` | Development, demos, visual testing |

## ✅ Advantages

- **Pure DOTS** - No GameObject/ECS hybrid confusion
- **Fast Iteration** - No baking delays
- **Testable** - Same pattern as runtime tests
- **Minimal Setup** - One GameObject, one script

## 🔗 Related Documentation

- **Complete Guide:** [`BOOTSTRAP_GUIDE.md`](BOOTSTRAP_GUIDE.md) ← Start here!
- **Architecture rules:** `/CLAUDE.md` (Core Architecture Principles — Minimal Bootstrap pattern)
- **Test Examples:** `../Test/CameraFollowSanityTest.cs`
- **Camera System:** `../Systems/CameraFollowSystem.cs`

