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

            // Sample all 8 corners and store densities + world positions
            var cornerDensities = new FixedList64Bytes<float>();
            var cornerWorldX = new FixedList64Bytes<float>();
            var cornerWorldY = new FixedList64Bytes<float>();
            var cornerWorldZ = new FixedList64Bytes<float>();

            for (int corner = 0; corner < 8; corner++)
            {
                var offset = CornerOffsets[corner];
                var samplePos = new int3(cellX + offset.x, cellY + offset.y, cellZ + offset.z);
                var density = SampleDensity(samplePos);

                cornerDensities.Add(density);
                cornerWorldX.Add(samplePos.x * VoxelSize);
                cornerWorldY.Add(samplePos.y * VoxelSize);
                cornerWorldZ.Add(samplePos.z * VoxelSize);

                densitySum += density;
                minDensity = math.min(minDensity, density);
                maxDensity = math.max(maxDensity, density);
            }

            var hasSurface = minDensity < 0f && maxDensity > 0f;
            if (!hasSurface)
            {
                SetCellSign(cellX, cellY, cellZ, densitySum >= 0f ? (sbyte)1 : (sbyte)(-1));
                return;
            }

            SetCellSign(cellX, cellY, cellZ, 0);

            // Edge interpolation: find zero-crossing on each of the 12 cube edges where
            // a sign change occurs, then average those crossing points.
            // This places the vertex on the actual isosurface instead of biasing toward
            // corners with density ≈ 0 (which caused banding on nearly-horizontal surfaces).
            //
            // Edge pairs (corner index A → corner index B):
            // Bottom face: 0-1, 1-2, 2-3, 3-0
            // Top face:    4-5, 5-6, 6-7, 7-4
            // Verticals:   0-4, 1-5, 2-6, 3-7
            float3 crossingSum = float3.zero;
            int crossingCount = 0;

            ProcessEdge(0, 1, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(1, 2, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(2, 3, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(3, 0, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(4, 5, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(5, 6, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(6, 7, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(7, 4, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(0, 4, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(1, 5, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(2, 6, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);
            ProcessEdge(3, 7, ref cornerDensities, ref cornerWorldX, ref cornerWorldY, ref cornerWorldZ, ref crossingSum, ref crossingCount);

            var vertex = crossingCount > 0
                ? crossingSum / crossingCount
                : (new float3(cellX, cellY, cellZ) + 0.5f) * VoxelSize;

            int newIndex = Vertices.Length;
            Vertices.Add(vertex);
            SetVertexIndex(cellX, cellY, cellZ, newIndex);
        }

        /// <summary>
        /// Check one cube edge for a sign change. If found, linearly interpolate to the
        /// zero-crossing point and accumulate it into the running average.
        /// </summary>
        private static void ProcessEdge(
            int a, int b,
            ref FixedList64Bytes<float> densities,
            ref FixedList64Bytes<float> worldX,
            ref FixedList64Bytes<float> worldY,
            ref FixedList64Bytes<float> worldZ,
            ref float3 crossingSum,
            ref int crossingCount)
        {
            float dA = densities[a];
            float dB = densities[b];

            // Only process edges where a sign change occurs (one positive, one negative)
            if ((dA < 0f) == (dB < 0f)) return;

            // Linear interpolation to find zero-crossing: t where density = 0
            float t = dA / (dA - dB);
            t = math.clamp(t, 0f, 1f);

            var posA = new float3(worldX[a], worldY[a], worldZ[a]);
            var posB = new float3(worldX[b], worldY[b], worldZ[b]);
            crossingSum += math.lerp(posA, posB, t);
            crossingCount++;
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

                        // Full 3D gradient at the shared interior grid node (x+1, y+1, z)
                        // using backward differences for X/Y and forward for Z.
                        var d111 = SampleDensity(new int3(x + 1, y + 1, z + 1));
                        var d110 = SampleDensity(new int3(x + 1, y + 1, z));
                        var d011 = SampleDensity(new int3(x, y + 1, z + 1));
                        var d101 = SampleDensity(new int3(x + 1, y, z + 1));
                        var gradient = new float3(d111 - d011, d111 - d101, d111 - d110);
                        TryEmitQuad(i0, i1, i2, i3, signSum, gradient);
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

                        // Full 3D gradient at the shared interior grid node (x+1, y, z+1)
                        // using backward differences for X/Z and forward for Y.
                        var d111 = SampleDensity(new int3(x + 1, y + 1, z + 1));
                        var d011 = SampleDensity(new int3(x, y + 1, z + 1));
                        var d101 = SampleDensity(new int3(x + 1, y, z + 1));
                        var d110 = SampleDensity(new int3(x + 1, y + 1, z));
                        var gradient = new float3(d111 - d011, d111 - d101, d111 - d110);
                        TryEmitQuad(i0, i1, i2, i3, signSum, gradient);
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

                        // Full 3D gradient at the shared interior grid node (x, y+1, z+1)
                        // using forward difference for X and backward for Y/Z.
                        var d111 = SampleDensity(new int3(x + 1, y + 1, z + 1));
                        var d011 = SampleDensity(new int3(x, y + 1, z + 1));
                        var d101 = SampleDensity(new int3(x + 1, y, z + 1));
                        var d110 = SampleDensity(new int3(x + 1, y + 1, z));
                        var gradient = new float3(d111 - d011, d111 - d101, d111 - d110);
                        TryEmitQuad(i0, i1, i2, i3, signSum, gradient);
                    }
                }
            }
        }

        private void TryEmitQuad(int i0, int i1, int i2, int i3, int signSum, float3 gradient)
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 || i3 < 0)
            {
                return;
            }

            if (math.abs(signSum) == 4)
            {
                return;
            }

            if (i0 >= Vertices.Length || i1 >= Vertices.Length || i2 >= Vertices.Length || i3 >= Vertices.Length)
            {
                return;
            }

            // Surface Nets winding: each triangle is independently oriented so its normal
            // aligns with the full 3D SDF gradient sampled at the shared interior grid
            // node. Using the full gradient (not just the face-axis component) handles
            // curved surfaces where the actual triangle normal may point in a very
            // different direction than the face axis.
            EmitTriangleWithGradient(i0, i1, i2, gradient);
            EmitTriangleWithGradient(i0, i2, i3, gradient);
        }

        private void EmitTriangleWithGradient(int a, int b, int c, float3 gradient)
        {
            var normal = math.cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            var dot = math.dot(normal, gradient);

            if (dot < 0f)
            {
                // Flip winding so normal aligns with gradient (outward)
                Indices.Add(a);
                Indices.Add(c);
                Indices.Add(b);
            }
            else
            {
                Indices.Add(a);
                Indices.Add(b);
                Indices.Add(c);
            }
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
