using System;
using System.Collections.Generic;
using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Hybrid bridge: reads ECS PlayerMovementState each frame and drives the Animator on the
    /// companion character visual. Never writes to ECS — purely a read-only view layer.
    /// Runs after PlayerVisualSync (DefaultExecutionOrder 1001) so transform is already synced,
    /// allowing the slide-facing yaw override to win on the same frame.
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

        [Tooltip("When enabled, logs animator parameter writes, trigger dispatches, missing controller parameters, and base-layer state changes.")]
        public bool EnableDebugLogging;

        private EntityManager _em;
        private World _world;
        private bool _valid;
        private bool _parametersValidated;
        private RuntimeAnimatorController _validatedController;
        private float _lastLoggedSpeed = float.NaN;
        private int _lastLoggedMovementMode = int.MinValue;
        private bool _lastLoggedGrounded;
        private bool _hasLoggedGrounded;
        private bool _lastLoggedBallisticRising;
        private bool _hasLoggedBallisticRising;
        private float _lastLoggedCharge = float.NaN;
        private float _lastLoggedRecovery = float.NaN;
        private int _lastLoggedStateHash = int.MinValue;
        private int _lastLoggedNextStateHash = int.MinValue;
        private bool _lastLoggedInTransition;

        // Cached optional parameter availability — populated by ValidateAnimatorParameters
        private bool _hasLandingRecoveryParam;

        // Pre-hashed Animator parameter names
        private static readonly int SpeedHash                 = Animator.StringToHash("Speed");
        private static readonly int MovementModeHash          = Animator.StringToHash("MovementMode");
        private static readonly int ChargingHash              = Animator.StringToHash("ChargingNormalized");
        private static readonly int LandingTriggerHash        = Animator.StringToHash("LandingTrigger");
        private static readonly int StandardLandingTriggerHash = Animator.StringToHash("StandardLandingTrigger");
        private static readonly int HardLandingTriggerHash    = Animator.StringToHash("HardLandingTrigger");
        private static readonly int SlideLandingTriggerHash   = Animator.StringToHash("SlideLandingTrigger");
        private static readonly int GroundedBoolHash          = Animator.StringToHash("GroundedBool");
        // True while Ballistic and still rising (vy >= 0). The controller uses this to
        // distinguish grounded Jump takeoff from Falling, and to keep slingshot release
        // on BallisticRise until the ascent turns into descent.
        private static readonly int BallisticRisingHash       = Animator.StringToHash("BallisticRising");
        // 0→1 countdown driven by LandingRecoveryTime/Duration for blend tree timing
        private static readonly int LandingRecoveryHash       = Animator.StringToHash("LandingRecoveryNormalized");

        private static readonly string[] RequiredAnimatorParameterNames =
        {
            "Speed",
            "MovementMode",
            "ChargingNormalized",
            "LandingTrigger",
            "GroundedBool",
            "BallisticRising",
        };

        private static readonly string[] OptionalAnimatorParameterNames =
        {
            "StandardLandingTrigger",
            "HardLandingTrigger",
            "SlideLandingTrigger",
            "LandingRecoveryNormalized",
        };

        private static readonly Dictionary<int, string> BaseLayerStateNamesByHash = new Dictionary<int, string>
        {
            { Animator.StringToHash("Base Layer.Idle"), "Idle" },
            { Animator.StringToHash("Base Layer.LocoBlend"), "LocoBlend" },
            { Animator.StringToHash("Base Layer.SlingshotCharge"), "SlingshotCharge" },
            { Animator.StringToHash("Base Layer.Slingshot_Charge_Start"), "Slingshot_Charge_Start" },
            { Animator.StringToHash("Base Layer.Slingshot_Charge_Hold"), "Slingshot_Charge_Hold" },
            { Animator.StringToHash("Base Layer.Slingshot_Release"), "Slingshot_Release" },
            { Animator.StringToHash("Base Layer.Jump"), "Jump" },
            { Animator.StringToHash("Base Layer.BallisticRise"), "BallisticRise" },
            { Animator.StringToHash("Base Layer.Falling"), "Falling" },
            { Animator.StringToHash("Base Layer.GlideCharging"), "GlideCharging" },
            { Animator.StringToHash("Base Layer.Gliding"), "Gliding" },
            { Animator.StringToHash("Base Layer.ThermalBoost"), "ThermalBoost" },
            { Animator.StringToHash("Base Layer.Landing"), "Landing" },
            { Animator.StringToHash("Idle"), "Idle" },
            { Animator.StringToHash("LocoBlend"), "LocoBlend" },
            { Animator.StringToHash("SlingshotCharge"), "SlingshotCharge" },
            { Animator.StringToHash("Slingshot_Charge_Start"), "Slingshot_Charge_Start" },
            { Animator.StringToHash("Slingshot_Charge_Hold"), "Slingshot_Charge_Hold" },
            { Animator.StringToHash("Slingshot_Release"), "Slingshot_Release" },
            { Animator.StringToHash("Jump"), "Jump" },
            { Animator.StringToHash("BallisticRise"), "BallisticRise" },
            { Animator.StringToHash("Falling"), "Falling" },
            { Animator.StringToHash("GlideCharging"), "GlideCharging" },
            { Animator.StringToHash("Gliding"), "Gliding" },
            { Animator.StringToHash("ThermalBoost"), "ThermalBoost" },
            { Animator.StringToHash("Landing"), "Landing" },
        };

        private void LateUpdate()
        {
            if (!TryResolveWorld()) return;
            if (TargetEntity == Entity.Null || !_em.Exists(TargetEntity)) return;
            if (CharacterAnimator == null) return;
            if (!_em.HasComponent<PlayerMovementState>(TargetEntity)) return;

            if (!_parametersValidated || CharacterAnimator.runtimeAnimatorController != _validatedController)
            {
                ValidateAnimatorParameters();
                if (EnableDebugLogging) LogAnimatorStateSnapshot(force: true);
            }

            var state = _em.GetComponentData<PlayerMovementState>(TargetEntity);

            // Normalize horizontal speed for walk/run blend tree
            var horizSpeed = math.length(new float2(state.Velocity.x, state.Velocity.z));
            float normalizedSpeed = Mathf.Clamp01(horizSpeed / Mathf.Max(RunSpeed, 0.1f));
            CharacterAnimator.SetFloat(SpeedHash, normalizedSpeed);
            LogFloatParameterChange("Speed", normalizedSpeed, ref _lastLoggedSpeed, 0.05f);

            CharacterAnimator.SetInteger(MovementModeHash, (int)state.Mode);
            LogMovementModeChange(state.Mode);

            // IsGrounded is the semantic source for GroundedBool. Mode stays Ballistic
            // while speed > 2 m/s after contact, so Mode-based gating would show the wrong
            // state during the high-speed landing window.
            CharacterAnimator.SetBool(GroundedBoolHash, state.IsGrounded);
            LogBoolParameterChange("GroundedBool", state.IsGrounded, ref _lastLoggedGrounded, ref _hasLoggedGrounded);

            // Within Ballistic: rising (vy >= 0) selects the upward branch
            // (Jump from grounded locomotion or BallisticRise from slingshot release),
            // while falling (vy < 0) selects Falling.
            bool ballisticRising = state.Mode == PlayerMovementMode.Ballistic && state.Velocity.y >= 0f;
            CharacterAnimator.SetBool(BallisticRisingHash, ballisticRising);
            LogBoolParameterChange("BallisticRising", ballisticRising, ref _lastLoggedBallisticRising, ref _hasLoggedBallisticRising);

            // Slingshot charge depth — present only while charging
            float chargeNormalized = 0f;
            if (_em.HasComponent<SlingshotChargeState>(TargetEntity))
            {
                var charge = _em.GetComponentData<SlingshotChargeState>(TargetEntity);
                chargeNormalized = charge.ChargeNormalized;
            }

            CharacterAnimator.SetFloat(ChargingHash, chargeNormalized);
            LogFloatParameterChange("ChargingNormalized", chargeNormalized, ref _lastLoggedCharge, 0.05f);

            // Landing recovery normalized (1 at impact, 0 when recovery ends).
            float recoveryNorm = state.LandingRecoveryDuration > 0f
                ? state.LandingRecoveryTime / state.LandingRecoveryDuration
                : 0f;
            if (_hasLandingRecoveryParam)
            {
                CharacterAnimator.SetFloat(LandingRecoveryHash, recoveryNorm);
                LogFloatParameterChange("LandingRecoveryNormalized", recoveryNorm, ref _lastLoggedRecovery, 0.05f);
            }

            // Tiered landing triggers — fired once on the LandingImpactEvent frame.
            // LandingDetectionSystem enables the event on contact (IsGrounded edge) so the
            // correct trigger fires on the actual touch-down frame, not after Mode catches up.
            bool hasLandingEvent = _em.HasComponent<LandingImpactEvent>(TargetEntity) &&
                                   _em.IsComponentEnabled<LandingImpactEvent>(TargetEntity);
            if (hasLandingEvent)
            {
                var evt = _em.GetComponentData<LandingImpactEvent>(TargetEntity);
                LandingConfig cfg = _em.HasComponent<LandingConfig>(TargetEntity)
                    ? _em.GetComponentData<LandingConfig>(TargetEntity)
                    : LandingConfig.Default;

                if (cfg.UseSimpleLandingTrigger)
                {
                    // Safe fallback: fires LandingTrigger for all landings.
                    // Works with any animator controller that has the original single-trigger setup.
                    // Flip UseSimpleLandingTrigger = false only after the controller has dedicated
                    // states for StandardLandingTrigger, HardLandingTrigger, and SlideLandingTrigger.
                    CharacterAnimator.SetTrigger(LandingTriggerHash);
                    LogLandingTrigger("LandingTrigger", evt, cfg, state, "simple fallback");
                }
                else
                {
                    bool isHard  = evt.VerticalSpeed >= cfg.HardLandingVerticalSpeed;
                    bool isSlide = isHard && evt.HorizontalSpeed >= cfg.SlideThresholdHorizontalSpeed;
                    bool isStd   = !isHard && evt.VerticalSpeed >= cfg.StandardLandingVerticalSpeed;

                    if (isSlide)
                    {
                        CharacterAnimator.SetTrigger(SlideLandingTriggerHash);
                        LogLandingTrigger("SlideLandingTrigger", evt, cfg, state, "hard impact with slide speed");
                    }
                    else if (isHard)
                    {
                        CharacterAnimator.SetTrigger(HardLandingTriggerHash);
                        LogLandingTrigger("HardLandingTrigger", evt, cfg, state, "hard impact");
                    }
                    else if (isStd)
                    {
                        CharacterAnimator.SetTrigger(StandardLandingTriggerHash);
                        LogLandingTrigger("StandardLandingTrigger", evt, cfg, state, "standard impact");
                    }
                    else
                    {
                        CharacterAnimator.SetTrigger(LandingTriggerHash);
                        LogLandingTrigger("LandingTrigger", evt, cfg, state, "light impact fallback");
                    }
                }
            }

            // During a slide recovery, override the visual root's Y rotation so the character
            // faces their direction of travel. Hard and standard landings keep camera-yaw
            // facing (set by PlayerVisualSync) so the stumble looks correct.
            // This bridge runs at 1001, after PlayerVisualSync at 1000, so this write wins.
            if (state.LandingIsSlide && state.LandingRecoveryTime > 0f)
            {
                var horizVel = new float2(state.Velocity.x, state.Velocity.z);
                if (math.lengthsq(horizVel) > 0.01f)
                {
                    // horizVel.x = world X, horizVel.y = world Z (float2 packs x,z)
                    float yaw = Mathf.Rad2Deg * Mathf.Atan2(horizVel.x, horizVel.y);
                    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }

            LogAnimatorStateSnapshot();

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

        private void ValidateAnimatorParameters()
        {
            _parametersValidated = true;
            _validatedController = CharacterAnimator.runtimeAnimatorController;

            var parameters = CharacterAnimator.parameters;
            var availableParameters = new HashSet<string>(StringComparer.Ordinal);
            var parameterList = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                availableParameters.Add(parameters[i].name);
                if (i > 0)
                {
                    parameterList.Append(", ");
                }

                parameterList.Append(parameters[i].name);
            }

            // Cache optional parameter availability so callers can guard writes safely
            _hasLandingRecoveryParam = availableParameters.Contains("LandingRecoveryNormalized");

            if (EnableDebugLogging)
            {
                Debug.Log($"[PlayerAnimatorBridge] Animator controller '{GetControllerName()}' attached. Parameters: {parameterList}", this);
                LogMissingParameters(RequiredAnimatorParameterNames, availableParameters, isRequired: true);
                LogMissingParameters(OptionalAnimatorParameterNames, availableParameters, isRequired: false);
            }
        }

        private void LogMissingParameters(string[] expectedParameters, HashSet<string> availableParameters, bool isRequired)
        {
            for (int i = 0; i < expectedParameters.Length; i++)
            {
                if (availableParameters.Contains(expectedParameters[i]))
                {
                    continue;
                }

                if (isRequired)
                {
                    Debug.LogWarning($"[PlayerAnimatorBridge] Animator controller '{GetControllerName()}' is missing required parameter '{expectedParameters[i]}'.", this);
                }
                else
                {
                    Debug.Log($"[PlayerAnimatorBridge] Animator controller '{GetControllerName()}' does not define optional parameter '{expectedParameters[i]}'. That is expected until the controller is upgraded to the tiered landing setup.", this);
                }
            }
        }

        private void LogFloatParameterChange(string parameterName, float value, ref float lastValue, float epsilon)
        {
            if (!EnableDebugLogging)
            {
                return;
            }

            if (!float.IsNaN(lastValue) && Mathf.Abs(lastValue - value) < epsilon)
            {
                return;
            }

            lastValue = value;
            Debug.Log($"[PlayerAnimatorBridge] {parameterName}={value:F2}", this);
        }

        private void LogMovementModeChange(PlayerMovementMode mode)
        {
            if (!EnableDebugLogging)
            {
                return;
            }

            int modeValue = (int)mode;
            if (_lastLoggedMovementMode == modeValue)
            {
                return;
            }

            _lastLoggedMovementMode = modeValue;
            Debug.Log($"[PlayerAnimatorBridge] MovementMode={mode} ({modeValue})", this);
        }

        private void LogBoolParameterChange(string parameterName, bool value, ref bool lastValue, ref bool hasLastValue)
        {
            if (!EnableDebugLogging)
            {
                return;
            }

            if (hasLastValue && lastValue == value)
            {
                return;
            }

            lastValue = value;
            hasLastValue = true;
            Debug.Log($"[PlayerAnimatorBridge] {parameterName}={value}", this);
        }

        private void LogLandingTrigger(string triggerName, LandingImpactEvent evt, LandingConfig cfg, PlayerMovementState state, string reason)
        {
            if (!EnableDebugLogging)
            {
                return;
            }

            Debug.Log(
                $"[PlayerAnimatorBridge] Triggered {triggerName} ({reason}) | mode={state.Mode}, grounded={state.IsGrounded}, verticalSpeed={evt.VerticalSpeed:F2}, horizontalSpeed={evt.HorizontalSpeed:F2}, recovery={state.LandingRecoveryTime:F2}/{state.LandingRecoveryDuration:F2}, simpleFallback={cfg.UseSimpleLandingTrigger}",
                this);
        }

        private void LogAnimatorStateSnapshot(bool force = false)
        {
            if (!EnableDebugLogging || CharacterAnimator == null || !CharacterAnimator.isActiveAndEnabled)
            {
                return;
            }

            const int baseLayerIndex = 0;
            var currentState = CharacterAnimator.GetCurrentAnimatorStateInfo(baseLayerIndex);
            bool isInTransition = CharacterAnimator.IsInTransition(baseLayerIndex);
            int nextStateHash = 0;
            string nextStateName = "<none>";

            if (isInTransition)
            {
                var nextState = CharacterAnimator.GetNextAnimatorStateInfo(baseLayerIndex);
                nextStateHash = nextState.fullPathHash;
                nextStateName = ResolveStateName(nextState);
            }

            if (!force &&
                currentState.fullPathHash == _lastLoggedStateHash &&
                nextStateHash == _lastLoggedNextStateHash &&
                isInTransition == _lastLoggedInTransition)
            {
                return;
            }

            _lastLoggedStateHash = currentState.fullPathHash;
            _lastLoggedNextStateHash = nextStateHash;
            _lastLoggedInTransition = isInTransition;

            string clipSummary = GetCurrentClipSummary(baseLayerIndex);
            string transitionSuffix = isInTransition ? $", next={nextStateName}" : string.Empty;

            Debug.Log(
                $"[PlayerAnimatorBridge] Base Layer state={ResolveStateName(currentState)}, normalizedTime={currentState.normalizedTime:F2}, inTransition={isInTransition}{transitionSuffix}, clips={clipSummary}",
                this);
        }

        private string GetCurrentClipSummary(int layerIndex)
        {
            var clipInfos = CharacterAnimator.GetCurrentAnimatorClipInfo(layerIndex);
            if (clipInfos == null || clipInfos.Length == 0)
            {
                return "<none>";
            }

            int clipCount = Mathf.Min(clipInfos.Length, 2);
            var builder = new StringBuilder();
            for (int i = 0; i < clipCount; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                float roundedWeight = Mathf.Round(clipInfos[i].weight * 10f) / 10f;
                builder.Append(clipInfos[i].clip.name);
                builder.Append('@');
                builder.Append(roundedWeight.ToString("F1"));
            }

            if (clipInfos.Length > clipCount)
            {
                builder.Append(", ...");
            }

            return builder.ToString();
        }

        private static string ResolveStateName(AnimatorStateInfo stateInfo)
        {
            if (BaseLayerStateNamesByHash.TryGetValue(stateInfo.fullPathHash, out string stateName) ||
                BaseLayerStateNamesByHash.TryGetValue(stateInfo.shortNameHash, out stateName))
            {
                return stateName;
            }

            return $"hash:{stateInfo.shortNameHash}";
        }

        private string GetControllerName()
        {
            return CharacterAnimator.runtimeAnimatorController != null
                ? CharacterAnimator.runtimeAnimatorController.name
                : "<none>";
        }
    }
}
