using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Editor
{
    /// <summary>
    /// Editor utility that generates the grass blade mesh used by GrassChunkRenderSystem.
    ///
    /// The mesh is a single vertical quad (1 unit tall, configurable width) with:
    ///   - UV.x = 0..1 left→right across the blade
    ///   - UV.y = 0..1 bottom (base) → top (tip)
    ///
    /// The vertex shader in GrassBlades.shader scales the Y axis by each blade's Height
    /// and applies Y-axis billboarding, so the mesh only needs to define the shape template.
    ///
    /// Saved to Assets/Resources/GrassBladeMesh.asset so GrassChunkGenerationSystem and
    /// GrassSystemSettings can reference it without an inspector assignment.
    /// </summary>
    public static class GrassBladeMeshBuilder
    {
        private const string MeshAssetPath  = "Assets/Resources/GrassBladeMesh.asset";
        private const float  BladeHalfWidth = 0.05f; // half-width in local space

        [MenuItem("DOTS Terrain/Build Grass Blade Mesh")]
        public static void BuildAndSave()
        {
            var mesh = BuildMesh();
            SaveMesh(mesh);
        }

        /// <summary>Creates the blade mesh in memory without saving to disk.</summary>
        public static Mesh BuildMesh()
        {
            // Single vertical quad:
            //   v2 (-w,1,0)  v3 (w,1,0)   ← tip
            //   v0 (-w,0,0)  v1 (w,0,0)   ← base (root)
            //
            // Origin at the base centre so the shader places it at WorldPosition.

            float w = BladeHalfWidth;

            var verts = new Vector3[]
            {
                new Vector3(-w, 0f, 0f),  // 0 base-left
                new Vector3( w, 0f, 0f),  // 1 base-right
                new Vector3(-w, 1f, 0f),  // 2 tip-left
                new Vector3( w, 1f, 0f),  // 3 tip-right
            };

            var uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };

            // Two triangles, both wound CCW when viewed from +Z.
            // Cull Off in the shader means winding doesn't determine visibility.
            var tris = new int[] { 0, 2, 1,  1, 2, 3 };

            var mesh = new Mesh
            {
                name = "GrassBladeMesh"
            };
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true); // mark read-only for GPU

            return mesh;
        }

        private static void SaveMesh(Mesh mesh)
        {
            // Ensure Resources folder exists.
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            // Replace existing asset if present.
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existing != null)
            {
                // Overwrite the existing asset so references in GrassSystemSettings are preserved.
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            }

            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[GrassBladeMeshBuilder] Saved grass blade mesh to {MeshAssetPath}");
        }
    }
}
