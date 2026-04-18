using System.Collections.Generic;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 前一場景寫入、組裝場景讀取之一次性暫存清單。
    /// 進入 Play 時會重置，避免靜態資料在關閉 Domain Reload 時殘留到下一次測試。
    /// </summary>
    public static class AssemblyHandoffSession
    {
        static readonly List<AssemblyPartSpawnEntry> _entries = new List<AssemblyPartSpawnEntry>();
        static bool _hasFreshEntries;

        static AssemblyStateSnapshot _assemblySnapshot;
        static bool _hasFreshAssemblySnapshot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlayEnter()
        {
            _entries.Clear();
            _hasFreshEntries = false;
            _assemblySnapshot = null;
            _hasFreshAssemblySnapshot = false;
        }

        /// <summary>由前一場景於載入組裝場景前呼叫。</summary>
        public static void SetEntries(IEnumerable<AssemblyPartSpawnEntry> entries)
        {
            _entries.Clear();
            _hasFreshEntries = false;
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e == null) continue;
                _entries.Add(e);
            }
            _hasFreshEntries = _entries.Count > 0;
        }

        /// <summary>由組裝場景於載入下一場景前呼叫；與 <see cref="SetEntries"/> 為獨立通道。</summary>
        public static void SetAssemblySnapshot(AssemblyStateSnapshot snapshot)
        {
            _assemblySnapshot = snapshot;
            _hasFreshAssemblySnapshot = snapshot != null && snapshot.pieces != null && snapshot.pieces.Length > 0;
        }

        public static void ClearAssemblySnapshot()
        {
            _assemblySnapshot = null;
            _hasFreshAssemblySnapshot = false;
        }

        public static void Clear()
        {
            _entries.Clear();
            _hasFreshEntries = false;
            _assemblySnapshot = null;
            _hasFreshAssemblySnapshot = false;
        }

        public static bool TryConsumeEntries(List<AssemblyPartSpawnEntry> output)
        {
            if (!_hasFreshEntries || output == null) return false;

            var any = false;
            foreach (var entry in _entries)
            {
                if (entry == null || entry.Prefab == null || entry.Count <= 0) continue;
                output.Add(entry);
                any = true;
            }

            _entries.Clear();
            _hasFreshEntries = false;
            return any;
        }

        /// <summary>讀取並清空組裝快照（一次性）。</summary>
        public static bool TryConsumeAssemblySnapshot(out AssemblyStateSnapshot snapshot)
        {
            if (!_hasFreshAssemblySnapshot || _assemblySnapshot == null)
            {
                snapshot = null;
                return false;
            }

            snapshot = _assemblySnapshot;
            _assemblySnapshot = null;
            _hasFreshAssemblySnapshot = false;
            return true;
        }

        public static IReadOnlyList<AssemblyPartSpawnEntry> Entries => _entries;

        public static bool HasEntries => _hasFreshEntries && _entries.Count > 0;

        public static bool HasAssemblySnapshot => _hasFreshAssemblySnapshot && _assemblySnapshot != null;
    }
}
