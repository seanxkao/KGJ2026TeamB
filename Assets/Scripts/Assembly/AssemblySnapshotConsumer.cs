using System;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 置於對戰等場景：【階段二 · 組裝→對戰】於 <see cref="Awake"/> 讀取 <see cref="MainFlowManager.TryGetSnapshot"/>（不會自動清空，請自行 <see cref="MainFlowManager.ClearSnapshot"/>）。
    /// </summary>
    public sealed class AssemblySnapshotConsumer : MonoBehaviour
    {
        public event Action<AssemblyStateSnapshot> Received;

        void Awake()
        {
            var m = MainFlowManager.Instance;
            if (m != null && m.TryGetSnapshot(out var snap))
                Received?.Invoke(snap);
        }
    }
}
