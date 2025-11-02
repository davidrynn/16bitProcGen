# Player Movement Test Plan

## Overview
This document outlines the testing procedure for the DOTS-based player movement system.

## System Architecture
The player movement system consists of:

1. **PlayerInputSystem** (Presentation) - Captures WASD + mouse input
2. **PlayerGroundingSystem** (Physics) - Raycast-based ground detection  
3. **PlayerMovementSystem** (Physics) - Converts input to physics velocities
4. **PlayerCameraSystem** (Presentation) - Handles view control

## Test Setup

### Option 1: Use PlayerTest Scene (Recommended)
1. Open `Scenes/PlayerTest.unity` in Unity
2. Press Play - scene auto-sets up:
   - Blue capsule player with DOTS movement
   - Green ground plane
   - Camera attached to player
   - Proper lighting
   - Debugger for monitoring

### Option 2: Manual Setup
1. Create empty GameObject
2. Add `PlayerTestSetup` script
3. Right-click component → "Setup Test Environment"
4. Press Play

## Test Cases

### 1. Basic Movement (WASD)
**Expected Behavior:**
- W/S moves forward/backward
- A/D moves left/right
- Movement is smooth and responsive
- Player rotates to face movement direction

**Test Steps:**
1. Press W - player should move forward
2. Press S - player should move backward
3. Press A - player should move left
4. Press D - player should move right
5. Press W+A - player should move diagonally forward-left
6. Press S+D - player should move diagonally backward-right

**Success Criteria:**
- ✅ Movement is smooth and responsive
- ✅ No stuttering or lag
- ✅ Player rotates correctly with movement
- ✅ Diagonal movement works properly

### 2. Jumping (Space)
**Expected Behavior:**
- Space bar triggers jump when grounded
- Jump has proper physics arc
- Player cannot jump while airborne
- Landing is smooth

**Test Steps:**
1. Press Space while on ground - should jump
2. Press Space while airborne - should not jump again
3. Wait to land, then press Space - should jump again
4. Try rapid Space presses - should only jump once per landing

**Success Criteria:**
- ✅ Jump works when grounded
- ✅ No mid-air jumping
- ✅ Proper jump physics
- ✅ Smooth landing

### 3. Mouse Look
**Expected Behavior:**
- Mouse X rotates player horizontally (yaw)
- Mouse Y rotates camera vertically (pitch)
- Pitch is clamped to prevent over-rotation
- Camera follows player position

**Test Steps:**
1. Move mouse left/right - player should rotate
2. Move mouse up/down - camera should pitch
3. Try extreme mouse movements - pitch should be clamped
4. Move player while looking around - camera should follow

**Success Criteria:**
- ✅ Horizontal mouse look works
- ✅ Vertical mouse look works
- ✅ Pitch clamping prevents over-rotation
- ✅ Camera follows player position

### 4. Air Control
**Expected Behavior:**
- Limited steering while airborne
- Air control is less responsive than ground movement
- Player maintains momentum in air

**Test Steps:**
1. Jump and try to steer left/right while airborne
2. Compare air control to ground movement
3. Notice reduced responsiveness in air

**Success Criteria:**
- ✅ Air control works but is limited
- ✅ Less responsive than ground movement
- ✅ Maintains momentum

### 5. Ground Detection
**Expected Behavior:**
- Player knows when on ground vs airborne
- Ground detection is accurate
- No false positives/negatives

**Test Steps:**
1. Walk on ground - should be grounded
2. Jump - should become airborne
3. Land - should become grounded again
4. Walk off edge - should become airborne

**Success Criteria:**
- ✅ Accurate ground detection
- ✅ Proper state transitions
- ✅ No false positives

## Debug Information

### Console Logs
The system should log:
- "=== SETTING UP PLAYER TEST ENVIRONMENT ==="
- "Player entity found: [EntityID]"
- System status updates from debugger

### Debugger GUI
The PlayerMovementDebugger shows:
- Player entity ID
- Input data (Move, Look, Jump)
- Movement state (Mode, Grounded, FallTime)
- View data (Yaw, Pitch)
- Transform data (Position, Rotation)
- Physics data (Linear, Angular velocity)

### Systems Window
Check that these systems are running:
- PlayerInputSystem
- PlayerGroundingSystem  
- PlayerMovementSystem
- PlayerCameraSystem

## Troubleshooting

### Common Issues

**1. No Movement**
- Check Console for errors
- Verify DOTS systems are running
- Ensure PlayerAuthoring is configured
- Check that PhysicsWorldSingleton exists

**2. No Mouse Look**
- Verify Input System package is installed
- Check that camera is linked to player
- Ensure PlayerCameraSystem is running

**3. No Jumping**
- Check ground detection is working
- Verify PhysicsVelocity component exists
- Ensure jump input is being captured

**4. Camera Issues**
- Check camera positioning in PlayerCameraSystem
- Verify camera is linked to player entity
- Ensure LocalTransform components exist

### Debug Commands
- Right-click PlayerTestSetup → "Setup Test Environment"
- Right-click PlayerMovementDebugger → "Force Debug Update"
- Check Systems window for system status

## Performance Expectations
- 60+ FPS with smooth movement
- No stuttering or frame drops
- Responsive input (no lag)
- Smooth camera movement

## Success Criteria
The player movement system is working correctly when:
- ✅ All test cases pass
- ✅ Movement feels responsive and smooth
- ✅ No console errors
- ✅ All systems are running
- ✅ Debug information is accurate

## Next Steps
Once basic movement is working:
1. Tune movement parameters for better feel
2. Add additional movement modes (slingshot, swim)
3. Integrate with terrain destruction system
4. Add resource collection mechanics
