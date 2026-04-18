using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 單一可組裝零件在進場時的生成設定（清單元素）。
    /// </summary>
    [System.Serializable]
    public sealed class AssemblyPartSpawnEntry
    {
        [Tooltip("場上實例由此 Prefab 拉出（Instantiate）；請在 Prefab 上設定好 Mesh／材質／Collider。Rigidbody 會在 play mode 自動補上。")]
        public GameObject Prefab;

        [Min(0)]
        public int Count = 1;

        [Min(0f)]
        [Tooltip("初始排版使用的水平占地；0 代表自動依 Prefab 根節點 scale 推估，不用原始模型 bounds。")]
        public float SpawnFootprint = 0f;
    }
}
