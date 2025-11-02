// WFCPlaceholderPrefabGenerator.cs
// Place this file anywhere under an "Editor" folder (e.g., Assets/Editor/)
// Menu: Tools/WFC/Generate Placeholders
// Generates lightweight, orientation-obvious prefabs under: Assets/Prefabs/WFCPlaceholders/

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class WFCPlaceholderPrefabGenerator
{
    private const string RootFolder = "Assets/Prefabs/WFCPlaceholders";
    private const string MaterialsFolder = RootFolder + "/Materials";

    [MenuItem("Tools/WFC/Generate Placeholders")]
    public static void GenerateAll()
    {
        EnsureFolders();
        // Materials
        var matFloor    = CreateOrLoadMaterial("Floor_Mat",    new Color(0.55f, 0.55f, 0.55f));
        var matWall     = CreateOrLoadMaterial("Wall_Mat",     new Color(0.85f, 0.25f, 0.25f));
        var matCorner   = CreateOrLoadMaterial("Corner_Mat",   new Color(0.25f, 0.45f, 0.85f));
        var matCorridor = CreateOrLoadMaterial("Corridor_Mat", new Color(0.90f, 0.85f, 0.20f));
        var matDoor     = CreateOrLoadMaterial("Door_Mat",     new Color(0.20f, 0.80f, 0.40f));
        var matCue      = CreateOrLoadMaterial("Cue_Mat",      Color.white);

        // Build prefabs
        CreateFloorPrefab(matFloor, matCue);
        CreateWallPrefab(matWall, matCue);
        CreateCornerPrefab(matCorner);
        CreateCorridorPrefab(matCorridor);
        CreateDoorPrefab(matDoor, matCue);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WFC placeholder prefabs generated at: " + RootFolder);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "WFCPlaceholders");

        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder(RootFolder, "Materials");
    }

    private static Material CreateOrLoadMaterial(string name, Color color)
    {
        var path = $"{MaterialsFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }

        // Prefer URP Lit if present; fallback to Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void CreateFloorPrefab(Material matBase, Material matCue)
    {
        var root = new GameObject("Floor_Placeholder");
        try
        {
            // Plane base
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plane";
            plane.transform.SetParent(root.transform, false);
            // Unity plane is 10x10; scale to 1x1 world units for a single cell
            plane.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            ApplyMaterial(plane, matBase);
            StripColliders(plane);

            // Orientation cue: small cylinder at +Z edge
            var cue = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cue.name = "ZCue";
            cue.transform.SetParent(root.transform, false);
            cue.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            cue.transform.localPosition = new Vector3(0f, 0.055f, 0.45f);
            ApplyMaterial(cue, matCue);
            StripColliders(cue);

            SaveAsPrefab(root, $"{RootFolder}/Floor_Placeholder.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateWallPrefab(Material matBase, Material matCue)
    {
        var root = new GameObject("Wall_Placeholder");
        try
        {
            // Base slab (x=1, z=0.2)
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(root.transform, false);
            slab.transform.localScale = new Vector3(1f, 1f, 0.2f);
            ApplyMaterial(slab, matBase);
            StripColliders(slab);

            // Small protrusion towards +Z to signal "front"
            var front = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.name = "FrontMarker";
            front.transform.SetParent(root.transform, false);
            front.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            front.transform.localPosition = new Vector3(0f, 0.575f, 0.6f);
            ApplyMaterial(front, matCue);
            StripColliders(front);

            SaveAsPrefab(root, $"{RootFolder}/Wall_Placeholder.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateCornerPrefab(Material matBase)
    {
        var root = new GameObject("Corner_Placeholder");
        try
        {
            // Arm A (along Z)
            var armA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armA.name = "ArmZ";
            armA.transform.SetParent(root.transform, false);
            armA.transform.localScale = new Vector3(1f, 1f, 0.2f);
            armA.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            ApplyMaterial(armA, matBase);
            StripColliders(armA);

            // Arm B (along X)
            var armB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armB.name = "ArmX";
            armB.transform.SetParent(root.transform, false);
            armB.transform.localScale = new Vector3(0.2f, 1f, 1f);
            armB.transform.localPosition = new Vector3(0.4f, 0f, 0f);
            ApplyMaterial(armB, matBase);
            StripColliders(armB);

            SaveAsPrefab(root, $"{RootFolder}/Corner_Placeholder.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateCorridorPrefab(Material matBase)
    {
        var root = new GameObject("Corridor_Placeholder");
        try
        {
            // Left wall (along Z)
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "WallLeft";
            left.transform.SetParent(root.transform, false);
            left.transform.localScale = new Vector3(0.2f, 1f, 1f);
            left.transform.localPosition = new Vector3(-0.4f, 0f, 0f);
            ApplyMaterial(left, matBase);
            StripColliders(left);

            // Right wall (along Z)
            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "WallRight";
            right.transform.SetParent(root.transform, false);
            right.transform.localScale = new Vector3(0.2f, 1f, 1f);
            right.transform.localPosition = new Vector3(0.4f, 0f, 0f);
            ApplyMaterial(right, matBase);
            StripColliders(right);

            SaveAsPrefab(root, $"{RootFolder}/Corridor_Placeholder.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateDoorPrefab(Material matBase, Material matCue)
    {
        var root = new GameObject("Door_Placeholder");
        try
        {
            // Corridor walls
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "WallLeft";
            left.transform.SetParent(root.transform, false);
            left.transform.localScale = new Vector3(0.2f, 1f, 1f);
            left.transform.localPosition = new Vector3(-0.4f, 0f, 0f);
            ApplyMaterial(left, matBase);
            StripColliders(left);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "WallRight";
            right.transform.SetParent(root.transform, false);
            right.transform.localScale = new Vector3(0.2f, 1f, 1f);
            right.transform.localPosition = new Vector3(0.4f, 0f, 0f);
            ApplyMaterial(right, matBase);
            StripColliders(right);

            // Door bar: cylinder spanning left→right (align along local X)
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "DoorBar";
            bar.transform.SetParent(root.transform, false);
            bar.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f); // height=Y=0.5 → spans about 1 unit after rotation
            bar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // rotate so cylinder's length goes along X
            ApplyMaterial(bar, matCue);
            StripColliders(bar);

            SaveAsPrefab(root, $"{RootFolder}/Door_Placeholder.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ApplyMaterial(GameObject go, Material mat)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
    }

    private static void StripColliders(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(col);
        }
    }

    private static void SaveAsPrefab(GameObject root, string assetPath)
    {
        // Ensure directory exists (for safety in case user moved folders)
        var dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            var parts = dir.Split('/');
            var build = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder($"{build}/{parts[i]}"))
                    AssetDatabase.CreateFolder(build, parts[i]);
                build += "/" + parts[i];
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
    }
}
#endif
