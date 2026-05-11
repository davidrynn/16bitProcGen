using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for camera effects, set once at bootstrap.
    /// </summary>
    public struct CameraEffectConfig : IComponentData
    {
        public float BaseFOV;                // 60
        public float BaseDistance;           // 4.0
        public float3 BasePivotOffset;      // (0, 1.5, 0)

        // Slingshot charge
        public float ChargeDistanceAdd;     // 2.5
        public float ChargeFOVReduce;       // 5
        public float ChargeShakeMin;        // 0.01
        public float ChargeShakeMax;        // 0.06

        // Ballistic
        public float LaunchFOVPunch;        // 12
        public float LaunchFOVDecayRate;    // 3.0 (per second)
        public float SpeedFOVScale;         // 0.15
        public float SpeedFOVThreshold;     // 15
        public float SpeedFOVMax;           // 12
        public float BallisticDistanceAdd;  // 1.5

        // Glide
        public float GlideFOVAdd;           // 3
        public float GlideDistanceAdd;      // 0.5

        // Thermal
        public float ThermalFOVAdd;         // 4

        // Speed shake (continuous, airborne)
        public float ShakeSpeedThreshold;   // 15 m/s — below this, no shake
        public float ShakeAmplitudeScale;   // amplitude per m/s above threshold
        public float ShakeAmplitudeMax;     // clamp on amplitude
        public float ShakeFrequency;        // base oscillation Hz
        public float ShakeFrequencyScale;   // additional Hz per m/s above threshold

        // Landing
        public float LandingShakeScale;     // 0.01 per m/s
        public float LandingShakeMax;       // 0.20
        public float LandingFOVDip;         // 3
        public float LandingCameraDipMax;   // 0.8

        // Damping per state
        public float GroundedDamping;       // 12
        public float ChargeDamping;         // 8
        public float BallisticDamping;      // 6
        public float GlideDamping;          // 14
        public float ThermalDamping;        // 10

        public static CameraEffectConfig Default => new CameraEffectConfig
        {
            BaseFOV = 60f,
            BaseDistance = 4.0f,
            BasePivotOffset = new float3(0f, 1.5f, 0f),
            ChargeDistanceAdd = 2.5f,
            ChargeFOVReduce = 5f,
            ChargeShakeMin = 0.01f,
            ChargeShakeMax = 0.06f,
            LaunchFOVPunch = 12f,
            LaunchFOVDecayRate = 3.0f,
            SpeedFOVScale = 0.15f,
            SpeedFOVThreshold = 15f,
            SpeedFOVMax = 12f,
            BallisticDistanceAdd = 1.5f,
            GlideFOVAdd = 3f,
            GlideDistanceAdd = 0.5f,
            ThermalFOVAdd = 4f,
            ShakeSpeedThreshold = 15f,
            ShakeAmplitudeScale = 0.0012f,
            ShakeAmplitudeMax = 0.06f,
            ShakeFrequency = 18f,
            ShakeFrequencyScale = 0.15f,
            LandingShakeScale = 0.01f,
            LandingShakeMax = 0.20f,
            LandingFOVDip = 3f,
            LandingCameraDipMax = 0.8f,
            GroundedDamping = 12f,
            ChargeDamping = 8f,
            BallisticDamping = 6f,
            GlideDamping = 14f,
            ThermalDamping = 10f
        };
    }
}
