namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Editor-facing description of a scatter visual bootstrap (tree / rock / pebble) so a single
    /// shared inspector can draw a "what's wired" status box, an enable toggle, and a one-click
    /// default-mesh wiring button without hard-coding each bootstrap's concrete field names.
    /// </summary>
    /// <remarks>
    /// Members return serialized field names (via <c>nameof</c>) rather than values — the editor
    /// resolves them through <c>SerializedProperty</c> so it can both read status and write the
    /// auto-wired meshes back. Returning strings keeps this interface free of any UnityEditor
    /// dependency, so it can live in the runtime assembly alongside the bootstraps it describes.
    /// All members are runtime-harmless (constant strings); they only carry meaning in the editor.
    /// </remarks>
    public interface ISurfaceScatterVisualBootstrap
    {
        /// <summary>Human-readable feature label, e.g. "Pebble Scatter".</summary>
        string ScatterDisplayName { get; }

        /// <summary>Serialized <c>bool</c> field that gates <c>Start()</c>; drawn as the enable toggle.</summary>
        string FeatureEnabledFieldName { get; }

        /// <summary>Serialized <c>Mesh[]</c> near-variant field — the status check and auto-wire target.</summary>
        string NearMeshVariantsFieldName { get; }

        /// <summary>
        /// Serialized single-<c>Mesh</c> legacy fallback field, or <c>null</c> when the bootstrap has none.
        /// Counts toward "mesh wired" so legacy-only setups don't false-alarm.
        /// </summary>
        string LegacySingleMeshFieldName { get; }

        /// <summary>Serialized <c>Mesh[]</c> far-LOD field (parallel to near). Empty/unwired is allowed.</summary>
        string FarMeshVariantsFieldName { get; }

        /// <summary>Serialized <c>Material</c> field.</summary>
        string MaterialFieldName { get; }

        /// <summary>Serialized <c>float</c> LOD swap-distance field (0 = swap disabled).</summary>
        string LodSwapDistanceFieldName { get; }

        /// <summary>
        /// Name of the <c>ProjectFeatureConfig</c> bool that enables this render system
        /// (e.g. "EnablePebbleRenderSystem"). Lets the status box warn when the scene wiring is on
        /// but the system that consumes it is gated off at the project level.
        /// </summary>
        string RenderSystemConfigFlagName { get; }

        /// <summary>
        /// Asset path the auto-wire button pulls near/far meshes from, or <c>null</c> to hide the button.
        /// </summary>
        string DefaultModelAssetPath { get; }
    }
}
