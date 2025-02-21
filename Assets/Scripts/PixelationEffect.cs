using UnityEngine;

public class PixelationEffect : MonoBehaviour
{
    public RenderTexture lowResTexture;

    void Start()
    {
        if (lowResTexture != null)
        {
            Camera.main.targetTexture = lowResTexture;
        }
    }
}
