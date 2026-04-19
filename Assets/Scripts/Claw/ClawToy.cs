using System;
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

    public void SetModel(ModelData data, float resize = 0f)
    {
        Id = data.id;
        modelObj = Instantiate(data.model, transform);
        modelObj.transform.localPosition = data.clawToyPos;
        if (resize > 0f)
        {
            modelObj.transform.localScale = resize * Vector3.one;
        }
        else
        {
            modelObj.transform.localScale = data.clawToySize * Vector3.one;
        }
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
