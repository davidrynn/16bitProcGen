using NUnit.Framework;
using DOTS.Structures;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// R6 P4 contract (LANDMARK_DRAW_DISTANCE_SPEC.md §P4): the spawn fade
    /// progresses from 0 to fully visible over ~0.5s and clamps at 1. Tests the
    /// pure static step (same no-World pattern as
    /// RelicRealizationSystem.TemplateParticipatesInLod); the shader-side dither
    /// and the BRG property plumbing are visual, validated in Play Mode.
    /// </summary>
    [TestFixture]
    public class RelicSpawnFadeTests
    {
        [Test]
        public void Advance_ReachesFullVisibility_WithinSpawnFadeDuration()
        {
            // Simulate ~60fps frames; after the spec duration the relic must be solid.
            const float frame = 1f / 60f;
            float fade = 0f;
            float elapsed = 0f;
            while (elapsed < RelicSpawnFadeSystem.SpawnFadeSeconds)
            {
                fade = RelicSpawnFadeSystem.Advance(fade, frame);
                elapsed += frame;
            }

            Assert.AreEqual(1f, fade, 1e-4f,
                "Spawn fade must reach full visibility after SpawnFadeSeconds of updates.");
        }

        [Test]
        public void Advance_ClampsAtFullVisibility()
        {
            // A long hitch frame (e.g. a GC spike right after realization) must not
            // overshoot 1 — the shader clips against visibility, so >1 would be
            // harmless today but breaks the "finished fades stay exactly 1" skip
            // in RelicSpawnFadeSystem.
            Assert.AreEqual(1f, RelicSpawnFadeSystem.Advance(0.9f, 10f));
            Assert.AreEqual(1f, RelicSpawnFadeSystem.Advance(1f, 0.016f));
        }

        [Test]
        public void Advance_PartialStep_IsProportional()
        {
            // Half the duration in → half faded (linear ramp, no easing surprise).
            float fade = RelicSpawnFadeSystem.Advance(0f, RelicSpawnFadeSystem.SpawnFadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, fade, 1e-4f);
        }
    }
}
