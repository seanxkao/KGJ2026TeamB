using System;
using UnityEngine;

[Serializable]
public struct LaunchData
{
    public Vector3 LaunchVelocity;
    public float SpinSpeed;
}

public class Launcher : MonoBehaviour
{
    [SerializeField]
    private Transform _beybladeAnchor;

    [SerializeField]
    private Transform _handleTransform;

    [SerializeField]
    private Transform _handleRestPoint;

    [SerializeField]
    private Transform _handlePullEndPoint;

    [SerializeField]
    private Collider _handleCollider;

    [SerializeField, Min(0f)]
    private float _launchForce = 3f;

    [SerializeField, Min(0f)]
    private float _maxSpinSpeed = 100f;

    public event Action<LaunchData> LaunchRequested;
    public bool IsReadyToLaunch => !_hasLaunched;

    private Beyblade _loadedBeyblade;
    private bool _isDragging;
    private bool _hasLaunched;
    private Quaternion _handleLocalRotationOffset = Quaternion.identity;

    private void Awake()
    {
        _handleLocalRotationOffset = GetHandleRotationOffset();
        ResetHandleVisual();
    }

    private void Update()
    {
        if (_hasLaunched)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && IsHandlePressed())
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            UpdateDrag();
        }

        if (_isDragging && Input.GetMouseButtonUp(0))
        {
            CancelDrag();
        }
    }

    public void LoadBeyblade(Beyblade beyblade)
    {
        _loadedBeyblade = beyblade;
        _hasLaunched = false;
        _isDragging = false;
        ResetHandleVisual();

        if (_loadedBeyblade == null || _beybladeAnchor == null)
        {
            return;
        }

        _loadedBeyblade.AttachToLauncher(_beybladeAnchor);
        _loadedBeyblade.SetPreviewSpin(0f);
    }

    public void SetPullFromSensor(float normalized)
    {
        Debug.Log($"[Launcher] SetPullFromSensor normalized={normalized} hasLaunched={_hasLaunched}");
        SetHandlePull(normalized);
        if (_hasLaunched) return;
        if (_loadedBeyblade != null)
            _loadedBeyblade.SetPreviewSpin(_maxSpinSpeed * normalized);
        if (normalized >= 1f)
            FireLaunch();
    }

    public void ResetLauncher()
    {
        _loadedBeyblade = null;
        _hasLaunched = false;
        _isDragging = false;
        ResetHandleVisual();
    }

    private bool IsHandlePressed()
    {
        if (_handleCollider == null || Camera.main == null)
        {
            return false;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return _handleCollider.Raycast(ray, out _, float.MaxValue);
    }

    private void UpdateDrag()
    {
        var pullNormalized = GetPullNormalized(Input.mousePosition);
        SetHandlePull(pullNormalized);

        if (_loadedBeyblade != null)
        {
            _loadedBeyblade.SetPreviewSpin(_maxSpinSpeed * pullNormalized);
        }

        if (pullNormalized >= 1f)
        {
            FireLaunch();
        }
    }

    private float GetPullNormalized(Vector3 mousePosition)
    {
        if (Camera.main == null || _handleRestPoint == null || _handlePullEndPoint == null)
        {
            return 0f;
        }

        var restScreen = Camera.main.WorldToScreenPoint(_handleRestPoint.position);
        var endScreen = Camera.main.WorldToScreenPoint(_handlePullEndPoint.position);
        var axis = endScreen - restScreen;
        var axisLength = axis.magnitude;
        if (axisLength <= Mathf.Epsilon)
        {
            return 0f;
        }

        var projectedDistance = Vector3.Dot(mousePosition - restScreen, axis / axisLength);
        return Mathf.Clamp01(projectedDistance / axisLength);
    }

    private void SetHandlePull(float pullNormalized)
    {
        if (_handleTransform == null || _handleRestPoint == null || _handlePullEndPoint == null)
        {
            return;
        }

        var handlePosition = Vector3.Lerp(_handleRestPoint.position, _handlePullEndPoint.position, pullNormalized);
        var handleRotation = GetHandleRotation();

        _handleTransform.position = handlePosition;
        _handleTransform.rotation = handleRotation * _handleLocalRotationOffset;
    }

    private void ResetHandleVisual()
    {
        SetHandlePull(0f);
    }

    private void CancelDrag()
    {
        _isDragging = false;
        ResetHandleVisual();

        if (_loadedBeyblade != null)
        {
            _loadedBeyblade.SetPreviewSpin(0f);
        }
    }

    private void FireLaunch()
    {
        if (_hasLaunched || _loadedBeyblade == null)
        {
            return;
        }

        _hasLaunched = true;
        _isDragging = false;

        var launchDirection = GetLaunchDirection();
        var launchData = new LaunchData
        {
            LaunchVelocity = launchDirection.sqrMagnitude > 0f ? launchDirection * _launchForce : Vector3.zero,
            SpinSpeed = _maxSpinSpeed
        };

        _loadedBeyblade.DetachFromLauncher();
        LaunchRequested?.Invoke(launchData);
        ResetHandleVisual();
    }

    private Quaternion GetHandleRotationOffset()
    {
        if (_handleTransform == null)
        {
            return Quaternion.identity;
        }

        var handleRotation = GetHandleRotation();
        return Quaternion.Inverse(handleRotation) * _handleTransform.rotation;
    }

    private Quaternion GetHandleRotation()
    {
        var axis = GetPullAxis();
        if (axis.sqrMagnitude <= Mathf.Epsilon)
        {
            return _handleTransform != null ? _handleTransform.rotation : Quaternion.identity;
        }

        return Quaternion.LookRotation(axis, Vector3.up);
    }

    private Vector3 GetLaunchDirection()
    {
        var axis = GetPullAxis();
        var launchDirection = Vector3.ProjectOnPlane(-axis, Vector3.up).normalized;
        return launchDirection.sqrMagnitude > 0f ? launchDirection : Vector3.zero;
    }

    private Vector3 GetPullAxis()
    {
        if (_handleRestPoint == null || _handlePullEndPoint == null)
        {
            return Vector3.zero;
        }

        return (_handlePullEndPoint.position - _handleRestPoint.position).normalized;
    }
}
