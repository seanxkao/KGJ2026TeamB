using System.Collections.Generic;
using KGJ.AssemblyScene;
using UnityEngine;

[CreateAssetMenu(fileName = "ModelConfig", menuName = "Scriptable Objects/ModelConfig")]
public class ModelConfig : ScriptableObject
{
    [SerializeField]
    private List<ModelData> models;

    public ModelData GetDataById(string id)
    {
        foreach (var data in models)
        {
            if (data.id == id)
            {
                return data;
            }
        }
        return null;
    }

    public List<ModelData> GetAllModels()
    {
        return new List<ModelData>(models);
    }
    /// <summary>將型錄 id 與數量轉成 <see cref="AssemblyPartSpawnEntry"/> 並附加至 <paramref name="destination"/>。</summary>
    public void AppendSpawnEntriesFromCatalogIds(List<AssemblyPartSpawnEntry> destination, IEnumerable<(string modelId, int count)> items)
    {
        if (destination == null || items == null)
            return;

        foreach (var (modelId, count) in items)
        {
            if (string.IsNullOrEmpty(modelId) || count <= 0)
                continue;

            var data = GetDataById(modelId);
            if (data == null || data.model == null)
            {
                Debug.LogWarning($"[ModelConfig] 找不到 id「{modelId}」或 model 為空，已跳過。", this);
                continue;
            }

            destination.Add(new AssemblyPartSpawnEntry
            {
                Prefab = data.model,
                Count = count,
                CatalogId = modelId,
            });
        }
    }
}
