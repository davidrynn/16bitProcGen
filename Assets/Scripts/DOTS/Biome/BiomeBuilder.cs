using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Factory class for creating BiomeComponent and related structures
/// Provides methods for initializing biome data with proper memory management
/// </summary>
public static class BiomeBuilder
{
    /// <summary>
    /// Creates a BiomeComponent with default settings for a specific biome type
    /// </summary>
    /// <param name="biomeType">Type of biome to create</param>
    /// <returns>Fully initialized BiomeComponent</returns>
    public static BiomeComponent CreateBiomeComponent(BiomeType biomeType)
    {
        var biomeComponent = new BiomeComponent
        {
            biomeType = biomeType,
            biomeScale = GetDefaultBiomeScale(biomeType),
            noiseType = GetDefaultNoiseType(biomeType),
            noiseScale = GetDefaultNoiseScale(biomeType),
            heightMultiplier = GetDefaultHeightMultiplier(biomeType),
            noiseOffset = float2.zero,
            terrainData = CreateBiomeTerrainData(biomeType)
        };

        return biomeComponent;
    }

    /// <summary>
    /// Creates terrain data specific to a biome type
    /// </summary>
    /// <param name="biomeType">Type of biome</param>
    /// <returns>BlobAssetReference to BiomeTerrainData</returns>
    public static BlobAssetReference<BiomeTerrainData> CreateBiomeTerrainData(BiomeType biomeType)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<BiomeTerrainData>();
        
        // Get terrain probabilities for this biome
        var terrainChances = GetTerrainProbabilities(biomeType);
        var chances = builder.Allocate(ref root.terrainChances, terrainChances.Length);
        
        // Copy terrain probabilities
        for (int i = 0; i < terrainChances.Length; i++)
        {
            chances[i] = terrainChances[i];
        }
        
        var result = builder.CreateBlobAssetReference<BiomeTerrainData>(Allocator.Persistent);
        
        // Ensure builder is disposed even if an exception occurs
        try
        {
            builder.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BiomeBuilder: Error disposing BlobBuilder: {e.Message}");
        }
        
        return result;
    }

    /// <summary>
    /// Gets default biome scale for a biome type
    /// </summary>
    private static float GetDefaultBiomeScale(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains: return 1.0f;
            case BiomeType.Forest: return 0.8f;
            case BiomeType.Mountains: return 1.5f;
            case BiomeType.Desert: return 1.2f;
            case BiomeType.Ocean: return 0.5f;
            case BiomeType.Arctic: return 1.3f;
            case BiomeType.Volcanic: return 1.1f;
            case BiomeType.Swamp: return 0.9f;
            case BiomeType.Crystalline: return 1.4f;
            case BiomeType.Alien: return 2.0f;
            default: return 1.0f;
        }
    }

    /// <summary>
    /// Gets default noise type for a biome type
    /// </summary>
    private static NoiseType GetDefaultNoiseType(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains: return NoiseType.Perlin;
            case BiomeType.Forest: return NoiseType.Perlin;
            case BiomeType.Mountains: return NoiseType.Simplex;
            case BiomeType.Desert: return NoiseType.Cellular;
            case BiomeType.Ocean: return NoiseType.Perlin;
            case BiomeType.Arctic: return NoiseType.Simplex;
            case BiomeType.Volcanic: return NoiseType.Cellular;
            case BiomeType.Swamp: return NoiseType.Perlin;
            case BiomeType.Crystalline: return NoiseType.Cellular;
            case BiomeType.Alien: return NoiseType.Simplex;
            default: return NoiseType.Perlin;
        }
    }

    /// <summary>
    /// Gets default noise scale for a biome type
    /// </summary>
    private static float GetDefaultNoiseScale(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains: return 0.01f;
            case BiomeType.Forest: return 0.015f;
            case BiomeType.Mountains: return 0.008f;
            case BiomeType.Desert: return 0.012f;
            case BiomeType.Ocean: return 0.02f;
            case BiomeType.Arctic: return 0.01f;
            case BiomeType.Volcanic: return 0.009f;
            case BiomeType.Swamp: return 0.014f;
            case BiomeType.Crystalline: return 0.007f;
            case BiomeType.Alien: return 0.005f;
            default: return 0.01f;
        }
    }

    /// <summary>
    /// Gets default height multiplier for a biome type
    /// </summary>
    private static float GetDefaultHeightMultiplier(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains: return 50f;
            case BiomeType.Forest: return 80f;
            case BiomeType.Mountains: return 200f;
            case BiomeType.Desert: return 60f;
            case BiomeType.Ocean: return 20f;
            case BiomeType.Arctic: return 100f;
            case BiomeType.Volcanic: return 150f;
            case BiomeType.Swamp: return 40f;
            case BiomeType.Crystalline: return 120f;
            case BiomeType.Alien: return 300f;
            default: return 50f;
        }
    }

    /// <summary>
    /// Gets terrain probabilities for a biome type
    /// </summary>
    private static TerrainProbability[] GetTerrainProbabilities(BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Grass, minHeight = 0.3f, maxHeight = 0.7f, probability = 0.8f },
                    new TerrainProbability { terrainType = TerrainType.Sand, minHeight = 0.2f, maxHeight = 0.4f, probability = 0.2f }
                };
            
            case BiomeType.Forest:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Grass, minHeight = 0.4f, maxHeight = 0.8f, probability = 0.7f },
                    new TerrainProbability { terrainType = TerrainType.Rock, minHeight = 0.6f, maxHeight = 0.9f, probability = 0.3f }
                };
            
            case BiomeType.Mountains:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Rock, minHeight = 0.6f, maxHeight = 1.0f, probability = 0.8f },
                    new TerrainProbability { terrainType = TerrainType.Snow, minHeight = 0.8f, maxHeight = 1.0f, probability = 0.2f }
                };
            
            case BiomeType.Desert:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Sand, minHeight = 0.2f, maxHeight = 0.6f, probability = 0.9f },
                    new TerrainProbability { terrainType = TerrainType.Rock, minHeight = 0.5f, maxHeight = 0.7f, probability = 0.1f }
                };
            
            case BiomeType.Ocean:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Water, minHeight = 0.0f, maxHeight = 0.3f, probability = 1.0f }
                };
            
            case BiomeType.Arctic:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Snow, minHeight = 0.3f, maxHeight = 0.8f, probability = 0.8f },
                    new TerrainProbability { terrainType = TerrainType.Ice, minHeight = 0.0f, maxHeight = 0.4f, probability = 0.2f }
                };
            
            case BiomeType.Volcanic:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Rock, minHeight = 0.5f, maxHeight = 0.9f, probability = 0.7f },
                    new TerrainProbability { terrainType = TerrainType.Lava, minHeight = 0.0f, maxHeight = 0.3f, probability = 0.3f }
                };
            
            case BiomeType.Swamp:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Water, minHeight = 0.0f, maxHeight = 0.4f, probability = 0.4f },
                    new TerrainProbability { terrainType = TerrainType.Grass, minHeight = 0.2f, maxHeight = 0.5f, probability = 0.6f }
                };
            
            case BiomeType.Crystalline:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Crystal, minHeight = 0.4f, maxHeight = 0.8f, probability = 0.6f },
                    new TerrainProbability { terrainType = TerrainType.Rock, minHeight = 0.6f, maxHeight = 0.9f, probability = 0.4f }
                };
            
            case BiomeType.Alien:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Alien, minHeight = 0.2f, maxHeight = 0.8f, probability = 0.8f },
                    new TerrainProbability { terrainType = TerrainType.Crystal, minHeight = 0.5f, maxHeight = 0.9f, probability = 0.2f }
                };
            
            default:
                return new TerrainProbability[]
                {
                    new TerrainProbability { terrainType = TerrainType.Grass, minHeight = 0.3f, maxHeight = 0.7f, probability = 1.0f }
                };
        }
    }
} 