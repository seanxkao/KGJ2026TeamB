using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 依快照與 <see cref="ModelConfig"/> 在執行期實例化並重建 <see cref="FixedJoint"/>（與 <c>AssemblyHoldAndSnapController</c> 之接合同參數）。
    /// </summary>
    public static class AssemblyStateRestore
    {
        /// <summary>
        /// 以 <paramref name="rootWorldPosition"/>／<paramref name="rootWorldRotation"/> 作為快照根件的世界姿態，還原其餘件之相對關係。
        /// </summary>
        public static GameObject[] TryInstantiateSnapshot(
            AssemblyStateSnapshot snapshot,
            ModelConfig catalog,
            Vector3 rootWorldPosition,
            Quaternion rootWorldRotation,
            Transform parent = null)
        {
            if (snapshot == null || catalog == null || snapshot.pieces == null || snapshot.pieces.Length == 0)
                return null;

            var n = snapshot.pieces.Length;
            var objects = new GameObject[n];
            var rigidbodies = new Rigidbody[n];

            for (var i = 0; i < n; i++)
            {
                var rec = snapshot.pieces[i];
                var data = catalog.GetDataById(rec.modelId);
                if (data == null || data.model == null)
                {
                    Debug.LogWarning($"[AssemblyStateRestore] 找不到 modelId「{rec.modelId}」，已中止還原。");
                    DestroySpawned(objects, i);
                    return null;
                }

                var worldPos = rootWorldPosition + rootWorldRotation * rec.localPosition;
                var worldRot = rootWorldRotation * rec.localRotation;
                var go = Object.Instantiate(data.model, worldPos, worldRot, parent);
                var scale = rec.localScale;
                if (scale.sqrMagnitude < 1e-8f)
                    scale = Vector3.one;
                go.transform.localScale = scale;
                var piece = go.GetComponent<AssemblyPiece>();
                if (piece == null)
                    piece = go.AddComponent<AssemblyPiece>();
                var rb = piece.EnsureRuntimeRigidbody();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                rigidbodies[i] = rb;
                objects[i] = go;
            }

            Physics.SyncTransforms();

            if (snapshot.joints != null)
            {
                for (var j = 0; j < snapshot.joints.Length; j++)
                {
                    var e = snapshot.joints[j];
                    if ((uint)e.ownerIndex >= (uint)n || (uint)e.connectedIndex >= (uint)n)
                        continue;
                    var owner = rigidbodies[e.ownerIndex];
                    var target = rigidbodies[e.connectedIndex];
                    if (owner != null && target != null)
                        EnsureFixedJointConnection(owner, target);
                }
            }

            for (var i = 0; i < n; i++)
            {
                var rb = rigidbodies[i];
                if (rb != null)
                    rb.isKinematic = false;
            }

            Physics.SyncTransforms();
            return objects;
        }

        static void DestroySpawned(GameObject[] objects, int count)
        {
            for (var j = 0; j < count; j++)
            {
                if (objects[j] != null)
                    Object.Destroy(objects[j]);
            }
        }

        static void EnsureFixedJointConnection(Rigidbody owner, Rigidbody target)
        {
            if (owner == null || target == null || owner == target) return;

            var joints = owner.GetComponents<FixedJoint>();
            for (var i = 0; i < joints.Length; i++)
            {
                var joint = joints[i];
                if (joint != null && joint.connectedBody == target)
                    return;
            }

            var newJoint = owner.gameObject.AddComponent<FixedJoint>();
            newJoint.connectedBody = target;
            newJoint.enableCollision = false;
            newJoint.breakForce = Mathf.Infinity;
            newJoint.breakTorque = Mathf.Infinity;
        }
    }
}
