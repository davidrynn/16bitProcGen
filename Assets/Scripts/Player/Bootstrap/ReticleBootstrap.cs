using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DOTS.Terrain;
using DOTS.Terrain.Core;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Creates and manages a center-screen reticle that binds to the active main camera.
    /// Auto-instantiates at runtime so no scene wiring is required.
    /// </summary>
    public static class ReticleBootstrap
    {
        private const string RootName = "ReticleRoot";
        private const string CanvasName = "ReticleCanvas";
        private const string DotName = "ReticleDot";
        private const float DotSizePixels = 4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureReticleInstalled()
        {
            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            root.AddComponent<ReticleController>();
        }

        private sealed class ReticleController : MonoBehaviour
        {
            private Canvas _canvas;
            private Camera _boundCamera;
            private bool _warnedMissingCamera;
            private Sprite _dotSprite;
            private Texture2D _dotTexture;

            private void Awake()
            {
                BuildUiIfNeeded();
                SceneManager.sceneLoaded += OnSceneLoaded;
                TryBindToMainCamera();
            }

            private void OnDestroy()
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;

                if (_dotSprite != null)
                {
                    Destroy(_dotSprite);
                    _dotSprite = null;
                }

                if (_dotTexture != null)
                {
                    Destroy(_dotTexture);
                    _dotTexture = null;
                }
            }

            private void LateUpdate()
            {
                BuildUiIfNeeded();
                var currentMain = Camera.main;
                if (currentMain != _boundCamera)
                {
                    TryBindToMainCamera();
                }
            }

            private void OnSceneLoaded(Scene _, LoadSceneMode __)
            {
                BuildUiIfNeeded();
                TryBindToMainCamera();
            }

            private void BuildUiIfNeeded()
            {
                if (_canvas != null)
                {
                    return;
                }

                var canvasGO = new GameObject(CanvasName);
                canvasGO.transform.SetParent(transform, false);

                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 500;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var dotGO = new GameObject(DotName);
                dotGO.transform.SetParent(canvasGO.transform, false);

                var image = dotGO.AddComponent<Image>();
                _dotSprite = CreateCircleSprite(out _dotTexture);
                image.sprite = _dotSprite;
                image.color = new Color(1f, 1f, 1f, 0.9f);
                image.raycastTarget = false;

                var rect = image.rectTransform;
                rect.anchorMin = CenterAimRayUtility.CenterViewport;
                rect.anchorMax = CenterAimRayUtility.CenterViewport;
                rect.pivot = CenterAimRayUtility.CenterViewport;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(DotSizePixels, DotSizePixels);

                var outline = dotGO.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            private void TryBindToMainCamera()
            {
                if (_canvas == null)
                {
                    return;
                }

                var main = Camera.main;
                if (main == null)
                {
                    _boundCamera = null;
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _canvas.worldCamera = null;
                    if (_canvas.transform.parent != transform)
                    {
                        _canvas.transform.SetParent(transform, false);
                    }
                    if (!_warnedMissingCamera)
                    {
                        DebugSettings.LogPlayerWarning("ReticleBootstrap: MainCamera not found yet. Using ScreenSpaceOverlay fallback.");
                        _warnedMissingCamera = true;
                    }
                    return;
                }

                _warnedMissingCamera = false;
                _boundCamera = main;
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = main;
                _canvas.planeDistance = Mathf.Max(main.nearClipPlane + 0.01f, 0.1f);
                if (_canvas.transform.parent != main.transform)
                {
                    _canvas.transform.SetParent(main.transform, false);
                }
                DebugSettings.LogPlayer($"ReticleBootstrap: Bound to camera '{main.name}' (instanceID={main.GetInstanceID()}) pos={main.transform.position} depth={main.depth}");
                var allCams = Camera.allCameras;
                if (allCams.Length > 1)
                    DebugSettings.LogPlayerWarning($"ReticleBootstrap: {allCams.Length} cameras exist — reticle may be on wrong camera.");
            }
        }

        private static Sprite CreateCircleSprite(out Texture2D texture)
        {
            const int size = 16;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
            var radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = dist <= radius - 1f ? (byte)255 :
                                 dist <= radius ? (byte)(255 * (radius - dist)) :
                                 (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
