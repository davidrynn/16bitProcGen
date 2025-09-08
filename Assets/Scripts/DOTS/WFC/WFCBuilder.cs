using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Builder for creating WFC components and data
    /// </summary>
    public static class WFCBuilder
    {
        /// <summary>
        /// Creates a WFC component with default settings
        /// </summary>
        public static WFCComponent CreateWFCComponent(int2 gridSize, int patternSize = 3, float cellSize = 1.0f)
        {
            return new WFCComponent
            {
                gridSize = gridSize,
                patternSize = patternSize,
                cellSize = cellSize,
                isCollapsed = false,
                entropy = 1.0f,
                selectedPattern = -1,
                patterns = BlobAssetReference<WFCPatternData>.Null,
                constraints = BlobAssetReference<WFCConstraintData>.Null,
                needsGeneration = true,
                isGenerating = false,
                generationProgress = 0.0f,
                lastUpdateTime = 0.0f,
                iterations = 0,
                maxIterations = 1000
            };
        }
        
        /// <summary>
        /// Creates WFC pattern data blob asset
        /// </summary>
        public static BlobAssetReference<WFCPatternData> CreatePatternData(WFCPattern[] patterns)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WFCPatternData>();
            
            root.patternCount = patterns.Length;
            
            var patternArray = builder.Allocate(ref root.patterns, patterns.Length);
            
            for (int i = 0; i < patterns.Length; i++)
            {
                patternArray[i] = patterns[i];
            }
            
            var blobAsset = builder.CreateBlobAssetReference<WFCPatternData>(Allocator.Persistent);
            builder.Dispose();
            
            return blobAsset;
        }
        
        /// <summary>
        /// Creates WFC constraint data blob asset
        /// </summary>
        public static BlobAssetReference<WFCConstraintData> CreateConstraintData(WFCConstraint[] constraints)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WFCConstraintData>();
            
            root.constraintCount = constraints.Length;
            
            var constraintArray = builder.Allocate(ref root.constraints, constraints.Length);
            
            for (int i = 0; i < constraints.Length; i++)
            {
                constraintArray[i] = constraints[i];
            }
            
            var blobAsset = builder.CreateBlobAssetReference<WFCConstraintData>(Allocator.Persistent);
            builder.Dispose();
            
            return blobAsset;
        }
        
        /// <summary>
        /// Creates dungeon macro-tile patterns with 0/90/180/270 rotated variants.
        /// Patterns use 'F' (open) and 'W' (closed) on edges.
        /// </summary>
        public static List<WFCPattern> CreateDungeonMacroTilePatterns()
        {
            var result = new List<WFCPattern>();

            int nextId = 0;

            // Helper to add a base tile and 3 rotations
            void AddRotated(DungeonPatternType type, byte n, byte e, byte s, byte w, float weight)
            {
                byte rn = n, re = e, rs = s, rw = w;
                for (int rot = 0; rot < 4; rot++)
                {
                    var dp = new DungeonPattern
                    {
                        id = nextId++,
                        name = type.ToString() + "_" + rot * 90,
                        type = type,
                        north = rn,
                        east = re,
                        south = rs,
                        west = rw,
                        weight = weight
                    };
                    result.Add(PatternConversion.ToWFCPattern(dp));

                    // Rotate edges clockwise for next variant
                    byte oldN = rn;
                    rn = rw; // N <- W
                    rw = rs; // W <- S
                    rs = re; // S <- E
                    re = oldN; // E <- N
                }
            }

            // Room floor: open all sides
            AddRotated(DungeonPatternType.Floor, (byte)'F', (byte)'F', (byte)'F', (byte)'F', 1.0f);

            // Room edge: one closed face, three open
            AddRotated(DungeonPatternType.Wall, (byte)'W', (byte)'F', (byte)'F', (byte)'F', 1.0f);

            // Corridor straight: open N/S, closed E/W
            AddRotated(DungeonPatternType.Corridor, (byte)'F', (byte)'W', (byte)'F', (byte)'W', 1.0f);

            // Corner: open N and E, closed S and W (rotated gives the other corners)
            AddRotated(DungeonPatternType.Corner, (byte)'F', (byte)'F', (byte)'W', (byte)'W', 1.0f);

            // Corridor end with doorway: treat door side as open for adjacency
            AddRotated(DungeonPatternType.Door, (byte)'F', (byte)'W', (byte)'W', (byte)'W', 0.9f);

            return result;
        }
        
        /// <summary>
        /// Creates basic dungeon constraints for WFC
        /// </summary>
        public static List<WFCConstraint> CreateBasicDungeonConstraints()
        {
            var constraints = new List<WFCConstraint>();
            
            // Note: Current HybridWFCSystem does not apply constraints during propagation.
            // We keep a placeholder list to remain API-compatible.
            
            return constraints;
        }

        /// <summary>
        /// Creates default terrain patterns for WFC (generic approach)
        /// </summary>
        public static WFCPattern[] CreateDefaultTerrainPatterns()
        {
            return new WFCPattern[]
            {
                // Water pattern - open on all sides
                CreateGenericPattern(0, 1.0f, PatternDomain.Terrain, (byte)'W', (byte)'W', (byte)'W', (byte)'W'),
                
                // Sand pattern - can connect to water and grass
                CreateGenericPattern(1, 1.0f, PatternDomain.Terrain, (byte)'S', (byte)'S', (byte)'S', (byte)'S'),
                
                // Grass pattern - can connect to sand and rock
                CreateGenericPattern(2, 1.0f, PatternDomain.Terrain, (byte)'G', (byte)'G', (byte)'G', (byte)'G'),
                
                // Rock pattern - can connect to grass and snow
                CreateGenericPattern(3, 1.0f, PatternDomain.Terrain, (byte)'R', (byte)'R', (byte)'R', (byte)'R'),
                
                // Snow pattern - can connect to rock
                CreateGenericPattern(4, 1.0f, PatternDomain.Terrain, (byte)'N', (byte)'N', (byte)'N', (byte)'N')
            };
        }
        
        /// <summary>
        /// Creates default constraints for terrain patterns (generic approach)
        /// </summary>
        public static WFCConstraint[] CreateDefaultTerrainConstraints()
        {
            return new WFCConstraint[]
            {
                // Water can connect to sand
                CreateConstraint(0, 0, 1, 1.0f), // North
                CreateConstraint(0, 1, 1, 1.0f), // East
                CreateConstraint(0, 2, 1, 1.0f), // South
                CreateConstraint(0, 3, 1, 1.0f), // West
                
                // Sand can connect to water and grass
                CreateConstraint(1, 0, 2, 1.0f),
                CreateConstraint(1, 1, 2, 1.0f),
                CreateConstraint(1, 2, 2, 1.0f),
                CreateConstraint(1, 3, 2, 1.0f),
                
                // Grass can connect to sand and rock
                CreateConstraint(2, 0, 2, 1.0f),
                CreateConstraint(2, 1, 2, 1.0f),
                CreateConstraint(2, 2, 2, 1.0f),
                CreateConstraint(2, 3, 2, 1.0f),
                
                // Rock can connect to grass and snow
                CreateConstraint(3, 0, 2, 1.0f),
                CreateConstraint(3, 1, 2, 1.0f),
                CreateConstraint(3, 2, 2, 1.0f),
                CreateConstraint(3, 3, 2, 1.0f),
                
                // Snow can connect to rock
                CreateConstraint(4, 0, 1, 1.0f),
                CreateConstraint(4, 1, 1, 1.0f),
                CreateConstraint(4, 2, 1, 1.0f),
                CreateConstraint(4, 3, 1, 1.0f)
            };
        }
        
        /// <summary>
        /// Creates a generic WFC pattern with the new structure
        /// </summary>
        private static WFCPattern CreateGenericPattern(int patternId, float weight, PatternDomain domain, byte north, byte east, byte south, byte west)
        {
            return new WFCPattern
            {
                patternId = patternId,
                weight = weight,
                domain = domain,
                type = 0, // Generic type
                north = north,
                east = east,
                south = south,
                west = west
            };
        }
        
        /// <summary>
        /// Creates a WFC constraint
        /// </summary>
        private static WFCConstraint CreateConstraint(int patternId, int direction, int neighborCount, float strength)
        {
            return new WFCConstraint
            {
                patternId = patternId,
                direction = direction,
                neighborCount = neighborCount,
                strength = strength
            };
        }
        
        /// <summary>
        /// Creates WFC generation settings with default values
        /// </summary>
        public static WFCGenerationSettings CreateDefaultSettings()
        {
            return new WFCGenerationSettings
            {
                maxIterations = 1000,
                constraintStrength = 1.0f,
                entropyThreshold = 0.1f,
                enableBacktracking = true,
                backtrackingLimit = 100,
                generationTimeout = 5.0f
            };
        }

        /// <summary>
        /// Checks if two patterns are compatible in a given direction
        /// </summary>
        public static bool PatternsAreCompatible(WFCPattern a, WFCPattern b, int direction)
        {
            // direction: 0=N, 1=E, 2=S, 3=W
            // Returns true if a's edge matches b's opposite edge
            switch (direction)
            {
                case 0: return a.north == b.south; // a's north to b's south
                case 1: return a.east == b.west;   // a's east to b's west
                case 2: return a.south == b.north; // a's south to b's north
                case 3: return a.west == b.east;   // a's west to b's east
                default: return false;
            }
        }
    }
} 