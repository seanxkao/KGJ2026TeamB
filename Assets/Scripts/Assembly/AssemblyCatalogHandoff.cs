using System.Collections.Generic;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 前一場景：由 <see cref="ModelConfig"/> 建立進場清單並寫入 <see cref="AssemblyHandoffSession"/>（須在 <c>LoadScene</c> 之前呼叫）。
    /// </summary>
    public static class AssemblyCatalogHandoff
    {
        public static void SetSpawnEntriesFromModelCatalog(ModelConfig catalog, IEnumerable<(string modelId, int count)> items)
        {
            var list = new List<AssemblyPartSpawnEntry>();
            if (catalog != null && items != null)
                catalog.AppendSpawnEntriesFromCatalogIds(list, items);
            AssemblyHandoffSession.SetEntries(list);
        }
    }
}
