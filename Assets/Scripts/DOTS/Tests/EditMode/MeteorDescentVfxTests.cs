using NUnit.Framework;
using DOTS.Player.Bootstrap;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// V13 burn-envelope contract (METEOR_ARRIVAL_SEQUENCE_SPEC.md §5.2/§9.5): flames ramp in
    /// at ignition, then fade across the altitude band and are fully extinguished below it —
    /// well before landing. The band top now sits at the spawn altitude (owner call 2026-07-21),
    /// so full strength is reached only at the very top of the descent and the burn-off begins
    /// immediately; the assertions below still pin peak intensity at Y=400 because that IS the
    /// band top. Pure-function tests (same no-World pattern as MeteorArrivalGateTests); the
    /// screen-space visuals are Play Mode / eyeball items.
    /// </summary>
    [TestFixture]
    public class MeteorDescentVfxTests
    {
        private const float LongAfterIgnite = 10f;

        [Test]
        public void AtIgnition_StartsFromZero()
        {
            Assert.AreEqual(0f, MeteorDescentVfx.EvaluateIntensity(400f, 0f));
        }

        [Test]
        public void AfterRamp_AtBandTop_BurnsAtFullStrength()
        {
            Assert.AreEqual(1f, MeteorDescentVfx.EvaluateIntensity(400f, LongAfterIgnite), 1e-4f);
            Assert.AreEqual(1f, MeteorDescentVfx.EvaluateIntensity(MeteorDescentVfx.FadeStartY, LongAfterIgnite), 1e-4f);
        }

        [Test]
        public void InsideFadeBand_IntensityIsPartial()
        {
            float mid = (MeteorDescentVfx.FadeStartY + MeteorDescentVfx.FadeEndY) * 0.5f;
            float intensity = MeteorDescentVfx.EvaluateIntensity(mid, LongAfterIgnite);
            Assert.Greater(intensity, 0f);
            Assert.Less(intensity, 1f);
        }

        [Test]
        public void BelowFadeBand_FullyExtinguished_BeforeLanding()
        {
            // §9.5: fully extinguished before landing — zero at the band floor and below,
            // with margin above the ground plane (terrain sits near Y=0).
            Assert.AreEqual(0f, MeteorDescentVfx.EvaluateIntensity(MeteorDescentVfx.FadeEndY, LongAfterIgnite));
            Assert.AreEqual(0f, MeteorDescentVfx.EvaluateIntensity(50f, LongAfterIgnite));
            Assert.AreEqual(0f, MeteorDescentVfx.EvaluateIntensity(0f, LongAfterIgnite));
        }

        [Test]
        public void RampIn_IsFastButNotInstant()
        {
            float halfRamp = MeteorDescentVfx.EvaluateIntensity(400f, MeteorDescentVfx.IgniteRampSeconds * 0.5f);
            Assert.Greater(halfRamp, 0f);
            Assert.Less(halfRamp, 1f);
            Assert.AreEqual(1f, MeteorDescentVfx.EvaluateIntensity(400f, MeteorDescentVfx.IgniteRampSeconds), 1e-4f);
        }
    }
}
