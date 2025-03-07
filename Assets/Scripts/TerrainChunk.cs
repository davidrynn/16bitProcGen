﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TerrainChunk : MonoBehaviour
{
    public int width = 16;
    public int depth = 16;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3 pos = transform.position;
        Gizmos.DrawWireCube(
            new Vector3(pos.x + width / 2f, 0, pos.z + depth / 2f),
            new Vector3(width, 0, depth)
        );
    }

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

        // Generate mesh data
        Vector3[] vertices;
        Vector2[] uvs;
        GenerateVerticesAndUVs(chunkPos, biome, out vertices, out uvs);

        int[] triangles = GenerateTriangles();

        // ✅ Debug: Check if mesh data is valid
        if (vertices.Length == 0 || triangles.Length == 0)
        {
            Debug.LogError($"[ERROR] Chunk at {chunkPos} failed: No vertices or triangles!");
            return; // Stop execution if mesh is invalid
        }

        // Apply mesh data
        ApplyMeshData(vertices, triangles, uvs);

        // Assign texture
        AssignTerrainTexture(vertices, terrainTextures, biome);
    }


    //private void GenerateVerticesAndUVs(Vector2 chunkPos, BiomeData biome, out Vector3[] vertices, out Vector2[] uvs)
    //{
    //    vertices = new Vector3[(width + 1) * (depth + 1)];
    //    uvs = new Vector2[vertices.Length];

    //    float totalHeight = 0f;
    //    BiomeType? previousBiome = null;

    //    for (int z = 0, i = 0; z <= depth; z++)
    //    {
    //        for (int x = 0; x <= width; x++, i++)
    //        {
    //            // ✅ Correct global position calculation
    //            float globalX = chunkPos.x * width + x;
    //            float globalZ = chunkPos.y * depth + z;
    //            Vector2 worldPos = new Vector2(globalX, globalZ);

    //            TerrainManager terrainManager = TerrainManager.Instance;
    //            var biomeWeights = terrainManager.DetermineBiomeWeights(worldPos);

    //            // Ensure biomeWeights is not empty
    //            if (biomeWeights.Count == 0)
    //            {
    //                biomeWeights.Add(terrainManager.defaultBiome, 1.0f);
    //            }

    //            // Determine the strongest biome
    //            BiomeData strongestBiome = null;
    //            float highestWeight = 0f;
    //            foreach (var pair in biomeWeights)
    //            {
    //                if (pair.Value > highestWeight)
    //                {
    //                    highestWeight = pair.Value;
    //                    strongestBiome = pair.Key;
    //                }
    //            }

    //            // Debug biome transition
    //            if (strongestBiome != null && strongestBiome.biomeType != previousBiome)
    //            {
    //                Debug.Log($"Biome changed to {strongestBiome.biomeType} at world position ({globalX}, {globalZ})");
    //                previousBiome = strongestBiome.biomeType;
    //            }

    //            // Blend height based on biome weights
    //            float blendedHeight = 0f;
    //            foreach (var pair in biomeWeights)
    //            {
    //                blendedHeight += pair.Key.GenerateHeight(globalX, globalZ) * pair.Value;
    //                Debug.Log("blended height is" + blendedHeight);
    //            }

    //            totalHeight += blendedHeight;

    //            vertices[i] = new Vector3(x, blendedHeight, z);
    //            uvs[i] = new Vector2((float)x / width, (float)z / depth);
    //        }
    //    }
    //}
    private void GenerateVerticesAndUVs(Vector2 chunkPos, BiomeData biome, out Vector3[] vertices, out Vector2[] uvs)
    {
        vertices = new Vector3[(width + 1) * (depth + 1)];
        uvs = new Vector2[vertices.Length];

        for (int z = 0, i = 0; z <= depth; z++)
        {
            for (int x = 0; x <= width; x++, i++)
            {
                float globalX = chunkPos.x + x;
                float globalZ = chunkPos.y + z;
                float height = biome.GenerateHeight(globalX, globalZ);

                vertices[i] = new Vector3(x, height, z);
                uvs[i] = new Vector2((float)x / width, (float)z / depth);
            }
        }
    }

    private int[] GenerateTriangles()
    {
        int[] triangles = new int[width * depth * 6];
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
            vert++; // Correcting vertex offset for next row
        }

        return triangles;
    }

    private void ApplyMeshData(Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = mesh;
    }

    private void AssignTerrainTexture(Vector3[] vertices, Dictionary<TerrainType, Texture2D> terrainTextures, BiomeData biome)
    {
        float totalHeight = 0f;
        foreach (var vertex in vertices)
        {
            totalHeight += vertex.y;
        }

        float avgHeight = totalHeight / Mathf.Max(1, vertices.Length);
        TerrainType dominantTerrainType = biome.GetTerrainType(avgHeight);

        if (terrainTextures.ContainsKey(dominantTerrainType))
        {
              Material mat = meshRenderer.material;
        mat.SetTexture("_BaseMap", terrainTextures[dominantTerrainType]); // ✅ Try this for URP Shader
        mat.SetTexture("_MainTex", terrainTextures[dominantTerrainType]); // ✅ Standard Shader

        }
        else
        {
            Debug.LogWarning($"No texture found for terrain type {dominantTerrainType} for height: {avgHeight}");
            meshRenderer.material.mainTexture = terrainTextures[TerrainType.Default];
        }
    }


}
