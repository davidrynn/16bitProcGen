using Unity.Entities;
using UnityEngine;

[assembly: RegisterUnityEngineComponentType(typeof(Camera))]

namespace DOTS.Player
{
    /// <summary>
    /// Ensures UnityEngine.Camera is registered as a DOTS-compatible managed component.
    /// </summary>
    internal static class PlayerComponentRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize()
        {
            // Registration is handled by the assembly attribute.
        }
    }
}
