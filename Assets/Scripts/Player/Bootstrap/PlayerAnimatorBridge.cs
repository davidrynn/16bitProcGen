using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Hybrid bridge: reads ECS PlayerMovementState each frame and drives the Animator on the
    /// companion character visual. Never writes to ECS — purely a read-only view layer.
    /// Runs after PlayerVisualSync (DefaultExecutionOrder 1001) so transform is already synced.
    /// </summary>
    [DefaultExecutionOrder(1001)]
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [Tooltip("The ECS player entity. Assigned by bootstrap at runtime.")]
        public Entity TargetEntity;

        [Tooltip("Animator on the character child GameObject.")]
        public Animator CharacterAnimator;

        [Tooltip("Top movement speed used to normalize the Speed blend parameter.")]
        [Min(0.1f)]
        public float RunSpeed = 7f;

        private EntityManager _em;
        private World _world;
        private bool _valid;
        private PlayerMovementMode _lastMode = PlayerMovementMode.Grounded;

        // Pre-hashed Animator parameter names
        private static readonly int SpeedHash           = Animator.StringToHash("Speed");
        private static readonly int MovementModeHash    = Animator.StringToHash("MovementMode");
        private static readonly int ChargingHash        = Animator.StringToHash("ChargingNormalized");
        private static readonly int LandingTriggerHash  = Animator.StringToHash("LandingTrigger");
        private static readonly int GroundedBoolHash    = Animator.StringToHash("GroundedBool");
        // True while Ballistic and still rising (vy >= 0) — drives T-pose vs Falling split
        private static readonly int BallisticRisingHash = Animator.StringToHash("BallisticRising");

        private void LateUpdate()
        {
            if (!TryResolveWorld()) return;
            if (TargetEntity == Entity.Null || !_em.Exists(TargetEntity)) return;
            if (CharacterAnimator == null) return;
            if (!_em.HasComponent<PlayerMovementState>(TargetEntity)) return;

            var state = _em.GetComponentData<PlayerMovementState>(TargetEntity);

            // Normalize horizontal speed for walk/run blend tree
            var horizSpeed = math.length(new float2(state.Velocity.x, state.Velocity.z));
            CharacterAnimator.SetFloat(SpeedHash, Mathf.Clamp01(horizSpeed / Mathf.Max(RunSpeed, 0.1f)));

            CharacterAnimator.SetInteger(MovementModeHash, (int)state.Mode);
            CharacterAnimator.SetBool(GroundedBoolHash, state.Mode == PlayerMovementMode.Grounded);
            // Within Ballistic: rising (vy >= 0) = T-pose, falling (vy < 0) = Falling clip
            CharacterAnimator.SetBool(BallisticRisingHash, state.Mode == PlayerMovementMode.Ballistic && state.Velocity.y >= 0f);

            // Slingshot charge depth — present only while charging
            if (_em.HasComponent<SlingshotChargeState>(TargetEntity))
            {
                var charge = _em.GetComponentData<SlingshotChargeState>(TargetEntity);
                CharacterAnimator.SetFloat(ChargingHash, charge.ChargeNormalized);
            }
            else
            {
                CharacterAnimator.SetFloat(ChargingHash, 0f);
            }

            // Fire landing trigger once on airborne → Grounded transition
            var wasAirborne = _lastMode is PlayerMovementMode.Ballistic
                or PlayerMovementMode.GlideCharging
                or PlayerMovementMode.Gliding
                or PlayerMovementMode.ThermalBoost;
            if (wasAirborne && state.Mode == PlayerMovementMode.Grounded)
                CharacterAnimator.SetTrigger(LandingTriggerHash);

            _lastMode = state.Mode;
        }

        private bool TryResolveWorld()
        {
            if (_valid && _world is { IsCreated: true }) return true;
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) { _valid = false; return false; }
            _em = _world.EntityManager;
            _valid = true;
            return true;
        }
    }
}
