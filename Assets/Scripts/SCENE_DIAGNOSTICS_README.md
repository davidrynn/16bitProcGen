# Scene Diagnostics Helper

## Overview

The `SceneDiagnostics` component provides comprehensive diagnostic information about your scene setup, checking player, camera, terrain, and lighting configuration.

## Quick Start

1. **Add to Scene**: Create an empty GameObject in your scene and add the `SceneDiagnostics` component
2. **Run**: The diagnostics will automatically run on Start (after a 0.5s delay to let systems initialize)
3. **View Results**: Check the Unity Console for detailed diagnostic reports

## Features

### Automatic Diagnostics
- Runs automatically when the scene starts
- Can be configured to run periodically during play
- Shows results in both console and on-screen (optional)

### What It Checks

1. **Player Setup**
   - ECS player entity existence and components
   - Player visual GameObject
   - Player position and configuration

2. **Camera Setup**
   - ECS camera entity
   - Camera GameObject and component
   - Camera settings (FOV, clipping planes, etc.)
c
3. **Terrain Setup**c
   - GameObject-based terrain chunks (TerrainChunk components)c
   - TerrainManager instance
   - Mesh renderers and mesh validity
   - Generation status

4. **DOTS Terrain**
   - DOTS terrain entities
   - Render components

5. **Lighting**
   - Light components in scene
   - Ambient lighting settings

## Configuration

### Inspector Settings

- **Run On Start**: Automatically run diagnostics when scene starts
- **Run Periodically**: Run diagnostics every N seconds during play
- **Periodic Interval**: Time between periodic checks (default: 5s)
- **Show On Screen**: Display diagnostic results in a GUI panel
- **Show Detailed Info**: Include detailed component information
- **Check Options**: Toggle which systems to check (player, camera, terrain, etc.)

### Manual Trigger

- Right-click the component in Inspector → **"Run Diagnostics Now"**
- Or call `RunDiagnostics()` from code

## Example Output

```
========================================
SCENE DIAGNOSTICS REPORT #1
Time: 2.50s | Frame: 150
========================================

--- PLAYER SETUP ---
✓ Found 1 player entity/entities (ECS)
  Components: Transform=True, Movement=True, Input=True, View=True
  Position: (0.0, 2.0, 0.0)
✓ Found player visual GameObject: Player Visual (ECS Synced)
  Position: (0.0, 2.0, 0.0)
  Active: True

--- CAMERA SETUP ---
✓ Found 1 camera entity/entities (ECS)
✓ Found camera GameObject: Main Camera (ECS Player)
  Enabled: True
  Tag: MainCamera
  Position: (0.0, 3.6, 0.0)
  FOV: 60

--- TERRAIN SETUP (GameObject-based) ---
Found 5 TerrainChunk component(s)
  Generated: 5/5
  Visible: 5/5
  Has Mesh: 5/5

========================================
END OF DIAGNOSTICS REPORT
========================================
```

## Integration Tests

The `BasicSceneSetupTests` class provides automated tests that verify:
- Player and camera entities exist
- Required components are present
- Terrain chunks are generated and visible
- Scene integration is working

Run these tests via Unity Test Runner (Window > General > Test Runner).

## Limitations

**Important**: These diagnostics check that components exist and are configured correctly, but they **cannot verify actual visual rendering**. To verify that chunks are actually visible on screen:

1. **Manual Inspection**: Run the scene and visually check
2. **Graphics Tests**: Use Unity Test Framework Graphics tests (requires setup)
3. **Screenshot Comparison**: Capture and compare screenshots (complex setup)

## Troubleshooting

### "No player entity found"
- Check that `PlayerEntityBootstrap` system is enabled
- Verify DOTS world is initialized
- Check console for bootstrap errors

### "No camera GameObject found"
- Check that camera is being created by bootstrap
- Verify camera GameObject name matches expected name
- Check that camera is not being destroyed

### "No terrain chunks found"
- If using GameObject terrain: Check TerrainManager is in scene
- If using DOTS terrain: Check terrain generation systems are running
- Verify terrain entities are being created

### "Chunks not generated"
- Check terrain generation systems are enabled
- Verify biome data is available
- Check for errors in terrain generation pipeline

## Tips

- Run diagnostics immediately after scene start to catch initialization issues
- Use periodic diagnostics to monitor scene state during play
- Enable detailed info for debugging specific component issues
- Use on-screen display for quick visual feedback during testing


