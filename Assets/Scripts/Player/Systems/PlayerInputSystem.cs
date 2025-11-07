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
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        private bool _cursorLocked;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
            _cursorLocked = false;
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

            foreach (var (input, _) in SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<PlayerMovementConfig>>())
            {
                input.ValueRW.Move = move;
                input.ValueRW.Look = lookDelta;
                input.ValueRW.JumpPressed = input.ValueRO.JumpPressed || jumpPressed;
            }
        }

        private static void UpdateCursorLockState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
