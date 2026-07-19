using Unity.Collections;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    /// <summary>
    /// A single authored flatten region for the <c>H</c> mask <c>M(x,z)</c> (WORLD_STRUCTURE_SPEC.md
    /// §4.1, ticket H3). A capsule: the segment <see cref="A"/>→<see cref="B"/> plus a flat core
    /// half-width <see cref="Radius"/>, ramping back to full relief over <see cref="Feather"/>. The
    /// segment shape (not just a disc) is what lets one region cover the whole spawn→hero sightline.
    /// Blittable — safe in Burst jobs and shader-global packing.
    /// </summary>
    public struct WorldStructureMaskRegion
    {
        /// <summary>Corridor segment start, world XZ.</summary>
        public float2 A;
        /// <summary>Corridor segment end, world XZ.</summary>
        public float2 B;
        /// <summary>Flat-core half-width: within this distance of the segment, H is fully flattened.</summary>
        public float Radius;
        /// <summary>Ramp width from the flat core back to full relief.</summary>
        public float Feather;
    }

    /// <summary>
    /// The authored flatten mask <c>M(x,z)</c> — the third factor of <c>H = A(r)·ridgedFBM·M</c>
    /// (spec §4.1, ticket H3). <c>M ∈ [0,1]</c>: 0 = fully flat, 1 = full macro relief. It is the
    /// product of per-region capsule falloffs, so any region can flatten and overlaps stay flat.
    ///
    /// <para><b>MVP requirement (§4.1):</b> one region covers the spawn→hero-hand (0, 900) vista
    /// corridor so macro relief can never rise into the MVP sightline (<see cref="DefaultVistaCorridor"/>).
    /// Masks are authored data → inside the determinism invariant (§3).</para>
    ///
    /// <para>This C# evaluation mirrors <c>WorldStructure.hlsl:WorldMacroMask</c> line-for-line
    /// (same capsule distance + smoothstep), the same mirror contract as the rest of the pair.</para>
    /// </summary>
    public static class WorldStructureMask
    {
        /// <summary>Max simultaneous regions — mirrors the HLSL <c>WORLD_MACRO_MAX_MASKS</c> and the
        /// broadcast's shader-global array size. Keep the three in sync.</summary>
        public const int MaxRegions = 4;

        /// <summary>
        /// The MVP vista corridor: spawn (0,0) → past the hero at (0, 900) out to (0, 1000), with a
        /// flat core and feather wide enough that no macro ridge rises into the from-spawn view.
        /// Single source of truth — the broadcast seeds it by default and the guard tests assert against it.
        /// </summary>
        public static readonly WorldStructureMaskRegion DefaultVistaCorridor = new WorldStructureMaskRegion
        {
            A = new float2(0f, 0f),
            B = new float2(0f, 1000f),
            Radius = 110f,
            Feather = 220f,
        };

        /// <summary>Shortest distance from <paramref name="p"/> to segment <paramref name="a"/>→<paramref name="b"/>.</summary>
        public static float DistanceToSegment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float2 ap = p - a;
            float t = math.saturate(math.dot(ap, ab) / math.max(math.dot(ab, ab), 1e-6f));
            return math.distance(p, a + t * ab);
        }

        /// <summary>
        /// Mask value in [0,1] at a world-XZ position. Empty region set → 1 (full relief). Each region
        /// contributes <c>smoothstep(radius, radius+feather, distToSegment)</c>; the product means any
        /// region flattens and overlaps stay flat.
        /// </summary>
        public static float Evaluate(float2 worldXZ, NativeArray<WorldStructureMaskRegion> regions)
        {
            float m = 1f;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                float d = DistanceToSegment(worldXZ, r.A, r.B);
                m *= math.smoothstep(r.Radius, r.Radius + math.max(r.Feather, 1e-3f), d);
            }
            return m;
        }

        /// <summary>
        /// The full <c>H(x,z) = A(r)·ridgedFBM·M</c> (spec §4.1) — the H1 field times the H3 mask.
        /// This is the canonical sample for consumers that must respect the corridor (SDF gen, Phase C).
        /// </summary>
        public static float SampleWithMask(float2 worldXZ, in WorldStructureConstants c,
                                           NativeArray<WorldStructureMaskRegion> regions)
            => WorldStructure.Sample(worldXZ, c) * Evaluate(worldXZ, regions);
    }
}
