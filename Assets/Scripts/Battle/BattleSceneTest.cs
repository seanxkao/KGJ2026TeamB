using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BattleSceneTest : MonoBehaviour
{
    [SerializeField]
    private Button _restartButton;
    [SerializeField]
    private BattleManager _battleManager;

    private void Start()
    {
        if (_restartButton != null)
        {
            _restartButton.onClick.AddListener(_Restart);
        }
    }

    private void OnDestroy()
    {
        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(_Restart);
        }
    }

    private void _Restart()
    {
        RestartAsync().Forget();
    }

    private async UniTaskVoid RestartAsync()
    {
        if (_battleManager == null)
        {
            Debug.LogWarning("BattleSceneTest is missing a BattleManager reference.", this);
            return;
        }

        if (_restartButton != null)
        {
            _restartButton.interactable = false;
        }

        try
        {
            await _battleManager.RestartAsync();
        }
        finally
        {
            if (_restartButton != null)
            {
                _restartButton.interactable = true;
            }
        }
    }
}
