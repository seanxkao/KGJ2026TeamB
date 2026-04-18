using System;
using UnityEngine;

public class Claw : MonoBehaviour
{
    private Action<GameObject, Vector3> onCatch = null;

    public void RegisterOnCatch(Action<GameObject, Vector3> act)
    {
        onCatch += act;
    }

    public void UnregisterOnCatch(Action<GameObject, Vector3> act)
    {
        onCatch -= act;
    }

    private void OnTriggerEnter(Collider other)
    {
        var tag = other.gameObject.tag;
        if (tag != "Catchable")
        {
            onCatch?.Invoke(null, Vector3.zero);
        }
        var isHit = Physics.Raycast(transform.position, Vector3.down, out var rayHitInfo);
        if (!isHit)
        {
            Debug.LogError($"[Claw] Cannot detect hit!");
            onCatch?.Invoke(null, Vector3.zero);
            return;
        }
        onCatch?.Invoke(other.gameObject, rayHitInfo.point);
    }
}
