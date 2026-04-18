using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
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

    public GameObject Prefab => _prefab;
    public BeybladeAnchorType Anchor => _anchor;
    public Vector3 LocalPosition => _localPosition;
    public Vector3 LocalEulerAngles => _localEulerAngles;
    public Vector3 LocalScale => _localScale;
}

[Serializable]
public class BeybladeBuildConfig
{
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

    [SerializeField, Min(0f)]
    private float _battleDurationSeconds = 5f;

    [SerializeField, Min(0f)]
    private float _restartDelaySeconds = 0.5f;

    private CancellationTokenSource _battleCts;
    private UniTask _battleTask = UniTask.CompletedTask;
    private readonly List<Beyblade> _spawnedBeyblades = new();
    private readonly List<BeybladeBuildConfig> _activeBeybladeConfigs = new();
    private bool _isBattleActive;
    private bool _hasBattleResult;

    private void OnEnable()
    {
        SubscribeRingOutTriggers();
    }

    private async void Start()
    {
        await Play();
    }

    private void OnDisable()
    {
        UnsubscribeRingOutTriggers();
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

        _battleCts?.Dispose();
        _battleCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _battleTask = RunBattleLifecycleAsync(_battleCts.Token);
        return _battleTask;
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

        await Play();
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

    private UniTask StartBattleAsync()
    {
        _hasBattleResult = false;
        _isBattleActive = true;

        for (var i = 0; i < _spawnedBeyblades.Count; i++)
        {
            var beyblade = _spawnedBeyblades[i];
            if (beyblade == null)
            {
                continue;
            }

            beyblade.BeginBattle();
            beyblade.Launch(GetLaunchVelocity(i));
        }

        return UniTask.CompletedTask;
    }

    private async UniTask WaitForBattleToFinishAsync(CancellationToken cancellationToken)
    {
        if (_battleDurationSeconds <= 0f)
        {
            while (!_hasBattleResult)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(cancellationToken);
            }

            return;
        }

        var remainingTime = _battleDurationSeconds;
        while (!_hasBattleResult && remainingTime > 0f)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield(cancellationToken);
            remainingTime -= Time.deltaTime;
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

        _hasBattleResult = true;
        Debug.Log($"{beyblade.DisplayName} was knocked out of the arena.", beyblade);
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
            beyblade.Build(config.Attachments);
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
}
