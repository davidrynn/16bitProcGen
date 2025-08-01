using System;
using UnityEngine;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Static event system for WFC test completion
    /// Allows DOTS systems to signal completion to MonoBehaviour components
    /// </summary>
    public static class WFCTestEvents
    {
        public static event Action<bool, string> OnTestCompleted;
        public static event Action<int, int> OnProgressUpdated;
        public static event Action<string> OnDebugMessage;
        
        public static void SignalTestCompleted(bool success, string message)
        {
            OnTestCompleted?.Invoke(success, message);
        }
        
        public static void SignalProgressUpdated(int current, int total)
        {
            OnProgressUpdated?.Invoke(current, total);
        }
        
        public static void SignalDebugMessage(string message)
        {
            OnDebugMessage?.Invoke(message);
        }
    }
} 