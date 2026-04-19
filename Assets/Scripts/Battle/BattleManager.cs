using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using KGJ.AssemblyScene;
using TMPro;
using UnityEngine;

[Serializable]
public class BeybladeAttachmentConfig
{
    [SerializeField]
    private GameObject _prefab;

    [SerializeField]
    private BeybladeAnchorType _anchor = BeybladeAnchorType.Center;

    [SerializeField]
    private Vector3 _localPosition = Vector3.zero;

    [SerializeField]
    private Vector3 _localEulerAngles = Vector3.zero;

    [SerializeField]
    private Vector3 _localScale = Vector3.one;

    public BeybladeAttachmentConfig(GameObject prefab, BeybladeAnchorType anchor, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
    {
        _prefab = prefab;
        _anchor = anchor;
        _localPosition = localPosition;
        _localEulerAngles = localEulerAngles;
        _localScale = localScale;
    }

    public GameObject Prefab => _prefab;
    public BeybladeAnchorType Anchor => _anchor;
    public Vector3 LocalPosition => _localPosition;
    public Vector3 LocalEulerAngles => _localEulerAngles;
    public Vector3 LocalScale => _localScale;
}

[Serializable]
public class BeybladePartPlayConfig
{
    public string modelId;
    public BeybladeAnchorType anchor = BeybladeAnchorType.Center;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
}

[Serializable]
public class BeybladeBuildConfig
{
    [SerializeField]
    private string _displayName;

    [SerializeField]
    private Beyblade _beybladePrefab;

    [SerializeField]
    private Transform _spawnPoint;

    [SerializeField]
    private BeybladeAttachmentConfig[] _attachments;

    [SerializeField, Min(0f)]
    private float _launchForce = 3f;

    [SerializeField]
    private Vector3 _launchDirection = Vector3.forward;

    public string DisplayName => _displayName;
    public Beyblade BeybladePrefab => _beybladePrefab;
    public Transform SpawnPoint => _spawnPoint;
    public BeybladeAttachmentConfig[] Attachments => _attachments;
    public float LaunchForce => _launchForce;
    public Vector3 LaunchDirection => _launchDirection;
}

public class BattleManager : MonoBehaviour
{
    [SerializeField]
    private BeybladeBuildConfig[] _beybladeConfigs;

    [SerializeField]
    private TriggerEventSource[] _ringOutTriggers;

    [SerializeField]
    private ModelConfig _modelConfig;

    [SerializeField]
    private BattleComputerLineup _computerLineup;

    [SerializeField]
    private Launcher _playerLauncher;

    [SerializeField, Min(0)]
    private int _playerBeybladeIndex;

    [SerializeField]
    private BattleResultUI _battleResultUI;

    [SerializeField]
    private TextMeshProUGUI _resultText;

    [SerializeField, Min(0f)]
    private float _restartDelaySeconds = 0.5f;

    [SerializeField]
    private bool _autoPlay = true;

    [SerializeField]
    private GameObject startPanel;

    private CancellationTokenSource _battleCts;
    private UniTask _battleTask = UniTask.CompletedTask;
    private readonly List<Beyblade> _spawnedBeyblades = new();
    private readonly List<BeybladeBuildConfig> _activeBeybladeConfigs = new();
    private readonly HashSet<Beyblade> _eliminatedBeyblades = new();
    private UniTaskCompletionSource _playerLaunchSource;
    private LaunchData _pendingPlayerLaunchData;
    private bool _hasPendingPlayerLaunch;
    private BeybladePartPlayConfig[][] _pendingPlayConfigs;
    private BeybladePartPlayConfig[][] _activePlayConfigs;
    private Beyblade _winner;
    private bool _isBattleActive;
    private bool _hasBattleResult;
    private bool _isHandlingResultUiAction;

    private void OnEnable()
    {
        SubscribeRingOutTriggers();
        SubscribeLauncher();
        SubscribeBattleResultUi();
    }

    private async void Start()
    {
        if (_autoPlay)
        {
            await Play(GetOrCreateDefaultPlayConfigs());
        }
    }

    private void OnDisable()
    {
        UnsubscribeRingOutTriggers();
        UnsubscribeLauncher();
        UnsubscribeBattleResultUi();
    }

    private void OnDestroy()
    {
        CancelCurrentBattle();
        ClearSpawnedBeyblades();
    }

    public UniTask Play()
    {
        if (!_battleTask.Status.IsCompleted())
        {
            return _battleTask;
        }

        _pendingPlayConfigs ??= _activePlayConfigs ?? GetOrCreateDefaultPlayConfigs();

        _battleCts?.Dispose();
        _battleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _battleTask = RunBattleLifecycleAsync(_battleCts.Token);
        return _battleTask;
    }

    public UniTask Play(BeybladePartPlayConfig[][] playConfigs)
    {
        startPanel.SetActive(false);
        _pendingPlayConfigs = playConfigs;
        _activePlayConfigs = playConfigs;
        return Play();
    }

    public async UniTask RestartAsync()
    {
        CancelCurrentBattle();
        await EndBattleAsync();
        await ResetBattleStateAsync();

        if (_restartDelaySeconds > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_restartDelaySeconds), cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        await Play(_activePlayConfigs ?? GetOrCreateDefaultPlayConfigs());
    }

    private async UniTask RunBattleLifecycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SetupBattleAsync();
            await StartBattleAsync();
            await WaitForBattleToFinishAsync(cancellationToken);
            await EndBattleAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await EndBattleAsync();
        }
        finally
        {
            _isBattleActive = false;
            _hasBattleResult = false;
        }
    }

    private UniTask SetupBattleAsync()
    {
        if (_spawnedBeyblades.Count == 0)
        {
            SpawnBeyblades();
        }

        _activePlayConfigs = _pendingPlayConfigs ?? _activePlayConfigs ?? GetOrCreateDefaultPlayConfigs();

        _winner = null;
        _eliminatedBeyblades.Clear();
        SetResultText(string.Empty);
        _battleResultUI?.Hide();

        var playerBeybladeIndex = GetEffectivePlayerBeybladeIndex();

        for (var i = 0; i < _spawnedBeyblades.Count; i++)
        {
            var beyblade = _spawnedBeyblades[i];
            if (beyblade == null)
            {
                continue;
            }

            beyblade.ResetState();
            ApplyPlayConfigToBeyblade(i, beyblade);

            if (i == playerBeybladeIndex && _playerLauncher != null)
            {
                _playerLauncher.LoadBeyblade(beyblade);
            }
        }

        return UniTask.CompletedTask;
    }

    private async UniTask StartBattleAsync()
    {
        _hasBattleResult = false;
        _isBattleActive = false;
        _playerLaunchSource = null;
        _hasPendingPlayerLaunch = false;
        _pendingPlayerLaunchData = default;
        var playerBeybladeIndex = GetEffectivePlayerBeybladeIndex();

        for (var i = 0; i < _spawnedBeyblades.Count; i++)
        {
            var beyblade = _spawnedBeyblades[i];
            if (beyblade == null)
            {
                continue;
            }

            if (i == playerBeybladeIndex && _playerLauncher != null)
            {
                _playerLaunchSource = new UniTaskCompletionSource();
                continue;
            }
        }

        if (_playerLaunchSource != null)
        {
            await _playerLaunchSource.Task;
        }

        for (var i = 0; i < _spawnedBeyblades.Count; i++)
        {
            var beyblade = _spawnedBeyblades[i];
            if (beyblade == null)
            {
                continue;
            }

            if (i == playerBeybladeIndex && _playerLauncher != null)
            {
                if (_hasPendingPlayerLaunch)
                {
                    beyblade.SetPreviewSpin(_pendingPlayerLaunchData.SpinSpeed);
                    beyblade.BeginBattle();
                    beyblade.Launch(_pendingPlayerLaunchData.LaunchVelocity);
                }

                continue;
            }

            beyblade.BeginBattle();
            beyblade.Launch(GetLaunchVelocity(i));
        }

        _isBattleActive = true;
    }

    private async UniTask WaitForBattleToFinishAsync(CancellationToken cancellationToken)
    {
        while (!_hasBattleResult)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield(cancellationToken);
        }
    }

    private UniTask EndBattleAsync()
    {
        _isBattleActive = false;

        foreach (var beyblade in _spawnedBeyblades)
        {
            if (beyblade == null)
            {
                continue;
            }

            beyblade.EndBattle();
        }

        return UniTask.CompletedTask;
    }

    private UniTask ResetBattleStateAsync()
    {
        _playerLauncher?.ResetLauncher();
        _hasPendingPlayerLaunch = false;
        _pendingPlayerLaunchData = default;
        _winner = null;
        _eliminatedBeyblades.Clear();
        SetResultText(string.Empty);
        _battleResultUI?.Hide();

        foreach (var beyblade in _spawnedBeyblades)
        {
            if (beyblade == null)
            {
                continue;
            }

            beyblade.ResetState();
        }

        return UniTask.CompletedTask;
    }

    private void CancelCurrentBattle()
    {
        if (_battleCts == null)
        {
            _battleTask = UniTask.CompletedTask;
            return;
        }

        if (!_battleCts.IsCancellationRequested)
        {
            _battleCts.Cancel();
        }

        _battleCts.Dispose();
        _battleCts = null;
        _battleTask = UniTask.CompletedTask;
    }

    private void SubscribeRingOutTriggers()
    {
        if (_ringOutTriggers == null)
        {
            return;
        }

        foreach (var ringOutTrigger in _ringOutTriggers)
        {
            if (ringOutTrigger == null)
            {
                continue;
            }

            ringOutTrigger.TriggerEntered -= HandleRingOutTriggerEntered;
            ringOutTrigger.TriggerEntered += HandleRingOutTriggerEntered;
        }
    }

    private void UnsubscribeRingOutTriggers()
    {
        if (_ringOutTriggers == null)
        {
            return;
        }

        foreach (var ringOutTrigger in _ringOutTriggers)
        {
            if (ringOutTrigger == null)
            {
                continue;
            }

            ringOutTrigger.TriggerEntered -= HandleRingOutTriggerEntered;
        }
    }

    private void HandleRingOutTriggerEntered(Collider other)
    {
        if (!_isBattleActive || _hasBattleResult || other == null)
        {
            return;
        }

        var beyblade = other.GetComponentInParent<Beyblade>();
        if (beyblade == null)
        {
            return;
        }

        if (!_eliminatedBeyblades.Add(beyblade))
        {
            return;
        }

        Debug.Log($"{beyblade.DisplayName} was knocked out of the arena.", beyblade);

        if (_winner == null)
        {
            _winner = FindLastStandingBeyblade();
        }

        if (_winner != null)
        {
            _hasBattleResult = true;
            SetResultText($"{_winner.DisplayName} 勝利");
            _battleResultUI?.Show();
        }
    }

    private void SubscribeLauncher()
    {
        if (_playerLauncher == null)
        {
            return;
        }

        _playerLauncher.LaunchRequested -= HandlePlayerLaunchRequested;
        _playerLauncher.LaunchRequested += HandlePlayerLaunchRequested;
    }

    private void UnsubscribeLauncher()
    {
        if (_playerLauncher == null)
        {
            return;
        }

        _playerLauncher.LaunchRequested -= HandlePlayerLaunchRequested;
    }

    private void HandlePlayerLaunchRequested(LaunchData launchData)
    {
        var playerBeybladeIndex = GetEffectivePlayerBeybladeIndex();
        if (playerBeybladeIndex < 0)
        {
            return;
        }

        _pendingPlayerLaunchData = launchData;
        _hasPendingPlayerLaunch = true;
        _playerLaunchSource?.TrySetResult();
    }

    private void SubscribeBattleResultUi()
    {
        if (_battleResultUI == null)
        {
            return;
        }

        _battleResultUI.ResetRequested -= HandleResetRequested;
        _battleResultUI.ResetRequested += HandleResetRequested;
        _battleResultUI.BackToMainMenuRequested -= HandleBackToMainMenuRequested;
        _battleResultUI.BackToMainMenuRequested += HandleBackToMainMenuRequested;
    }

    private void UnsubscribeBattleResultUi()
    {
        if (_battleResultUI == null)
        {
            return;
        }

        _battleResultUI.ResetRequested -= HandleResetRequested;
        _battleResultUI.BackToMainMenuRequested -= HandleBackToMainMenuRequested;
    }

    private void HandleResetRequested()
    {
        RestartFromResultUiAsync().Forget();
    }

    private async UniTaskVoid RestartFromResultUiAsync()
    {
        if (_isHandlingResultUiAction)
        {
            return;
        }

        _isHandlingResultUiAction = true;

        try
        {
            await RestartAsync();
        }
        finally
        {
            _isHandlingResultUiAction = false;
        }
    }

    private void HandleBackToMainMenuRequested()
    {
        if (_isHandlingResultUiAction)
        {
            return;
        }

        _isHandlingResultUiAction = true;
        CancelCurrentBattle();
        MainFlowManager.Instance?.GoToMenu();
    }

    private void SpawnBeyblades()
    {
        ClearSpawnedBeyblades();

        if (_beybladeConfigs == null)
        {
            return;
        }

        foreach (var config in _beybladeConfigs)
        {
            if (config == null || config.BeybladePrefab == null)
            {
                continue;
            }

            var spawnParent = transform;
            var spawnPosition = transform.position;
            var spawnRotation = transform.rotation;

            if (config.SpawnPoint != null)
            {
                spawnParent = config.SpawnPoint.parent;
                spawnPosition = config.SpawnPoint.position;
                spawnRotation = config.SpawnPoint.rotation;
            }

            var beyblade = Instantiate(config.BeybladePrefab, spawnPosition, spawnRotation, spawnParent);
            beyblade.SetDisplayName(config.DisplayName);
            _spawnedBeyblades.Add(beyblade);
            _activeBeybladeConfigs.Add(config);
        }
    }

    private void ClearSpawnedBeyblades()
    {
        foreach (var beyblade in _spawnedBeyblades)
        {
            if (beyblade == null)
            {
                continue;
            }

            Destroy(beyblade.gameObject);
        }

        _spawnedBeyblades.Clear();
        _activeBeybladeConfigs.Clear();
    }

    private Vector3 GetLaunchVelocity(int beybladeIndex)
    {
        if (beybladeIndex < 0 || beybladeIndex >= _activeBeybladeConfigs.Count)
        {
            return Vector3.zero;
        }

        var config = _activeBeybladeConfigs[beybladeIndex];
        if (config == null || config.LaunchForce <= 0f)
        {
            return Vector3.zero;
        }

        var launchDirection = Vector3.ProjectOnPlane(config.LaunchDirection, Vector3.up).normalized;
        if (launchDirection.sqrMagnitude <= 0f)
        {
            return Vector3.zero;
        }

        return launchDirection * config.LaunchForce;
    }

    private int GetEffectivePlayerBeybladeIndex()
    {
        if (_playerLauncher == null || _spawnedBeyblades.Count == 0)
        {
            return -1;
        }

        return Mathf.Clamp(_playerBeybladeIndex, 0, _spawnedBeyblades.Count - 1);
    }

    private Beyblade FindLastStandingBeyblade()
    {
        Beyblade lastStanding = null;
        var aliveCount = 0;

        foreach (var beyblade in _spawnedBeyblades)
        {
            if (beyblade == null || _eliminatedBeyblades.Contains(beyblade))
            {
                continue;
            }

            aliveCount++;
            lastStanding = beyblade;

            if (aliveCount > 1)
            {
                return null;
            }
        }

        return aliveCount == 1 ? lastStanding : null;
    }

    private void SetResultText(string message)
    {
        if (_resultText == null)
        {
            return;
        }

        _resultText.text = message;
        _resultText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void ApplyPlayConfigToBeyblade(int beybladeIndex, Beyblade beyblade)
    {
        if (beyblade == null)
        {
            return;
        }

        var attachments = GetResolvedAttachments(beybladeIndex);
        beyblade.Build(attachments);
    }

    private BeybladeAttachmentConfig[] GetResolvedAttachments(int beybladeIndex)
    {
        if (beybladeIndex < 0 || beybladeIndex >= _activeBeybladeConfigs.Count)
        {
            return Array.Empty<BeybladeAttachmentConfig>();
        }

        var playConfigs = _activePlayConfigs ?? _pendingPlayConfigs;
        if (playConfigs != null &&
            beybladeIndex < playConfigs.Length &&
            playConfigs[beybladeIndex] != null &&
            playConfigs[beybladeIndex].Length > 0)
        {
            return ResolvePlayConfigAttachments(playConfigs[beybladeIndex]);
        }

        var defaultAttachments = _activeBeybladeConfigs[beybladeIndex]?.Attachments;
        return defaultAttachments ?? Array.Empty<BeybladeAttachmentConfig>();
    }

    private BeybladeAttachmentConfig[] ResolvePlayConfigAttachments(BeybladePartPlayConfig[] parts)
    {
        if (parts == null || parts.Length == 0)
        {
            return Array.Empty<BeybladeAttachmentConfig>();
        }

        var attachments = new List<BeybladeAttachmentConfig>(parts.Length);
        foreach (var part in parts)
        {
            if (part == null)
            {
                continue;
            }

            var prefab = FindModelPrefab(part.modelId);
            if (prefab == null)
            {
                Debug.LogWarning($"BattleManager could not resolve model id '{part.modelId}'.", this);
                continue;
            }

            attachments.Add(new BeybladeAttachmentConfig(
                prefab,
                part.anchor,
                part.localPosition,
                part.localEulerAngles,
                Vector3.one));
        }

        return attachments.ToArray();
    }

    private GameObject FindModelPrefab(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId) || _modelConfig == null)
        {
            return null;
        }

        var modelData = _modelConfig.GetDataById(modelId);
        return modelData?.model;
    }

    private BeybladePartPlayConfig[][] GetOrCreateDefaultPlayConfigs()
    {
        if (_beybladeConfigs == null || _beybladeConfigs.Length == 0)
        {
            return Array.Empty<BeybladePartPlayConfig[]>();
        }

        var playConfigs = new BeybladePartPlayConfig[_beybladeConfigs.Length][];
        playConfigs[0] = CreatePlayerPartsFromSnapshotOrPlaceholder();

        for (var i = 1; i < _beybladeConfigs.Length; i++)
        {
            var computerIndex = i - 1;
            playConfigs[i] = GetComputerParts(computerIndex);
        }

        return playConfigs;
    }

    private BeybladePartPlayConfig[] CreatePlayerPartsFromSnapshotOrPlaceholder()
    {
        var flowManager = MainFlowManager.Instance;
        if (flowManager != null && flowManager.TryGetSnapshot(out var snapshot))
        {
            flowManager.ClearSnapshot();
            var snapshotParts = ConvertSnapshotToPartConfigs(snapshot);
            if (snapshotParts.Length > 0)
            {
                return snapshotParts;
            }
        }

        return CreatePlaceholderPlayerParts();
    }

    private BeybladePartPlayConfig[] CreatePlaceholderPlayerParts()
    {
        return new[]
        {
            new BeybladePartPlayConfig
            {
                modelId = "oiiao",
                anchor = BeybladeAnchorType.Center,
                localPosition = Vector3.zero,
                localEulerAngles = Vector3.zero,
            },
            new BeybladePartPlayConfig
            {
                modelId = "shit",
                anchor = BeybladeAnchorType.Top,
                localPosition = new Vector3(0f, 4f, 0f),
                localEulerAngles = Vector3.zero,
            },
        };
    }

    private BeybladePartPlayConfig[] ConvertSnapshotToPartConfigs(AssemblyStateSnapshot snapshot)
    {
        if (snapshot == null || snapshot.pieces == null || snapshot.pieces.Length == 0)
        {
            return Array.Empty<BeybladePartPlayConfig>();
        }

        var parts = new List<BeybladePartPlayConfig>(snapshot.pieces.Length);
        foreach (var piece in snapshot.pieces)
        {
            if (piece == null || string.IsNullOrWhiteSpace(piece.modelId))
            {
                continue;
            }

            parts.Add(new BeybladePartPlayConfig
            {
                modelId = piece.modelId,
                anchor = BeybladeAnchorType.Center,
                localPosition = piece.localPosition,
                localEulerAngles = piece.localRotation.eulerAngles,
            });
        }

        return parts.ToArray();
    }

    private BeybladePartPlayConfig[] GetComputerParts(int computerIndex)
    {
        if (_computerLineup != null &&
            _computerLineup.Entries != null &&
            computerIndex >= 0 &&
            computerIndex < _computerLineup.Entries.Length &&
            _computerLineup.Entries[computerIndex] != null &&
            _computerLineup.Entries[computerIndex].Parts != null)
        {
            return _computerLineup.Entries[computerIndex].Parts;
        }

        var buildConfigIndex = computerIndex + 1;
        if (buildConfigIndex < 0 || buildConfigIndex >= _beybladeConfigs.Length)
        {
            return Array.Empty<BeybladePartPlayConfig>();
        }

        return ConvertAttachmentsToPartConfigs(_beybladeConfigs[buildConfigIndex]?.Attachments);
    }

    private BeybladePartPlayConfig[] ConvertAttachmentsToPartConfigs(BeybladeAttachmentConfig[] attachments)
    {
        if (attachments == null || attachments.Length == 0)
        {
            return Array.Empty<BeybladePartPlayConfig>();
        }

        var parts = new List<BeybladePartPlayConfig>(attachments.Length);
        foreach (var attachment in attachments)
        {
            if (attachment == null)
            {
                continue;
            }

            var modelId = FindModelId(attachment.Prefab);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                continue;
            }

            parts.Add(new BeybladePartPlayConfig
            {
                modelId = modelId,
                anchor = attachment.Anchor,
                localPosition = attachment.LocalPosition,
                localEulerAngles = attachment.LocalEulerAngles,
            });
        }

        return parts.ToArray();
    }

    private string FindModelId(GameObject prefab)
    {
        if (prefab == null || _modelConfig == null)
        {
            return null;
        }

        var models = _modelConfig.GetAllModels();
        foreach (var model in models)
        {
            if (model == null || model.model == null)
            {
                continue;
            }

            if (model.model == prefab)
            {
                return model.id;
            }
        }

        return null;
    }

    public void OnClickStart()
    {
        Play(GetOrCreateDefaultPlayConfigs());
    }
}
