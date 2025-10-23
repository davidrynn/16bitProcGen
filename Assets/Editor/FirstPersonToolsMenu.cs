using UnityEditor;
using UnityEngine;

namespace DOTS.Player.Editor
{
    /// <summary>
    /// Editor menu items for first-person controller utilities.
    /// </summary>
    public static class FirstPersonToolsMenu
    {
        [MenuItem("Tools/FirstPerson/Lock Cursor", priority = 100)]
        public static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Cursor locked. Press ESC in play mode to unlock.");
        }

        [MenuItem("Tools/FirstPerson/Unlock Cursor", priority = 101)]
        public static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("Cursor unlocked.");
        }

        [MenuItem("Tools/FirstPerson/Toggle Lock", priority = 102)]
        public static void ToggleLock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }
    }
}

