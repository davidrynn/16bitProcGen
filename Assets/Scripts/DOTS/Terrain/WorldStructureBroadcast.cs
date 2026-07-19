using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// H2 — one-shot seeding of the <c>_WorldMacro*</c> shader globals from the <c>H</c> authority
    /// (WORLD_STRUCTURE_SPEC.md §4.2, ticket H2). Mirrors the <c>AtmosphereBroadcast</c> pattern —
    /// static, stateless, and seeded early in both player and editor contexts so Phase-B HLSL
    /// consumers (sky band, disc undulation) never read zeroed globals.
    ///
    /// <para><b>No per-frame broadcast.</b> Unlike the atmosphere authority, <c>H</c> is static per
    /// world (§4.2/§6.6) — these globals are pushed once at bootstrap and never again. If the world
    /// seed or settings change at runtime (dev tooling), call <see cref="PushFromSettings"/> or
    /// <see cref="Push"/> explicitly; do not add an update loop.</para>
    /// </summary>
    public static class WorldStructureBroadcast
    {
        /// <summary>Resources path of the tunable settings asset (Assets/Resources/…).</summary>
        public const string SettingsResourcePath = "WorldStructureSettings";

        private static readonly int FreqId       = Shader.PropertyToID("_WorldMacroFreq");
        private static readonly int SeedOffsetId = Shader.PropertyToID("_WorldMacroSeedOffset");
        private static readonly int OctavesId    = Shader.PropertyToID("_WorldMacroOctaves");
        private static readonly int LacunarityId = Shader.PropertyToID("_WorldMacroLacunarity");
        private static readonly int GainId       = Shader.PropertyToID("_WorldMacroGain");
        private static readonly int ANearId      = Shader.PropertyToID("_WorldMacroANear");
        private static readonly int AFarId       = Shader.PropertyToID("_WorldMacroAFar");
        private static readonly int RampStartId  = Shader.PropertyToID("_WorldMacroRampStart");
        private static readonly int RampEndId    = Shader.PropertyToID("_WorldMacroRampEnd");

        /// <summary>Push a constants snapshot to the <c>_WorldMacro*</c> globals.</summary>
        public static void Push(in WorldStructureConstants c)
        {
            Shader.SetGlobalFloat(FreqId, c.MacroFreq);
            Shader.SetGlobalVector(SeedOffsetId, new Vector4(c.SeedOffset.x, c.SeedOffset.y, 0f, 0f));
            Shader.SetGlobalInteger(OctavesId, c.Octaves);
            Shader.SetGlobalFloat(LacunarityId, c.Lacunarity);
            Shader.SetGlobalFloat(GainId, c.Gain);
            Shader.SetGlobalFloat(ANearId, c.ANear);
            Shader.SetGlobalFloat(AFarId, c.AFar);
            Shader.SetGlobalFloat(RampStartId, c.RampStart);
            Shader.SetGlobalFloat(RampEndId, c.RampEnd);
        }

        /// <summary>
        /// Load the settings asset from Resources and seed the globals; falls back to
        /// <see cref="WorldStructureSettings.DefaultConstants"/> when the asset is absent (fresh
        /// clone before it is authored, or a test scene) so the globals are never left at zero — a
        /// zero ramp span would otherwise divide-by-guard to a flat field and a zero octave count
        /// would read <c>H = 0</c>.
        /// </summary>
        public static void PushFromSettings()
        {
            var settings = Resources.Load<WorldStructureSettings>(SettingsResourcePath);
            Push(settings != null ? settings.ToConstants() : WorldStructureSettings.DefaultConstants);
        }

        // Globals are zero in a fresh session until someone sets them; seed sane defaults early in
        // both contexts (the AtmosphereBroadcast convention), before any consumer shader samples them.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PushRuntimeDefaults() => PushFromSettings();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PushEditorDefaults() => PushFromSettings();
#endif
    }
}
