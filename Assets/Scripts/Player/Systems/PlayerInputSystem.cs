using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Captures player input from keyboard/mouse and manages cursor lock state.
    /// Runs in InitializationSystemGroup to ensure input is ready before simulation.
    /// Cannot use [BurstCompile] on OnUpdate because it accesses Mouse.current / Keyboard.current (managed).
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        private bool _cursorLocked;
        /// <summary>
        /// Tracks whether both mouse buttons were held last frame, so we can detect the
        /// release event (SlingshotReleased) as a one-frame transition.
        /// </summary>
        private bool _wasSlingshotHeld;
        /// <summary>
        /// When true, mouse buttons route to terrain editing instead of slingshot.
        /// Toggled by Tab. Persists across frames as system state.
        /// </summary>
        private bool _isEditMode;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
            _cursorLocked = false;
            _wasSlingshotHeld = false;
            _isEditMode = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                return;
            }

            // Handle cursor lock toggle with Escape key
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _cursorLocked = !_cursorLocked;
                UpdateCursorLockState(_cursorLocked);
            }

            // Auto-lock cursor when entering play mode
            if (!_cursorLocked && Application.isPlaying)
            {
                _cursorLocked = true;
                UpdateCursorLockState(true);
            }

            float2 move = float2.zero;
            if (keyboard.wKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            if (math.lengthsq(move) > 1f)
            {
                move = math.normalize(move);
            }

            // Only capture look input when cursor is locked
            float2 lookDelta = _cursorLocked ? mouse.delta.ReadValue() : float2.zero;
            bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            bool jumpHeld = keyboard.spaceKey.isPressed;

            // Tab toggles between movement mode and terrain-edit mode.
            // In edit mode, mouse buttons route to TerrainEditInputSystem instead of slingshot.
            bool tabPressed = keyboard.tabKey.wasPressedThisFrame;
            bool cameraTogglePressed = keyboard.vKey.wasPressedThisFrame;

            // Slingshot input: LMB + RMB simultaneous hold (only in movement mode)
            bool lmbHeld = mouse.leftButton.isPressed;
            bool rmbHeld = mouse.rightButton.isPressed;
            bool slingshotHeld = !_isEditMode && lmbHeld && rmbHeld;

            // Detect release: was held last frame, no longer held this frame
            bool slingshotReleased = _wasSlingshotHeld && !slingshotHeld;
            _wasSlingshotHeld = slingshotHeld;

            // Toggle edit mode on Tab press (before writing to component)
            if (tabPressed)
            {
                _isEditMode = !_isEditMode;
            }

            foreach (var (input, _) in SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<PlayerMovementConfig>>())
            {
                input.ValueRW.Move = move;
                // Lock camera during slingshot charge so drag doesn't rotate the view
                input.ValueRW.Look = slingshotHeld ? float2.zero : lookDelta;
                input.ValueRW.JumpPressed = input.ValueRO.JumpPressed || jumpPressed;
                input.ValueRW.JumpHeld = jumpHeld;
                input.ValueRW.IsEditMode = _isEditMode;

                // Slingshot: accumulate drag while held, reset on release
                input.ValueRW.SlingshotHeld = slingshotHeld;
                input.ValueRW.SlingshotReleased = slingshotReleased;
                if (slingshotHeld)
                {
                    // Accumulate mouse delta during charge
                    input.ValueRW.SlingshotDrag += lookDelta;
                }
                else if (slingshotReleased)
                {
                    // Keep drag for the release frame so launch system can read it
                }
                else
                {
                    // Reset drag when not charging
                    input.ValueRW.SlingshotDrag = float2.zero;
                }
            }

            // V key toggles 1st/3rd person camera
            if (cameraTogglePressed)
            {
                foreach (var camSettings in SystemAPI.Query<RefRW<PlayerCameraSettings>>())
                {
                    camSettings.ValueRW.IsThirdPerson = !camSettings.ValueRO.IsThirdPerson;
                }
            }
        }

        private static void UpdateCursorLockState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
