using Unity.Collections;
using Unity.Mathematics;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Utility class for analyzing terrain mesh properties to diagnose banding artifacts.
    /// Used by diagnostic tests to measure vertex distribution, triangle quality, and clustering patterns.
    /// </summary>
    public static class TerrainMeshAnalyzer
    {
        public struct MeshAnalysis
        {
            public int VertexCount;
            public int TriangleCount;
            public float MinY;
            public float MaxY;
            public float[] YHistogram;
            public int HistogramBuckets;
            public float AvgTriangleArea;
            public float MinTriangleArea;
            public float MaxAspectRatio;
            public int DegenerateTriangleCount;
        }

        /// <summary>
        /// Performs a complete analysis of mesh properties for banding diagnosis.
        /// </summary>
        /// <param name="vertices">Array of vertex positions</param>
        /// <param name="indices">Array of triangle indices (3 per triangle)</param>
        /// <param name="histogramBuckets">Number of buckets for Y-position histogram</param>
        /// <returns>Analysis results including vertex distribution and triangle metrics</returns>
        public static MeshAnalysis Analyze(NativeArray<float3> vertices, NativeArray<int> indices, int histogramBuckets = 100)
        {
            var analysis = new MeshAnalysis
            {
                VertexCount = vertices.Length,
                TriangleCount = indices.Length / 3,
                HistogramBuckets = histogramBuckets,
                MinY = float.MaxValue,
                MaxY = float.MinValue,
                MinTriangleArea = float.MaxValue,
                MaxAspectRatio = 0f,
                DegenerateTriangleCount = 0
            };

            if (vertices.Length == 0)
            {
                analysis.MinY = 0f;
                analysis.MaxY = 0f;
                analysis.YHistogram = new float[histogramBuckets];
                return analysis;
            }

            // Find Y range
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                if (y < analysis.MinY) analysis.MinY = y;
                if (y > analysis.MaxY) analysis.MaxY = y;
            }

            // Compute Y histogram
            analysis.YHistogram = ComputeYHistogram(vertices, histogramBuckets, analysis.MinY, analysis.MaxY);

            // Analyze triangles
            if (indices.Length >= 3)
            {
                float totalArea = 0f;
                int validTriangles = 0;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];

                    if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                    {
                        continue;
                    }

                    float3 a = vertices[i0];
                    float3 b = vertices[i1];
                    float3 c = vertices[i2];

                    float area = ComputeTriangleArea(a, b, c);
                    float aspectRatio = ComputeTriangleAspectRatio(a, b, c);

                    totalArea += area;
                    validTriangles++;

                    if (area < analysis.MinTriangleArea)
                    {
                        analysis.MinTriangleArea = area;
                    }

                    if (aspectRatio > analysis.MaxAspectRatio)
                    {
                        analysis.MaxAspectRatio = aspectRatio;
                    }

                    // Consider a triangle degenerate if area is very small or aspect ratio is very high
                    const float degenerateAreaThreshold = 1e-6f;
                    const float degenerateAspectThreshold = 100f;
                    if (area < degenerateAreaThreshold || aspectRatio > degenerateAspectThreshold)
                    {
                        analysis.DegenerateTriangleCount++;
                    }
                }

                if (validTriangles > 0)
                {
                    analysis.AvgTriangleArea = totalArea / validTriangles;
                }

                if (analysis.MinTriangleArea == float.MaxValue)
                {
                    analysis.MinTriangleArea = 0f;
                }
            }
            else
            {
                analysis.MinTriangleArea = 0f;
            }

            return analysis;
        }

        /// <summary>
        /// Computes a histogram of vertex Y positions.
        /// </summary>
        /// <param name="vertices">Array of vertex positions</param>
        /// <param name="buckets">Number of histogram buckets</param>
        /// <returns>Array of bucket counts normalized by total vertex count</returns>
        public static float[] ComputeYHistogram(NativeArray<float3> vertices, int buckets)
        {
            if (vertices.Length == 0 || buckets <= 0)
            {
                return new float[math.max(buckets, 1)];
            }

            // Find Y range
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return ComputeYHistogram(vertices, buckets, minY, maxY);
        }

        private static float[] ComputeYHistogram(NativeArray<float3> vertices, int buckets, float minY, float maxY)
        {
            var histogram = new float[buckets];

            if (vertices.Length == 0 || buckets <= 0)
            {
                return histogram;
            }

            float range = maxY - minY;
            if (range <= 0f)
            {
                // All vertices at same Y, put everything in first bucket
                histogram[0] = 1f;
                return histogram;
            }

            // Count vertices in each bucket
            var counts = new int[buckets];
            for (int i = 0; i < vertices.Length; i++)
            {
                float y = vertices[i].y;
                float normalized = (y - minY) / range;
                int bucket = math.clamp((int)(normalized * buckets), 0, buckets - 1);
                counts[bucket]++;
            }

            // Normalize by total count
            float totalCount = vertices.Length;
            for (int i = 0; i < buckets; i++)
            {
                histogram[i] = counts[i] / totalCount;
            }

            return histogram;
        }

        /// <summary>
        /// Finds peaks in the histogram that indicate vertex clustering.
        /// A peak is a bucket with count exceeding average by more than threshold.
        /// </summary>
        /// <param name="histogram">Normalized histogram (values sum to 1)</param>
        /// <param name="threshold">Multiplier over average to consider a peak (e.g., 2.0 = 2x average)</param>
        /// <returns>Number of peaks found</returns>
        public static int FindClusteringPeaks(float[] histogram, float threshold)
        {
            if (histogram == null || histogram.Length == 0)
            {
                return 0;
            }

            // Calculate average bucket value
            float sum = 0f;
            for (int i = 0; i < histogram.Length; i++)
            {
                sum += histogram[i];
            }
            float average = sum / histogram.Length;

            // Count buckets that exceed threshold * average
            float peakThreshold = average * threshold;
            int peakCount = 0;

            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > peakThreshold)
                {
                    peakCount++;
                }
            }

            return peakCount;
        }

        /// <summary>
        /// Computes the aspect ratio of a triangle.
        /// Aspect ratio = longest edge / altitude to longest edge.
        /// A perfect equilateral triangle has aspect ratio ~1.15.
        /// Degenerate (thin) triangles have very high aspect ratios.
        /// </summary>
        public static float ComputeTriangleAspectRatio(float3 a, float3 b, float3 c)
        {
            float3 ab = b - a;
            float3 bc = c - b;
            float3 ca = a - c;

            float lenAB = math.length(ab);
            float lenBC = math.length(bc);
            float lenCA = math.length(ca);

            // Find longest edge
            float longestEdge = math.max(lenAB, math.max(lenBC, lenCA));

            if (longestEdge < 1e-8f)
            {
                return float.MaxValue; // Degenerate triangle
            }

            // Compute area using cross product
            float3 cross = math.cross(ab, ca);
            float area = math.length(cross) * 0.5f;

            if (area < 1e-10f)
            {
                return float.MaxValue; // Degenerate triangle
            }

            // Altitude to longest edge = 2 * area / longestEdge
            float altitude = (2f * area) / longestEdge;

            if (altitude < 1e-10f)
            {
                return float.MaxValue;
            }

            return longestEdge / altitude;
        }

        /// <summary>
        /// Computes the area of a triangle using the cross product method.
        /// </summary>
        public static float ComputeTriangleArea(float3 a, float3 b, float3 c)
        {
            float3 ab = b - a;
            float3 ac = c - a;
            float3 cross = math.cross(ab, ac);
            return math.length(cross) * 0.5f;
        }

        /// <summary>
        /// Gets the Y positions where histogram peaks occur.
        /// Useful for identifying band heights in the mesh.
        /// </summary>
        /// <param name="histogram">Normalized histogram</param>
        /// <param name="minY">Minimum Y value in mesh</param>
        /// <param name="maxY">Maximum Y value in mesh</param>
        /// <param name="threshold">Multiplier over average to consider a peak</param>
        /// <returns>Array of Y positions where peaks occur</returns>
        public static float[] GetPeakYPositions(float[] histogram, float minY, float maxY, float threshold)
        {
            if (histogram == null || histogram.Length == 0)
            {
                return System.Array.Empty<float>();
            }

            // Calculate average
            float sum = 0f;
            for (int i = 0; i < histogram.Length; i++)
            {
                sum += histogram[i];
            }
            float average = sum / histogram.Length;
            float peakThreshold = average * threshold;

            // Find peak indices
            var peakIndices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < histogram.Length; i++)
            {
                if (histogram[i] > peakThreshold)
                {
                    peakIndices.Add(i);
                }
            }

            // Convert to Y positions
            float range = maxY - minY;
            var peakYs = new float[peakIndices.Count];
            for (int i = 0; i < peakIndices.Count; i++)
            {
                float bucketCenter = (peakIndices[i] + 0.5f) / histogram.Length;
                peakYs[i] = minY + bucketCenter * range;
            }

            return peakYs;
        }

        /// <summary>
        /// Computes statistics about vertex spacing to detect quantization.
        /// </summary>
        public struct VertexSpacingStats
        {
            public float MeanSpacing;
            public float MinSpacing;
            public float MaxSpacing;
            public float StandardDeviation;
        }

        /// <summary>
        /// Analyzes the Y-spacing between sorted vertex Y positions.
        /// If vertices are quantized to discrete levels, spacing will show regular patterns.
        /// </summary>
        public static VertexSpacingStats AnalyzeVertexYSpacing(NativeArray<float3> vertices)
        {
            var stats = new VertexSpacingStats
            {
                MeanSpacing = 0f,
                MinSpacing = float.MaxValue,
                MaxSpacing = 0f,
                StandardDeviation = 0f
            };

            if (vertices.Length < 2)
            {
                stats.MinSpacing = 0f;
                return stats;
            }

            // Extract and sort Y values
            var yValues = new float[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                yValues[i] = vertices[i].y;
            }
            System.Array.Sort(yValues);

            // Compute spacings between consecutive unique Y values
            var spacings = new System.Collections.Generic.List<float>();
            float prevY = yValues[0];

            for (int i = 1; i < yValues.Length; i++)
            {
                float spacing = yValues[i] - prevY;
                if (spacing > 1e-6f) // Skip near-duplicate Y values
                {
                    spacings.Add(spacing);
                    prevY = yValues[i];
                }
            }

            if (spacings.Count == 0)
            {
                stats.MinSpacing = 0f;
                return stats;
            }

            // Compute statistics
            float sum = 0f;
            foreach (var s in spacings)
            {
                sum += s;
                if (s < stats.MinSpacing) stats.MinSpacing = s;
                if (s > stats.MaxSpacing) stats.MaxSpacing = s;
            }
            stats.MeanSpacing = sum / spacings.Count;

            // Standard deviation
            float variance = 0f;
            foreach (var s in spacings)
            {
                float diff = s - stats.MeanSpacing;
                variance += diff * diff;
            }
            stats.StandardDeviation = math.sqrt(variance / spacings.Count);

            return stats;
        }
    }
}
