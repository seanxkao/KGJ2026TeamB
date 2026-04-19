using System;
using System.Drawing;
using UnityEngine;

public class ClawToy : MonoBehaviour
{
    public string Id
    {
        get; private set;
    } = "default";

    private GameObject modelObj = null;

    [SerializeField]
    private Renderer originRenderer;

    private Action<ClawToy> onHole;

    public void EnableJoint()
    {
        var joint = GetComponent<ConfigurableJoint>();
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
    }

    public void DisableJoint()
    {
        var joint = GetComponent<ConfigurableJoint>();
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;
    }

    public void SetModel(ModelData data)
    {
        Id = data.id;
        modelObj = Instantiate(data.model, transform);
        modelObj.transform.localPosition = data.clawToyPos;
        modelObj.transform.localScale = data.clawToySize * Vector3.one;
        originRenderer.enabled = false;
    }

    public void RegisterOnHole(Action<ClawToy> act)
    {
        onHole += act;
    }

    public void UnregisterOnHole(Action<ClawToy> act)
    {
        onHole -= act;
    }

    private void OnCollisionEnter(Collision collision)
    {
        var tag = collision.gameObject.tag;
        if (tag != "Hole")
        {
            return;
        }

        onHole?.Invoke(this);
        Destroy(gameObject);
    }
}
