using UnityEngine;

public class Beyblade : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private bool _isSpinning;

    public string DisplayName => gameObject.name;

    private void Awake()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    private void Update()
    {
        if (!_isSpinning || _rb == null)
        {
            return;
        }

        _rb.angularVelocity = new Vector3(0, 100, 0);
    }

    public void BeginBattle()
    {
        _isSpinning = true;
    }

    public void EndBattle()
    {
        _isSpinning = false;

        if (_rb == null)
        {
            return;
        }

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void ResetState()
    {
        EndBattle();

        transform.SetPositionAndRotation(_initialPosition, _initialRotation);

        if (_rb == null)
        {
            return;
        }

        _rb.position = _initialPosition;
        _rb.rotation = _initialRotation;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }
}
