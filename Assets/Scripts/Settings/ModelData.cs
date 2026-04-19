using System;
using UnityEngine;

[Serializable]
public class ModelData
{
    public string id;
    public GameObject model;
    public Vector3 clawToyPos = Vector3.zero;
    public float clawToySize = 1f;
}
