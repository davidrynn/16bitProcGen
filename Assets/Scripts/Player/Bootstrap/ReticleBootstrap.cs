using UnityEngine;
using UnityEngine.UI;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Creates a small center-screen reticle dot at runtime.
    /// Lightweight: no Update loop, static ScreenSpaceOverlay canvas.
    /// </summary>
    public class ReticleBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var canvasGO = new GameObject("ReticleCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();

            var dotGO = new GameObject("ReticleDot");
            dotGO.transform.SetParent(canvasGO.transform, false);

            var image = dotGO.AddComponent<Image>();
            image.sprite = CreateCircleSprite();
            image.color = new Color(1f, 1f, 1f, 0.6f);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(8f, 8f);
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
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
