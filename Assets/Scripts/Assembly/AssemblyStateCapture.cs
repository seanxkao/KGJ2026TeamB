using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 從目前場景的 <see cref="AssemblyPiece"/> 與 <see cref="FixedJoint"/> 擷取快照（相對根姿態、有向拓樸）。
    /// 假設場上<strong>只有一坨</strong>：該場景內所有零件須透過關節接成單一連通群，否則擷取失敗並回傳 <c>null</c>。
    /// </summary>
    public static class AssemblyStateCapture
    {
        /// <summary>擷取 <paramref name="scene"/> 內零件；若 <paramref name="scene"/> 無效則使用作用中場景。</summary>
        public static AssemblyStateSnapshot TryCaptureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                scene = SceneManager.GetActiveScene();

            var allPieces = Object.FindObjectsByType<AssemblyPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            var pieces = new List<AssemblyPiece>(allPieces.Length);
            for (var i = 0; i < allPieces.Length; i++)
            {
                var p = allPieces[i];
                if (p != null && p.gameObject.scene == scene)
                    pieces.Add(p);
            }

            var n = pieces.Count;
            if (n == 0)
                return null;

            var rbToIdx = new Dictionary<Rigidbody, int>(n);
            var rigidbodies = new Rigidbody[n];
            for (var i = 0; i < n; i++)
            {
                var rb = pieces[i].EnsureRuntimeRigidbody();
                rigidbodies[i] = rb;
                rbToIdx[rb] = i;
            }

            var adj = new List<int>[n];
            for (var i = 0; i < n; i++)
                adj[i] = new List<int>(4);

            var allJoints = Object.FindObjectsByType<FixedJoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var j = 0; j < allJoints.Length; j++)
            {
                var fj = allJoints[j];
                if (fj == null) continue;
                var a = GetRigidbodyForJoint(fj);
                var b = fj.connectedBody;
                if (a == null || b == null || a == b) continue;
                if (!rbToIdx.TryGetValue(a, out var ia)) continue;
                if (!rbToIdx.TryGetValue(b, out var ib)) continue;
                adj[ia].Add(ib);
                adj[ib].Add(ia);
            }

            var visited = new bool[n];
            var comp = new List<int>(n);
            DepthFirstCollect(0, adj, visited, comp);
            if (comp.Count != n)
            {
                Debug.LogWarning(
                    $"[AssemblyStateCapture] 場景 {scene.name} 有 {n} 個 AssemblyPiece，預期全部接成一坨，但從索引 0 僅連到 {comp.Count} 件（可能有多坨或未接合）。已略過擷取。",
                    pieces.Count > 0 ? pieces[0] : null);
                return null;
            }

            comp.Sort();
            var k = n;

            var remap = new int[n];
            for (var i = 0; i < n; i++)
                remap[i] = -1;
            for (var ni = 0; ni < k; ni++)
                remap[comp[ni]] = ni;

            var inComp = new HashSet<int>(comp);

            var rootOld = comp[0];
            var bestDist = float.PositiveInfinity;
            for (var t = 0; t < comp.Count; t++)
            {
                var idx = comp[t];
                var d = rigidbodies[idx].position.sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    rootOld = idx;
                }
            }

            var rootRb = rigidbodies[rootOld];
            var rootPos = rootRb.position;
            var rootRot = rootRb.rotation;
            var invRootRot = Quaternion.Inverse(rootRot);

            var pieceRecords = new AssemblyPieceSnapshotRecord[k];
            for (var ni = 0; ni < k; ni++)
            {
                var oi = comp[ni];
                var rb = rigidbodies[oi];
                var tr = pieces[oi].transform;
                pieceRecords[ni] = new AssemblyPieceSnapshotRecord
                {
                    modelId = pieces[oi].GetCatalogId(),
                    instanceGuid = pieces[oi].GetInstanceID().ToString(),
                    localPosition = invRootRot * (rb.position - rootPos),
                    localRotation = invRootRot * rb.rotation,
                    worldPosition = rb.position,
                    worldRotation = rb.rotation,
                    localScale = tr.localScale,
                };
            }

            var rootNew = remap[rootOld];
            var jointEdges = CollectDirectedJointEdges(allJoints, rbToIdx, inComp, remap);

            return new AssemblyStateSnapshot
            {
                formatVersion = 2,
                rootIndex = rootNew,
                pieces = pieceRecords,
                joints = jointEdges.ToArray(),
            };
        }

        /// <summary>擷取目前作用中場景。</summary>
        public static AssemblyStateSnapshot TryCaptureActiveScene() =>
            TryCaptureScene(SceneManager.GetActiveScene());

        static void DepthFirstCollect(int start, List<int>[] adj, bool[] visited, List<int> comp)
        {
            var stack = new Stack<int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var u = stack.Pop();
                if (visited[u]) continue;
                visited[u] = true;
                comp.Add(u);
                var nb = adj[u];
                for (var i = 0; i < nb.Count; i++)
                {
                    var v = nb[i];
                    if (!visited[v])
                        stack.Push(v);
                }
            }
        }

        static List<AssemblyJointEdgeRecord> CollectDirectedJointEdges(
            FixedJoint[] allJoints,
            Dictionary<Rigidbody, int> rbToIdx,
            HashSet<int> inComp,
            int[] remap)
        {
            var list = new List<AssemblyJointEdgeRecord>();
            var seen = new HashSet<long>();

            for (var j = 0; j < allJoints.Length; j++)
            {
                var fj = allJoints[j];
                if (fj == null) continue;
                var ownerRb = GetRigidbodyForJoint(fj);
                var connRb = fj.connectedBody;
                if (ownerRb == null || connRb == null) continue;
                if (!rbToIdx.TryGetValue(ownerRb, out var oOld)) continue;
                if (!rbToIdx.TryGetValue(connRb, out var cOld)) continue;
                if (!inComp.Contains(oOld) || !inComp.Contains(cOld)) continue;

                var oNew = remap[oOld];
                var cNew = remap[cOld];
                var key = ((long)oNew << 32) | (uint)cNew;
                if (!seen.Add(key)) continue;

                list.Add(new AssemblyJointEdgeRecord
                {
                    ownerIndex = oNew,
                    connectedIndex = cNew,
                });
            }

            return list;
        }

        /// <summary>
        /// 關節元件掛載之剛體（與 <see cref="AssemblyHoldAndSnapController"/> 於同物件上加 <see cref="FixedJoint"/> 一致）。
        /// 部分 Unity 版本之 <see cref="Joint"/> 無 <c>attachedRigidbody</c> 欄位，故以此相容。
        /// </summary>
        static Rigidbody GetRigidbodyForJoint(Joint joint)
        {
            if (joint == null) return null;
            return joint.GetComponent<Rigidbody>() ?? joint.GetComponentInParent<Rigidbody>();
        }
    }
}
