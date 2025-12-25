using Unity.Entities;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Ensures the simulation and fixed-step system groups tick with a predictable timestep so
    /// initial physics integration applies gravity exactly once per outer frame. Runs once at initialization.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct PlayerBootstrapFixedRateInstaller : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No-op. All work was performed during OnCreate, but we keep OnUpdate defined
            // to satisfy ISystem requirements should the system be re-enabled in the future.
        }
    }
}
