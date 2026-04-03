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

            // Use >= 0f (not > 0f) so that a corner with density exactly 0 is treated as
            // "on the surface" rather than "not yet outside terrain."
            //
            // WHY THIS MATTERS FOR EDITS:
            // OpSubtraction(baseDensity, editDistance) returns 0 when the sample sits exactly
            // on the sphere boundary (editDistance == 0) and the base terrain is present
            // (baseDensity <= 0).  This produces density = 0.0f at the ring of voxels where
            // the edit sphere intersects the terrain surface.
            //
            // A cell with bottom corners at -1 (inside terrain) and top corners at 0 (sphere
            // boundary) has minDensity=-1, maxDensity=0.  The old strict check
            //   minDensity < 0f && maxDensity > 0f  →  true && false  →  false
            // silently dropped the cell, leaving a ring of holes wherever the sphere edge met
            // the terrain.  The relaxed check correctly detects this as a surface crossing.
            var hasSurface = minDensity < 0f && maxDensity >= 0f;
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

                        var gradient = AveragedGradient(
                            x, y, z,
                            x + 1, y, z,
                            x + 1, y + 1, z,
                            x, y + 1, z);
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

                        var gradient = AveragedGradient(
                            x, y, z,
                            x + 1, y, z,
                            x + 1, y, z + 1,
                            x, y, z + 1);
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

                        var gradient = AveragedGradient(
                            x, y, z,
                            x, y + 1, z,
                            x, y + 1, z + 1,
                            x, y, z + 1);
                        TryEmitQuad(i0, i1, i2, i3, signSum, gradient);
                    }
                }
            }
        }

        /// <summary>
        /// Full 3D SDF gradient averaged over 4 face corners. Using all three
        /// components instead of just the face-normal axis prevents the dot product
        /// in EmitTriangleWithGradient from becoming unreliable when the surface
        /// gradient is nearly tangent to the face axis (common at CSG seams where
        /// OpSubtraction/OpUnion create C0-continuous kinks in the density field).
        /// </summary>
        private float3 AveragedGradient(
            int x0, int y0, int z0,
            int x1, int y1, int z1,
            int x2, int y2, int z2,
            int x3, int y3, int z3)
        {
            return GradientAt(x0, y0, z0)
                 + GradientAt(x1, y1, z1)
                 + GradientAt(x2, y2, z2)
                 + GradientAt(x3, y3, z3);
        }

        private float3 GradientAt(int x, int y, int z)
        {
            var d = SampleDensity(new int3(x, y, z));
            return new float3(
                SampleDensity(new int3(x + 1, y, z)) - d,
                SampleDensity(new int3(x, y + 1, z)) - d,
                SampleDensity(new int3(x, y, z + 1)) - d);
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
