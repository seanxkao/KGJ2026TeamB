using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 從目前場景的 <see cref="AssemblyPiece"/> 與 <see cref="FixedJoint"/> 擷取單一連通群組之快照（相對根姿態、有向拓樸）。
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

            var bestComp = FindLargestComponent(n, adj);
            if (bestComp == null || bestComp.Count == 0)
                return null;

            bestComp.Sort();
            var k = bestComp.Count;
            if (k < n)
            {
                Debug.LogWarning(
                    $"[AssemblyStateCapture] 場景 {scene.name} 有 {n} 個零件，僅匯出最大連通群（{k} 件），其餘未接合者已略過。");
            }

            var remap = new int[n];
            for (var i = 0; i < n; i++)
                remap[i] = -1;
            for (var ni = 0; ni < k; ni++)
                remap[bestComp[ni]] = ni;

            var inComp = new HashSet<int>(bestComp);

            var rootOld = bestComp[0];
            var bestDist = float.PositiveInfinity;
            for (var t = 0; t < bestComp.Count; t++)
            {
                var idx = bestComp[t];
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
                var oi = bestComp[ni];
                var rb = rigidbodies[oi];
                pieceRecords[ni] = new AssemblyPieceSnapshotRecord
                {
                    modelId = pieces[oi].GetCatalogId(),
                    instanceGuid = pieces[oi].GetInstanceID().ToString(),
                    localPosition = invRootRot * (rb.position - rootPos),
                    localRotation = invRootRot * rb.rotation,
                };
            }

            var rootNew = remap[rootOld];
            var jointEdges = CollectDirectedJointEdges(allJoints, rbToIdx, inComp, remap);

            return new AssemblyStateSnapshot
            {
                formatVersion = 1,
                rootIndex = rootNew,
                pieces = pieceRecords,
                joints = jointEdges.ToArray(),
            };
        }

        /// <summary>擷取目前作用中場景。</summary>
        public static AssemblyStateSnapshot TryCaptureActiveScene() =>
            TryCaptureScene(SceneManager.GetActiveScene());

        static List<int> FindLargestComponent(int n, List<int>[] adj)
        {
            var visited = new bool[n];
            List<int> best = null;

            for (var i = 0; i < n; i++)
            {
                if (visited[i]) continue;
                var comp = new List<int>();
                DepthFirstCollect(i, adj, visited, comp);
                if (best == null || comp.Count > best.Count)
                    best = comp;
            }

            return best;
        }

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
