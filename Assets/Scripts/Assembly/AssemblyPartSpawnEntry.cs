using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 單一可組裝零件在進場時的生成設定（清單元素）。
    /// </summary>
    [System.Serializable]
    public sealed class AssemblyPartSpawnEntry
    {
        [Tooltip("場上實例由此 Prefab 拉出（Instantiate）；請在 Prefab 上設定好 Mesh／材質／Rigidbody／Collider。")]
        public GameObject Prefab;

        [Min(0)]
        public int Count = 1;
    }
}
