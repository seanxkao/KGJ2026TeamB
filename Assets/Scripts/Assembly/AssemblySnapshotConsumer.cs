using System;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 置於「接收組裝快照」的場景中，於 <see cref="Awake"/> 一次性 <see cref="AssemblyHandoffSession.TryConsumeAssemblySnapshot"/>。
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
