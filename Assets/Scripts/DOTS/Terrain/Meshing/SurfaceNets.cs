using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DOTS.Terrain.Meshing
{
    [BurstCompile]
    public struct SurfaceNetsJob : IJob
    {
        [ReadOnly] public NativeArray<float> Densities;
        public int3 Resolution;
        public float VoxelSize;
        public NativeList<float3> Vertices;
        public NativeList<int> Indices;

        public NativeArray<int> VertexIndices;
        public NativeArray<sbyte> CellSigns;
        public int3 CellResolution;
        public int3 BaseCellResolution;

        private static readonly int3[] CornerOffsets =
        {
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(1, 0, 1),
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(1, 1, 1),
            new int3(0, 1, 1)
        };

        public void Execute()
        {
            if (!Densities.IsCreated || !Vertices.IsCreated || !Indices.IsCreated)
            {
                return;
            }

            if (CellResolution.x <= 0 || CellResolution.y <= 0 || CellResolution.z <= 0)
            {
                return;
            }

            InitializeWorkingArrays();

            for (int z = 0; z < CellResolution.z; z++)
            {
                for (int y = 0; y < CellResolution.y; y++)
                {
                    for (int x = 0; x < CellResolution.x; x++)
                    {
                        ProcessCell(x, y, z);
                    }
                }
            }

            BuildIndices();
        }

        private void InitializeWorkingArrays()
        {
            if (VertexIndices.IsCreated)
            {
                for (int i = 0; i < VertexIndices.Length; i++)
                {
                    VertexIndices[i] = -1;
                }
            }

            if (CellSigns.IsCreated)
            {
                for (int i = 0; i < CellSigns.Length; i++)
                {
                    CellSigns[i] = 0;
                }
            }
        }

        private void ProcessCell(int cellX, int cellY, int cellZ)
        {
            float minDensity = float.MaxValue;
            float maxDensity = float.MinValue;
            float densitySum = 0f;

            float totalWeight = 0f;
            float3 weightedPosition = float3.zero;

            for (int corner = 0; corner < 8; corner++)
            {
                var offset = CornerOffsets[corner];
                var samplePos = new int3(cellX + offset.x, cellY + offset.y, cellZ + offset.z);
                var density = SampleDensity(samplePos);

                densitySum += density;
                minDensity = math.min(minDensity, density);
                maxDensity = math.max(maxDensity, density);

                var worldPos = new float3(samplePos) * VoxelSize;
                var weight = 1f / (math.abs(density) + 1e-5f);

                weightedPosition += worldPos * weight;
                totalWeight += weight;
            }

            var hasSurface = minDensity < 0f && maxDensity > 0f;
            if (!hasSurface)
            {
                SetCellSign(cellX, cellY, cellZ, densitySum >= 0f ? (sbyte)1 : (sbyte)(-1));
                return;
            }

            SetCellSign(cellX, cellY, cellZ, 0);

            var vertex = totalWeight > 0f
                ? weightedPosition / totalWeight
                : (new float3(cellX, cellY, cellZ) + 0.5f) * VoxelSize;

            int newIndex = Vertices.Length;
            Vertices.Add(vertex);
            SetVertexIndex(cellX, cellY, cellZ, newIndex);
        }

        private void BuildIndices()
        {
            if (!VertexIndices.IsCreated || !CellSigns.IsCreated)
            {
                return;
            }

            GenerateXYFaces();
            GenerateXZFaces();
            GenerateYZFaces();

            if (Indices.Length == 0 && Vertices.Length >= 3)
            {
                for (int i = 2; i < Vertices.Length; i++)
                {
                    EmitTriangle(0, i - 1, i);
                }
            }
        }

        private void GenerateXYFaces()
        {
            if (CellResolution.x < 2 || CellResolution.y < 2)
            {
                return;
            }

            // Allow stitching on +X/+Y by permitting x/y at the last base cell (referencing the ghost cell at +1).
            var maxX = math.min(BaseCellResolution.x, CellResolution.x - 1);
            var maxY = math.min(BaseCellResolution.y, CellResolution.y - 1);
            for (int z = 0; z < CellResolution.z; z++)
            {
                for (int y = 0; y < maxY; y++)
                {
                    for (int x = 0; x < maxX; x++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x + 1, y, z);
                        var i2 = GetVertexIndex(x + 1, y + 1, z);
                        var i3 = GetVertexIndex(x, y + 1, z);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x + 1, y, z)
                                     + GetCellSign(x + 1, y + 1, z)
                                     + GetCellSign(x, y + 1, z);

                        // Use the sign of the neighboring +Z cell to break ties on blended slopes / seams.
                        var neighborSign = GetCellSign(x, y, math.min(z + 1, CellResolution.z - 1));
                        // For XY faces, enforce outward along -Z (matches prior orientation expectations).
                        TryEmitQuad(i0, i1, i2, i3, signSum, neighborSign, new float3(0f, 0f, -1f));
                    }
                }
            }
        }

        private void GenerateXZFaces()
        {
            if (CellResolution.x < 2 || CellResolution.z < 2)
            {
                return;
            }

            // Allow stitching on +X/+Z by permitting x/z at the last base cell (referencing the ghost cell at +1).
            var maxX = math.min(BaseCellResolution.x, CellResolution.x - 1);
            var maxZ = math.min(BaseCellResolution.z, CellResolution.z - 1);
            for (int y = 0; y < CellResolution.y; y++)
            {
                for (int z = 0; z < maxZ; z++)
                {
                    for (int x = 0; x < maxX; x++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x + 1, y, z);
                        var i2 = GetVertexIndex(x + 1, y, z + 1);
                        var i3 = GetVertexIndex(x, y, z + 1);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x + 1, y, z)
                                     + GetCellSign(x + 1, y, z + 1)
                                     + GetCellSign(x, y, z + 1);

                        var neighborSign = GetCellSign(x, math.min(y + 1, CellResolution.y - 1), z);
                        TryEmitQuad(i0, i1, i2, i3, signSum, neighborSign, new float3(0f, 1f, 0f));
                    }
                }
            }
        }

        private void GenerateYZFaces()
        {
            if (CellResolution.y < 2 || CellResolution.z < 2)
            {
                return;
            }

            // Don't emit the full overlap region on +X; stitching is handled by XY/XZ faces.
            // Allow stitching on +Y/+Z by permitting y/z at the last base cell (referencing the ghost cell at +1).
            var maxX = math.min(BaseCellResolution.x, CellResolution.x);
            var maxY = math.min(BaseCellResolution.y, CellResolution.y - 1);
            var maxZ = math.min(BaseCellResolution.z, CellResolution.z - 1);

            for (int x = 0; x < maxX; x++)
            {
                for (int z = 0; z < maxZ; z++)
                {
                    for (int y = 0; y < maxY; y++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x, y + 1, z);
                        var i2 = GetVertexIndex(x, y + 1, z + 1);
                        var i3 = GetVertexIndex(x, y, z + 1);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x, y + 1, z)
                                     + GetCellSign(x, y + 1, z + 1)
                                     + GetCellSign(x, y, z + 1);

                        var neighborSign = GetCellSign(math.min(x + 1, CellResolution.x - 1), y, z);
                        TryEmitQuad(i0, i1, i2, i3, signSum, neighborSign, new float3(1f, 0f, 0f));
                    }
                }
            }
        }

        private void TryEmitQuad(int i0, int i1, int i2, int i3, int signSum, int tieBreakSign, float3 faceAxis)
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0)
            {
                return;
            }

            if (math.abs(signSum) == 4)
            {
                return;
            }

            var effectiveSign = signSum;
            if (effectiveSign == 0)
            {
                effectiveSign = tieBreakSign;
            }

            if (effectiveSign == 0)
            {
                effectiveSign = 1; // deterministic fallback to avoid winding flip-flop
            }

            var flip = effectiveSign < 0;

            if (i0 >= Vertices.Length || i1 >= Vertices.Length || i2 >= Vertices.Length || i3 >= Vertices.Length)
            {
                return;
            }

            var a = Vertices[i0];
            var b = Vertices[flip ? i2 : i1];
            var c = Vertices[flip ? i1 : i2];

            var normal = math.cross(b - a, c - a);
            if (math.dot(normal, faceAxis) > 0f)
            {
                flip = !flip; // enforce deterministic outward-facing orientation per axis
            }

            if (flip)
            {
                EmitTriangle(i0, i2, i1);
                EmitTriangle(i0, i3, i2);
            }
            else
            {
                EmitTriangle(i0, i1, i2);
                EmitTriangle(i0, i2, i3);
            }
        }

        private void EmitTriangle(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(c);
            Indices.Add(b);
        }

        private void SetVertexIndex(int x, int y, int z, int vertexIndex)
        {
            if (!VertexIndices.IsCreated)
            {
                return;
            }

            var idx = GetCellIndex(x, y, z);
            if (idx >= 0 && idx < VertexIndices.Length)
            {
                VertexIndices[idx] = vertexIndex;
            }
        }

        private void SetCellSign(int x, int y, int z, sbyte sign)
        {
            if (!CellSigns.IsCreated)
            {
                return;
            }

            var idx = GetCellIndex(x, y, z);
            if (idx >= 0 && idx < CellSigns.Length)
            {
                CellSigns[idx] = sign;
            }
        }

        private int GetVertexIndex(int x, int y, int z)
        {
            if (!VertexIndices.IsCreated)
            {
                return -1;
            }

            var idx = GetCellIndex(x, y, z);
            return idx >= 0 && idx < VertexIndices.Length ? VertexIndices[idx] : -1;
        }

        private sbyte GetCellSign(int x, int y, int z)
        {
            if (!CellSigns.IsCreated)
            {
                return 0;
            }

            var idx = GetCellIndex(x, y, z);
            return idx >= 0 && idx < CellSigns.Length ? CellSigns[idx] : (sbyte)0;
        }

        private int GetCellIndex(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 ||
                x >= CellResolution.x || y >= CellResolution.y || z >= CellResolution.z)
            {
                return -1;
            }

            return x + CellResolution.x * (y + CellResolution.y * z);
        }

        private float SampleDensity(int3 position)
        {
            var index = position.x + Resolution.x * (position.y + Resolution.y * position.z);
            return Densities[index];
        }
    }
}
