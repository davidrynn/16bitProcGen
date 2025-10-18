using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace DOTS.Player.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct PlayerInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                return;
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

            float2 lookDelta = mouse.delta.ReadValue();
            bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;

            foreach (var (input, _) in SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<PlayerMovementConfig>>())
            {
                input.ValueRW.Move = move;
                input.ValueRW.Look = lookDelta;
                input.ValueRW.JumpPressed = input.ValueRO.JumpPressed || jumpPressed;
            }
        }
    }
}
