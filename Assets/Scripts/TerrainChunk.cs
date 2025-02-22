using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk : MonoBehaviour
{
    public int width = 16;
    public int depth = 16;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void GenerateChunk(Vector2 chunkPos, float[,] heightMap, Dictionary<TerrainType, Texture2D> terrainTextures, TerrainType[] possibleTerrains)
    {
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        Vector3[] vertices = new Vector3[(width + 1) * (depth + 1)];
        int[] triangles = new int[width * depth * 6];
        Vector2[] uvs = new Vector2[vertices.Length];

        TerrainType dominantTerrain = possibleTerrains[Random.Range(0, possibleTerrains.Length)];

        for (int z = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++)
            {
                float y = heightMap[(int)chunkPos.x + x, (int)chunkPos.y + z];
                vertices[z * (width + 1) + x] = new Vector3(x, y, z);
                uvs[z * (width + 1) + x] = new Vector2((float)x / width, (float)z / depth);
            }
        }

        int trisIndex = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int vertIndex = z * (width + 1) + x;
                triangles[trisIndex] = vertIndex;
                triangles[trisIndex + 1] = vertIndex + width + 1;
                triangles[trisIndex + 2] = vertIndex + 1;
                triangles[trisIndex + 3] = vertIndex + 1;
                triangles[trisIndex + 4] = vertIndex + width + 1;
                triangles[trisIndex + 5] = vertIndex + width + 2;
                trisIndex += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        // Assign material with correct texture
        if (meshRenderer.material == null)
        {
            meshRenderer.material = new Material(Shader.Find("Standard"));
        }
        meshRenderer.material.mainTexture = terrainTextures[dominantTerrain];

        // Add MeshCollider
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = mesh;
    }
}
