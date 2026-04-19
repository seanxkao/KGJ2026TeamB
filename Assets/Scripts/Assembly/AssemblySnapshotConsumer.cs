using System;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 置於「接收組裝快照」的場景中，於 <see cref="Awake"/> 觸發 <see cref="AssemblyHandoffSession.TryConsumeAssemblySnapshot"/>（暫存不會被清空，請自行 <see cref="AssemblyHandoffSession.ClearAssemblySnapshot"/>）。
    /// </summary>
    public sealed class AssemblySnapshotConsumer : MonoBehaviour
    {
        public event Action<AssemblyStateSnapshot> Received;

        void Awake()
        {
            if (AssemblyHandoffSession.TryConsumeAssemblySnapshot(out var snap))
                Received?.Invoke(snap);
        }
    }
}
