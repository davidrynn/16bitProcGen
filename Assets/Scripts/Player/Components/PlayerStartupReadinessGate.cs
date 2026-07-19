using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct PlayerStartupReadinessGate : IComponentData
    {
        // Negative means the gate has not started tracking elapsed time yet.
        public double StartTime;
        public float TimeoutSeconds;
        public float ProbeDistance;
        public float ReleasedGravityFactor;
        // V14 meteor shell: the gate never releases before this many seconds have elapsed, even
        // if terrain is ready (or the timeout is misconfigured shorter). Lives in the gate — not
        // the overlay — so gravity release and the shell's break-open stay a single beat: the
        // managed overlay polls for this component's removal as its open signal. 0 = no hold.
        public float MinHoldSeconds;

        /// <summary>
        /// Release predicate for the startup readiness gate. The min-hold clamps BOTH release
        /// paths (terrain-ready and timeout) so the V14 meteor shell never flash-opens — the
        /// shell's break-open is keyed to this component's removal
        /// (METEOR_ARRIVAL_SEQUENCE_SPEC.md §9.2). Lives on the component (not the system) so
        /// EditMode tests can exercise the timing contract without a physics world.
        /// </summary>
        public static bool ShouldRelease(double elapsedSinceStart, float timeoutSeconds, float minHoldSeconds, bool terrainReady)
        {
            var timedOut = elapsedSinceStart >= timeoutSeconds;
            return (terrainReady || timedOut) && elapsedSinceStart >= minHoldSeconds;
        }
    }
}
