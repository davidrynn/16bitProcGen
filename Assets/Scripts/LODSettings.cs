using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LODLevel
{
    public string name = "LOD Level";
    public float distance = 10f;
    public int meshResolution = 16;
    public bool useCollider = true;
    public bool useShadows = true;
    public Material material;
    [Range(0f, 1f)]
    public float textureQuality = 1f;
}

[CreateAssetMenu(fileName = "LODSettings", menuName = "Terrain/LOD Settings")]
public class LODSettings : ScriptableObject
{
    public LODLevel[] lodLevels = new LODLevel[]
    {
        new LODLevel { name = "High", distance = 300f, meshResolution = 16, useCollider = true, useShadows = true, textureQuality = 1f },
        new LODLevel { name = "Medium", distance = 800f, meshResolution = 8, useCollider = true, useShadows = false, textureQuality = 0.5f },
        new LODLevel { name = "Low", distance = 1500f, meshResolution = 4, useCollider = true, useShadows = false, textureQuality = 0.25f },
        new LODLevel { name = "Ultra Low", distance = 3000f, meshResolution = 2, useCollider = true, useShadows = false, textureQuality = 0.1f }
    };

    public float updateInterval = 0.5f;
    public bool enableLOD = true;
    public bool showLODGizmos = false;
} 