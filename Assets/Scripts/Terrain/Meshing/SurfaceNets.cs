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
        public float3 ChunkOrigin;

        public NativeList<float3> Vertices;
        public NativeList<int> Indices;

        public NativeArray<int> VertexIndices;
        public NativeArray<sbyte> CellSigns;
        public int3 CellResolution;

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

                var worldPos = ChunkOrigin + (new float3(samplePos) * VoxelSize);
                var weight = 1f / (math.abs(density) + 1e-5f);

                weightedPosition += worldPos * weight;
                totalWeight += weight;
            }

            SetCellSign(cellX, cellY, cellZ, densitySum >= 0f ? (sbyte)1 : (sbyte)(-1));

            if (minDensity >= 0f || maxDensity <= 0f)
            {
                return;
            }

            var vertex = totalWeight > 0f
                ? weightedPosition / totalWeight
                : ChunkOrigin + (new float3(cellX, cellY, cellZ) + 0.5f) * VoxelSize;

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

            for (int z = 0; z < CellResolution.z; z++)
            {
                for (int y = 0; y < CellResolution.y - 1; y++)
                {
                    for (int x = 0; x < CellResolution.x - 1; x++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x + 1, y, z);
                        var i2 = GetVertexIndex(x + 1, y + 1, z);
                        var i3 = GetVertexIndex(x, y + 1, z);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x + 1, y, z)
                                     + GetCellSign(x + 1, y + 1, z)
                                     + GetCellSign(x, y + 1, z);

                        TryEmitQuad(i0, i1, i2, i3, signSum);
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

            for (int y = 0; y < CellResolution.y; y++)
            {
                for (int z = 0; z < CellResolution.z - 1; z++)
                {
                    for (int x = 0; x < CellResolution.x - 1; x++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x + 1, y, z);
                        var i2 = GetVertexIndex(x + 1, y, z + 1);
                        var i3 = GetVertexIndex(x, y, z + 1);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x + 1, y, z)
                                     + GetCellSign(x + 1, y, z + 1)
                                     + GetCellSign(x, y, z + 1);

                        TryEmitQuad(i0, i1, i2, i3, signSum);
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

            for (int x = 0; x < CellResolution.x; x++)
            {
                for (int z = 0; z < CellResolution.z - 1; z++)
                {
                    for (int y = 0; y < CellResolution.y - 1; y++)
                    {
                        var i0 = GetVertexIndex(x, y, z);
                        var i1 = GetVertexIndex(x, y + 1, z);
                        var i2 = GetVertexIndex(x, y + 1, z + 1);
                        var i3 = GetVertexIndex(x, y, z + 1);

                        var signSum = GetCellSign(x, y, z)
                                     + GetCellSign(x, y + 1, z)
                                     + GetCellSign(x, y + 1, z + 1)
                                     + GetCellSign(x, y, z + 1);

                        TryEmitQuad(i0, i1, i2, i3, signSum);
                    }
                }
            }
        }

        private void TryEmitQuad(int i0, int i1, int i2, int i3, int signSum)
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0)
            {
                return;
            }

            if (math.abs(signSum) == 4)
            {
                return;
            }

            if (signSum < 0)
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
            Indices.Add(b);
            Indices.Add(c);
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
