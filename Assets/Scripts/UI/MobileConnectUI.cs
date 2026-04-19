using UnityEngine;

public class MobileConnectUI : MonoBehaviour
{
    [SerializeField]
    private Canvas _canvas;

    private DefaultActions _inputActions;

    public bool IsVisible
    {
        get => _canvas.enabled;
        set => _canvas.enabled = value;
    }

    void Awake()
    {
        _inputActions = new DefaultActions();
        _inputActions.Enable();   
    }

    void Update()
    {
        if (_inputActions.UI.MobileConnect.WasPressedThisFrame())
        {
            IsVisible = !IsVisible; 
        }
    }

    void OnDestroy()
    {
        _inputActions.Disable();
    }
}
