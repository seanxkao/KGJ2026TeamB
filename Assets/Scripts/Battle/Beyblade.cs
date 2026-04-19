using System.Collections.Generic;
using UnityEngine;

public enum BeybladeAnchorType
{
    Center,
    Top,
    Bottom,
}

public class Beyblade : MonoBehaviour
{
    [SerializeField]
    private Rigidbody _rb;

    [SerializeField]
    private Transform _centerAnchor;

    [SerializeField]
    private Transform _topAnchor;

    [SerializeField]
    private Transform _bottomAnchor;

    [SerializeField]
    private float _spin;
    [SerializeField]
    private float _recover;

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Transform _initialParent;
    private bool _isSpinning;
    private float _currentSpin;
    private string _displayName;
    private readonly List<GameObject> _spawnedAttachments = new();

    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;

    private void Awake()
    {
        _initialParent = transform.parent;
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _currentSpin = _spin;
    }

    private void FixedUpdate()
    {
        if (!_isSpinning || _rb == null)
        {
            return;
        }

        _rb.angularVelocity = transform.up * _currentSpin;

        var roll = Vector3.Cross(transform.up, Vector3.up);
        _rb.AddTorque(roll * _ActualRecover());
    }

    private float _ActualRecover()
    {
        return Mathf.Max(_recover - Time.time * 10, 0f);
    }

    public void BeginBattle()
    {
        _isSpinning = true;
    }

    public void SetDisplayName(string displayName)
    {
        _displayName = displayName;
    }

    public void SetPreviewSpin(float spinSpeed)
    {
        _currentSpin = spinSpeed;
    }

    public void Launch(Vector3 launchVelocity)
    {
        if (_rb == null)
        {
            return;
        }

        _rb.linearVelocity = launchVelocity;
    }

    public void AttachToLauncher(Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        transform.SetParent(anchor, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (_rb == null)
        {
            return;
        }

        _rb.position = transform.position;
        _rb.rotation = transform.rotation;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
    }

    public void DetachFromLauncher()
    {
        transform.SetParent(_initialParent, true);

        if (_rb == null)
        {
            return;
        }

        _rb.isKinematic = false;
        _rb.position = transform.position;
        _rb.rotation = transform.rotation;
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
        transform.SetParent(_initialParent, true);
        _currentSpin = _spin;

        if (_rb == null)
        {
            return;
        }

        _rb.isKinematic = false;
        _rb.position = _initialPosition;
        _rb.rotation = _initialRotation;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    public void Build(BeybladeAttachmentConfig[] attachments)
    {
        ClearAttachments();

        if (attachments == null)
        {
            return;
        }

        foreach (var attachment in attachments)
        {
            Attach(attachment);
        }
    }

    public void Attach(BeybladeAttachmentConfig attachment)
    {
        if (attachment == null || attachment.Prefab == null)
        {
            return;
        }

        var anchor = GetAnchor(attachment.Anchor);
        var instance = Instantiate(attachment.Prefab, anchor);
        instance.transform.localPosition = attachment.LocalPosition;
        instance.transform.localRotation = Quaternion.Euler(attachment.LocalEulerAngles);
        instance.transform.localScale = attachment.LocalScale;
        _spawnedAttachments.Add(instance);
    }

    public void ClearAttachments()
    {
        foreach (var spawnedAttachment in _spawnedAttachments)
        {
            if (spawnedAttachment == null)
            {
                continue;
            }

            Destroy(spawnedAttachment);
        }

        _spawnedAttachments.Clear();
    }

    private Transform GetAnchor(BeybladeAnchorType anchorType)
    {
        return anchorType switch
        {
            BeybladeAnchorType.Top => _topAnchor != null ? _topAnchor : transform,
            BeybladeAnchorType.Bottom => _bottomAnchor != null ? _bottomAnchor : transform,
            _ => _centerAnchor != null ? _centerAnchor : transform,
        };
    }
}
