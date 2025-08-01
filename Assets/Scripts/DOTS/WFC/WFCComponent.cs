using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Domain identifier for pattern types (expand as needed)
    /// </summary>
    public enum PatternDomain : byte
    {
        Dungeon = 0,
        Tree = 1,
        Enemy = 2,
        Terrain = 3 // Added for terrain patterns
    }

    /// <summary>
    /// Dungeon-specific pattern type
    /// </summary>
    public enum DungeonPatternType : int
    {
        Floor = 0,
        Wall = 1,
        Door = 2,
        Corridor = 3,
        Corner = 4
    }

    /// <summary>
    /// Domain-specific pattern struct for dungeons
    /// </summary>
    public struct DungeonPattern
    {
        public int id;
        public FixedString32Bytes name;
        public DungeonPatternType type;
        public byte north, east, south, west; // 'F' or 'W'
        public float weight;
    }

    /// <summary>
    /// Core WFC pattern struct (used by ECS system, supports all domains)
    /// </summary>
    public struct WFCPattern : IComponentData
    {
        public int patternId;         // Unique pattern ID
        public float weight;          // Pattern selection weight
        public PatternDomain domain;  // Domain (dungeon, tree, etc.)
        public int type;              // Domain-specific type (e.g., DungeonPatternType)
        public byte north, east, south, west; // Edge types (always present)
    }

    /// <summary>
    /// Conversion helper for DungeonPattern to WFCPattern
    /// </summary>
    public static class PatternConversion
    {
        public static WFCPattern ToWFCPattern(DungeonPattern pattern)
        {
            return new WFCPattern
            {
                patternId = pattern.id,
                weight = pattern.weight,
                domain = PatternDomain.Dungeon,
                type = (int)pattern.type,
                north = pattern.north,
                east = pattern.east,
                south = pattern.south,
                west = pattern.west
            };
        }
    }

    /// <summary>
    /// Component that stores Wave Function Collapse data for structured terrain generation
    /// </summary>
    public struct WFCComponent : IComponentData
    {
        public int2 gridSize;
        public int patternSize;
        public float cellSize;
        public bool isCollapsed;
        public float entropy;
        public int selectedPattern;
        public BlobAssetReference<WFCPatternData> patterns;
        public BlobAssetReference<WFCConstraintData> constraints;
        public bool needsGeneration;
        public bool isGenerating;
        public float generationProgress;
        public float lastUpdateTime;
        public int iterations;
        public int maxIterations;
    }

    /// <summary>
    /// Component for WFC cell data
    /// Each cell tracks its possible patterns as a bitmask (up to 32 patterns)
    /// </summary>
    public struct WFCCell : IComponentData
    {
        public int2 position;
        public bool collapsed;
        public float entropy;
        public int selectedPattern;
        public int patternCount;
        public bool needsUpdate;
        public bool visualized; // Flag to track if this cell has been visualized
        public uint possiblePatternsMask; // Each bit represents a possible pattern (up to 32)
    }

    public static class WFCCellHelpers
    {
        public static bool IsPatternPossible(ref WFCCell cell, int patternIndex)
        {
            return (cell.possiblePatternsMask & (1u << patternIndex)) != 0;
        }
        public static void SetPatternPossible(ref WFCCell cell, int patternIndex, bool possible)
        {
            if (possible)
                cell.possiblePatternsMask |= (1u << patternIndex);
            else
                cell.possiblePatternsMask &= ~(1u << patternIndex);
        }
        public static int CountPossiblePatterns(uint mask)
        {
            int count = 0;
            for (int i = 0; i < 32; i++)
                if ((mask & (1u << i)) != 0) count++;
            return count;
        }
        public static int GetFirstPossiblePattern(uint mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1u << i)) != 0) return i;
            return -1;
        }
    }

    /// <summary>
    /// Component for WFC pattern data
    /// </summary>
    public struct WFCPatternData
    {
        public BlobArray<WFCPattern> patterns;
        public int patternCount;
    }

    /// <summary>
    /// Component for WFC constraint data
    /// </summary>
    public struct WFCConstraint : IComponentData
    {
        public int patternId;
        public int direction; // 0=North, 1=East, 2=South, 3=West
        public int neighborCount;
        public float strength;
    }

    public struct WFCConstraintData
    {
        public BlobArray<WFCConstraint> constraints;
        public int constraintCount;
    }

    public struct WFCGenerationSettings : IComponentData
    {
        public int maxIterations;
        public float constraintStrength;
        public float entropyThreshold;
        public bool enableBacktracking;
        public int backtrackingLimit;
        public float generationTimeout;
    }

    public struct WFCPerformanceData : IComponentData
    {
        public float generationTime;
        public int cellsProcessed;
        public int constraintChecks;
        public float averageEntropy;
        public int successfulGenerations;
        public int failedGenerations;
    }
} 