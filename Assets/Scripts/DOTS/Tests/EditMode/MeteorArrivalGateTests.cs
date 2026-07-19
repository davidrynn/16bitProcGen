using NUnit.Framework;
using DOTS.Player.Components;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// V14 gate-timing contract (METEOR_ARRIVAL_SEQUENCE_SPEC.md §9.2): the startup readiness
    /// gate — whose removal is the meteor shell's break-open signal — never releases before the
    /// minimum hold elapses, on EITHER release path (terrain-ready or timeout). Tests the pure
    /// predicate on the component (same no-World pattern as RelicSpawnFadeTests); the overlay
    /// visuals and the gravity-release beat are Play Mode / eyeball items.
    /// </summary>
    [TestFixture]
    public class MeteorArrivalGateTests
    {
        private const float Timeout = 8f;
        private const float MinHold = 1.75f;

        [Test]
        public void TerrainReadyBeforeMinHold_DoesNotRelease()
        {
            // Fast load: colliders ready almost immediately — the shell must not flash-open.
            Assert.IsFalse(PlayerStartupReadinessGate.ShouldRelease(0.5, Timeout, MinHold, terrainReady: true));
        }

        [Test]
        public void TerrainReadyAfterMinHold_Releases()
        {
            Assert.IsTrue(PlayerStartupReadinessGate.ShouldRelease(MinHold, Timeout, MinHold, terrainReady: true));
        }

        [Test]
        public void NotReadyAfterMinHold_KeepsHolding()
        {
            // Min-hold elapsed but the world isn't ready — the gate still waits on readiness.
            Assert.IsFalse(PlayerStartupReadinessGate.ShouldRelease(3.0, Timeout, MinHold, terrainReady: false));
        }

        [Test]
        public void TimeoutPath_Releases_WithoutTerrainReady()
        {
            // The 8s fallback inherited from V7 — the shell always opens eventually.
            Assert.IsTrue(PlayerStartupReadinessGate.ShouldRelease(Timeout, Timeout, MinHold, terrainReady: false));
        }

        [Test]
        public void TimeoutShorterThanMinHold_StillWaitsForMinHold()
        {
            // Misconfiguration guard: even a timeout shorter than the min-hold must not
            // flash-open the shell — the min-hold clamps both release paths.
            Assert.IsFalse(PlayerStartupReadinessGate.ShouldRelease(1.0, timeoutSeconds: 0.5f, minHoldSeconds: MinHold, terrainReady: false));
            Assert.IsTrue(PlayerStartupReadinessGate.ShouldRelease(MinHold, timeoutSeconds: 0.5f, minHoldSeconds: MinHold, terrainReady: false));
        }

        [Test]
        public void ZeroMinHold_BehavesAsPreV14Gate()
        {
            // Shell disabled (or ground-level spawn) → MinHoldSeconds = 0: timing must be
            // byte-identical to the original V7 gate.
            Assert.IsTrue(PlayerStartupReadinessGate.ShouldRelease(0.0, Timeout, 0f, terrainReady: true));
            Assert.IsFalse(PlayerStartupReadinessGate.ShouldRelease(0.0, Timeout, 0f, terrainReady: false));
            Assert.IsTrue(PlayerStartupReadinessGate.ShouldRelease(Timeout, Timeout, 0f, terrainReady: false));
        }
    }
}
