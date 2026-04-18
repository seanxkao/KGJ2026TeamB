using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 可組裝零件標記；Collider 可在本物件或子物件（含 MeshCollider／BoxCollider），
    /// <see cref="AssemblyHoldAndSnapController"/> 會以階層搜尋。執行期會自動補上 <see cref="Rigidbody"/>，
    /// 讓 prefab 資產本身不必保存剛體元件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyPiece : MonoBehaviour
    {
        [Tooltip("與 ModelConfig 條目 id 一致時可填；否則快照會用執行期 CatalogId 或物件名稱推斷。")]
        [SerializeField] string _catalogId;

        string _runtimeCatalogId;

        [SerializeField, Min(0.0001f)] float _mass = 1f;
        [SerializeField, Min(0f)] float _linearDamping = 0.05f;
        [SerializeField, Min(0f)] float _angularDamping = 0.05f;
        [SerializeField] bool _useGravity = true;
        [SerializeField] bool _isKinematic;
        [SerializeField] RigidbodyInterpolation _interpolation = RigidbodyInterpolation.None;
        [SerializeField] RigidbodyConstraints _constraints = RigidbodyConstraints.None;
        [SerializeField] CollisionDetectionMode _collisionDetection = CollisionDetectionMode.ContinuousDynamic;

        void Awake()
        {
            if (!Application.isPlaying) return;
            EnsureRuntimeRigidbody();
        }

        public Rigidbody EnsureRuntimeRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            ApplyRuntimeSettings(rb);
            return rb;
        }

        /// <summary>由 <see cref="AssemblySceneBootstrap"/> 或型錄進場流程設定，優先於 Prefab 上填寫的型錄 id。</summary>
        public void SetRuntimeCatalogId(string catalogId) => _runtimeCatalogId = catalogId;

        public string GetCatalogId()
        {
            if (!string.IsNullOrEmpty(_runtimeCatalogId))
                return _runtimeCatalogId;
            if (!string.IsNullOrEmpty(_catalogId))
                return _catalogId;
            var n = gameObject.name;
            const string suffix = "(Clone)";
            if (n.EndsWith(suffix, System.StringComparison.Ordinal))
                return n.Substring(0, n.Length - suffix.Length).Trim();
            return n;
        }

        void ApplyRuntimeSettings(Rigidbody rb)
        {
            if (rb == null) return;

            rb.mass = Mathf.Max(0.0001f, _mass);
            rb.linearDamping = Mathf.Max(0f, _linearDamping);
            rb.angularDamping = Mathf.Max(0f, _angularDamping);
            rb.useGravity = _useGravity;
            rb.isKinematic = _isKinematic;
            rb.interpolation = _interpolation;
            rb.constraints = _constraints;
            rb.collisionDetectionMode = _collisionDetection;
        }
    }
}
