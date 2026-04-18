using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [SerializeField]
    private Beyblade[] _beyblades;

    [SerializeField]
    private TriggerEventSource[] _ringOutTriggers;

    [SerializeField, Min(0f)]
    private float _battleDurationSeconds = 5f;

    [SerializeField, Min(0f)]
    private float _restartDelaySeconds = 0.5f;

    private CancellationTokenSource _battleCts;
    private UniTask _battleTask = UniTask.CompletedTask;
    private bool _isBattleActive;
    private bool _hasBattleResult;

    private void Awake()
    {
        if (_beyblades == null || _beyblades.Length == 0)
        {
            _beyblades = GetComponentsInChildren<Beyblade>(includeInactive: true);
        }
    }

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
        foreach (var beyblade in _beyblades)
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

        foreach (var beyblade in _beyblades)
        {
            if (beyblade == null)
            {
                continue;
            }

            beyblade.BeginBattle();
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

        foreach (var beyblade in _beyblades)
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
        foreach (var beyblade in _beyblades)
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
}
