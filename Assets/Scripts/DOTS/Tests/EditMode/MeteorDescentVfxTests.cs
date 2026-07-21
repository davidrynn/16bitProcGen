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
        public void ResolveFadeStartY_RaisesAnAuthoredValueBelowSpawnHeight()
        {
            // The trap this closes: spawn height moves up, the authored band top doesn't follow,
            // and the descent silently regains a full-strength plateau before any fade starts.
            Assert.AreEqual(600f, MeteorDescentVfx.ResolveFadeStartY(400f, 600f));
            Assert.AreEqual(400f, MeteorDescentVfx.ResolveFadeStartY(340f, 400f));
        }

        [Test]
        public void ResolveFadeStartY_LeavesABandTopAboveSpawnHeightAlone()
        {
            // Authoring the band ABOVE the spawn is still allowed — it only means the fade is
            // already partway along at ignition, never that it starts late.
            Assert.AreEqual(500f, MeteorDescentVfx.ResolveFadeStartY(500f, 400f));
        }

        [Test]
        public void ResolvedBand_FadesFromTheVeryFirstFrameOfDescent()
        {
            // End-to-end on the resolved band: at the spawn altitude the burn is at full strength,
            // and any descent below it is already fading — no plateau.
            const float spawnY = 600f;
            float start = MeteorDescentVfx.ResolveFadeStartY(400f, spawnY);

            float atSpawn = MeteorDescentVfx.EvaluateIntensity(spawnY, LongAfterIgnite, start, MeteorDescentVfx.FadeEndY);
            float justBelow = MeteorDescentVfx.EvaluateIntensity(spawnY - 5f, LongAfterIgnite, start, MeteorDescentVfx.FadeEndY);

            Assert.AreEqual(1f, atSpawn, 1e-4f);
            Assert.Less(justBelow, atSpawn);
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
