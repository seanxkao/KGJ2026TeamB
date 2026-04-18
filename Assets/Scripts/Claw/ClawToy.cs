using System.Drawing;
using UnityEngine;

public class ClawToy : MonoBehaviour
{
    public void EnableJoint()
    {
        var joint = GetComponent<ConfigurableJoint>();
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
    }
}
