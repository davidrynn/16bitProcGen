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

    public void SetVisible(bool visible)
    {
        meshRenderer.enabled = visible;
    }

    public void GenerateChunk(Vector2 chunkPos, Dictionary<TerrainType, Texture2D> terrainTextures, BiomeData biome)
    {
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        Vector3[] vertices = new Vector3[(width + 1) * (depth + 1)];
        int[] triangles = new int[width * depth * 6];
        Vector2[] uvs = new Vector2[vertices.Length];

        float totalHeight = 0f;

        //for (int z = 0; z <= depth; z++)
        //{
        //    for (int x = 0; x <= width; x++)
        //    {
        //        // Calculate GLOBAL position here, not local
        //        float globalX = chunkPos.x + x;
        //        float globalZ = chunkPos.y + z;
        //       // Vector2 worldPos = new Vector2(chunkPos.x + x, chunkPos.y + z);
        //        float y = biome.GenerateHeight(globalX, globalZ);

        //       // float y = heightMap[(int)chunkPos.x + x, (int)chunkPos.y + z];

        //        vertices[z * (width + 1) + x] = new Vector3(x, y, z);
        //        uvs[z * (width + 1) + x] = new Vector2((float)x / width, (float)z / depth);

        //        totalHeight += y;
        //    }
        //}
        //// Generate triangles
        //int trisIndex = 0;
        //for (int z = 0; z < depth; z++)
        //{
        //    for (int x = 0; x < width; x++)
        //    {
        //        int vertIndex = z * (width + 1) + x;
        //        triangles[trisIndex] = vertIndex;
        //        triangles[trisIndex + 1] = vertIndex + width + 1;
        //        triangles[trisIndex + 2] = vertIndex + 1;
        //        triangles[trisIndex + 3] = vertIndex + 1;
        //        triangles[trisIndex + 4] = vertIndex + width + 1;
        //        triangles[trisIndex + 5] = vertIndex + width + 2;
        //        trisIndex += 6;
        //    }
        //}

        for (int z = 0, i = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++, i++)
            {
                float globalX = (chunkPos.x * width) + x;
                float globalZ = (chunkPos.y * depth) + z;

                float y = biome.GenerateHeight(globalX, globalZ);

                vertices[i] = new Vector3(x, y, z);
                uvs[i] = new Vector2((float)x / width, (float)z / depth);
            }
        }

        int tris = 0, vert = 0;
        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                triangles[tris + 0] = vert;
                triangles[tris + 1] = vert + width + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + width + 1;
                triangles[tris + 5] = vert + width + 2;

                vert++;
                tris += 6;
            }
            vert++;
        }


        // Assign texture based on average height
        float avgHeight = totalHeight / vertices.Length;
        TerrainType dominantTerrainType = biome.GetTerrainType(avgHeight);

        if (terrainTextures.ContainsKey(dominantTerrainType))
        {
            meshRenderer.material.mainTexture = terrainTextures[dominantTerrainType];
        }
        else
        {
            Debug.LogWarning($"No texture found for terrain type {dominantTerrainType} for height: {avgHeight}");
        }

        // Apply mesh updates
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        // Add MeshCollider
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = mesh;
    }
}
