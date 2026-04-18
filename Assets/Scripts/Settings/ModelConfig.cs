using System.Collections.Generic;
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
}
