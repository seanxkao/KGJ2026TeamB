using System;
using UnityEngine;

public class TriggerEventSource : MonoBehaviour
{
    public event Action<Collider> TriggerEntered;

    private void OnTriggerEnter(Collider other)
    {
        TriggerEntered?.Invoke(other);
    }
}
