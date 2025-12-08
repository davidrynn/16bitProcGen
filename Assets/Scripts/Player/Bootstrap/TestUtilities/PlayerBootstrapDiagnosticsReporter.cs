#if UNITY_EDITOR
using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Editor-only helper that surfaces the current physics snapshot in the console for diagnostics.
    /// Attach this to a bootstrap scene object to validate gravity/timestep alignment during play mode tests.
    /// </summary>
    [AddComponentMenu("DOTS Player/Player Bootstrap Diagnostics Reporter")]
    public class PlayerBootstrapDiagnosticsReporter : MonoBehaviour
    {
        [SerializeField] private bool logOnStart = true;
        [SerializeField] private bool logEachLateUpdate;

        private void Start()
        {
            if (logOnStart)
            {
                Report("Start");
            }
        }

        private void LateUpdate()
        {
            if (logEachLateUpdate)
            {
                Report("LateUpdate");
            }
        }

        private void Report(string context)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            PlayerBootstrapPhysicsUtility.Capture(world, context);
        }
    }
}
#endif
