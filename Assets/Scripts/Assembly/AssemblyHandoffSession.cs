using System.Collections.Generic;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 前一場景寫入、組裝場景讀取之暫存清單（不實作載入失敗 fallback）。
    /// </summary>
    public static class AssemblyHandoffSession
    {
        static readonly List<AssemblyPartSpawnEntry> _entries = new List<AssemblyPartSpawnEntry>();

        /// <summary>由前一場景於載入組裝場景前呼叫。</summary>
        public static void SetEntries(IEnumerable<AssemblyPartSpawnEntry> entries)
        {
            _entries.Clear();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e == null) continue;
                _entries.Add(e);
            }
        }

        public static void Clear() => _entries.Clear();

        public static IReadOnlyList<AssemblyPartSpawnEntry> Entries => _entries;

        public static bool HasEntries => _entries.Count > 0;
    }
}
