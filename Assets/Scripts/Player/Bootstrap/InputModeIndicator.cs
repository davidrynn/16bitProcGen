using Unity.Entities;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Minimal OnGUI overlay that shows the current input mode (MOVE / EDIT).
    /// Auto-installs at runtime via RuntimeInitializeOnLoadMethod.
    /// Temporary prototype indicator — replace with proper HUD when available.
    /// </summary>
    public static class InputModeIndicator
    {
        private const string RootName = "InputModeIndicatorRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (GameObject.Find(RootName) != null)
                return;

            var go = new GameObject(RootName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Indicator>();
        }

        private sealed class Indicator : MonoBehaviour
        {
            private GUIStyle _style;
            private bool _isEditMode;

            private void LateUpdate()
            {
                // Read edit mode flag from the player entity each frame
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    return;

                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(typeof(PlayerInputComponent));
                if (query.IsEmpty)
                    return;

                var input = query.GetSingleton<PlayerInputComponent>();
                _isEditMode = input.IsEditMode;
            }

            private void OnGUI()
            {
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperLeft,
                        normal = { textColor = Color.white }
                    };
                }

                string label = _isEditMode ? "EDIT" : "MOVE";
                Color bg = _isEditMode ? new Color(0.8f, 0.5f, 0.1f, 0.7f) : new Color(0.1f, 0.5f, 0.8f, 0.7f);

                var rect = new Rect(10, 10, 70, 28);
                GUI.color = bg;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(14, 12, 66, 24), label, _style);
            }
        }
    }
}
