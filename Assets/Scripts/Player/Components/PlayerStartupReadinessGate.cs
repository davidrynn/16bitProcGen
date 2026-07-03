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
    }
}
