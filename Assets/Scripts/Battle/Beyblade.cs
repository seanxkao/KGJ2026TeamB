using UnityEngine;

public class Beyblade : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _rb.angularVelocity = new Vector3(0, 100, 0);
    }
}
