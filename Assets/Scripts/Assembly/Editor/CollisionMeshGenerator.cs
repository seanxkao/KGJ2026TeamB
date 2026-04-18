using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace KGJ.AssemblyScene.EditorTools
{
    /// <summary>
    /// 在 Project 視窗選一顆 Mesh（或選一個帶有 MeshFilter / SkinnedMeshRenderer 的 GameObject）之後
    /// 透過選單產生一顆低面數 convex collision mesh，存成 .asset。
    /// 目標三角形數 256（MeshCollider convex 的硬限制）。
    /// </summary>
    public static class CollisionMeshGenerator
    {
        const int TargetTriangleCount = 256;
        const string OutputFolder = "Assets/GeneratedColliders";

        [MenuItem("KGJ/Assembly/Generate Low-Poly Collision Mesh", priority = 100)]
        static void GenerateFromSelection()
        {
            var source = ResolveSourceMesh(out var defaultName);
            if (source == null)
            {
                EditorUtility.DisplayDialog(
                    "Generate Collision Mesh",
                    "請先在 Project 視窗選一顆 Mesh，或在 Hierarchy 選一個含 MeshFilter / SkinnedMeshRenderer 的 GameObject。",
                    "OK");
                return;
            }

            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            var tris = source.triangles.Length / 3;
            var ratio = tris <= TargetTriangleCount ? 1f : (float)TargetTriangleCount / tris;

            var simplifier = new MeshSimplifier();
            simplifier.Initialize(source);
            simplifier.SimplifyMesh(ratio);
            var simplified = simplifier.ToMesh();
            simplified.name = defaultName + "_collision";
            simplified.RecalculateBounds();

            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{simplified.name}.asset");
            AssetDatabase.CreateAsset(simplified, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

            Debug.Log(
                $"[CollisionMeshGenerator] 原 {tris} 三角形 → 降至 {simplified.triangles.Length / 3}。輸出：{path}\n" +
                "請把 Prefab 上 MeshCollider.sharedMesh 指到這顆 asset，並確保 Convex=true。");
        }

        static Mesh ResolveSourceMesh(out string defaultName)
        {
            defaultName = "mesh";

            var mesh = Selection.activeObject as Mesh;
            if (mesh != null)
            {
                defaultName = mesh.name;
                return mesh;
            }

            var go = Selection.activeGameObject;
            if (go == null) return null;

            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                defaultName = mf.sharedMesh.name;
                return mf.sharedMesh;
            }

            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                defaultName = smr.sharedMesh.name;
                return baked;
            }

            return null;
        }
    }
}
