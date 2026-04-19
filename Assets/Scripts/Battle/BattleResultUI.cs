using System;
using UnityEngine;
using UnityEngine.UI;

public class BattleResultUI : MonoBehaviour
{
    [SerializeField]
    private Canvas _canvas;

    [SerializeField]
    private Button _resetButton;

    [SerializeField]
    private Button _backToMainMenuButton;

    public event Action ResetRequested;
    public event Action BackToMainMenuRequested;

    public bool IsVisible
    {
        get
        {
            if (_canvas != null)
            {
                return _canvas.enabled;
            }

            return gameObject.activeSelf;
        }
        set
        {
            if (_canvas != null)
            {
                _canvas.enabled = value;
                return;
            }

            gameObject.SetActive(value);
        }
    }

    private void Awake()
    {
        Hide();
    }

    private void OnEnable()
    {
        if (_resetButton != null)
        {
            _resetButton.onClick.AddListener(HandleResetClicked);
        }

        if (_backToMainMenuButton != null)
        {
            _backToMainMenuButton.onClick.AddListener(HandleBackToMainMenuClicked);
        }
    }

    private void OnDisable()
    {
        if (_resetButton != null)
        {
            _resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        if (_backToMainMenuButton != null)
        {
            _backToMainMenuButton.onClick.RemoveListener(HandleBackToMainMenuClicked);
        }
    }

    private void HandleResetClicked()
    {
        ResetRequested?.Invoke();
    }

    private void HandleBackToMainMenuClicked()
    {
        BackToMainMenuRequested?.Invoke();
    }

    public void Show()
    {
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
    }
}
