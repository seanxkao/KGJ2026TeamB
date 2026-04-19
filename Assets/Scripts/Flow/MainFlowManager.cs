using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using KGJ.AssemblyScene;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================================
// 各階段應使用的方法（本類別為流程入口；通道彼此獨立）
// -----------------------------------------------------------------------------
// 【階段一 · 爪機 → 組裝】資料：進場「零件清單」（與 ClawToyIds / ModelConfig 相關）
//   · 爪機結束進組裝：SetClawToyIds → StartAssembly
//   · 不經爪機、直接依型錄進組裝：GoToAssemblyWithCatalog
//   · 組裝場景開場取清單生成零件：TryTakeParts（一般由 AssemblySceneBootstrap 呼叫）
//   · 僅清階段一暫存：ClearParts；清階段一＋二：ClearCarry
// -----------------------------------------------------------------------------
// 【階段二 · 組裝 → 對戰】資料：「組裝快照」（擷取後的 AssemblyStateSnapshot）
//   · 離開組裝、帶快照換場（建議）：LoadSceneWithSnapshotAsync(下一場名稱)
//   · 下一場固定為對戰：GoToBattleWithSnapshotAsync
//   · 對戰（等）場景讀取快照：TryGetSnapshot；讀畢釋出：ClearSnapshot
//   · 若要分步：AssemblyStateCapture.TryCaptureActiveScene → ClearParts → SetSnapshot → LoadSceneAsync
// -----------------------------------------------------------------------------
// 【與階段無關的一般轉場】GoToMenu、StartGame、StartBattle；任意場名：LoadScene / LoadSceneAsync
// =============================================================================

/// <summary>
/// 掛在 Menu 場景（並 <see cref="DontDestroyOnLoad"/>）：全專案場景切換與跨場資料請由此進出。
/// </summary>
/// <remarks>
/// 跨場傳遞分兩階段（通道彼此獨立）：<br/>
/// <b>階段一 · 爪機 → 組裝</b>：爪機寫入 <see cref="ClawToyIds"/>，進組裝前轉成「進場零件清單」暫存，
/// 由組裝場景 <see cref="TryTakeParts"/> 一次性取走並生成。<br/>
/// <b>階段二 · 組裝 → 對戰</b>：離開組裝時擷取場上組裝快照，經 <see cref="LoadSceneWithSnapshotAsync"/>（或 <see cref="GoToBattleWithSnapshotAsync"/>）
/// 寫入暫存，對戰場景以 <see cref="TryGetSnapshot"/> 讀取。
/// </remarks>
[DisallowMultipleComponent]
public class MainFlowManager : MonoBehaviour
{
    public const string SceneMenu = "Menu";
    public const string SceneClaw = "Claw";
    /// <summary>與 Build Settings 內場景檔名一致（目前為 Asembly.unity）。</summary>
    public const string SceneAssembly = "Asembly";
    public const string SceneBattle = "Battle";

    static MainFlowManager _instance;

    public static MainFlowManager Instance => _instance;

    [Tooltip("【階段一】爪機→組裝：依 ClawToyIds 與此型錄建立進場零件清單。可與 Claw 場景共用同一資產。")]
    [SerializeField]
    ModelConfig _modelConfig;

    /// <summary>【階段一】爪機收集的模型 id（可重複）；進組裝前由 <see cref="StartAssembly"/> 依型錄合成零件清單。</summary>
    public List<string> ClawToyIds { get; } = new();

    // --- 階段一暫存：進場零件清單（爪機/型錄 → 組裝生成） ---

    [System.NonSerialized]
    readonly List<AssemblyPartSpawnEntry> _carryParts = new();

    [System.NonSerialized]
    bool _hasCarryParts;

    // --- 階段二暫存：組裝快照（組裝 → 對戰等） ---

    [System.NonSerialized]
    AssemblyStateSnapshot _snapshot;

    [System.NonSerialized]
    bool _hasSnapshot;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        ClearCarry();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>回主選單。</summary>
    public void GoToMenu()
    {
        ClearCarry();
        ClawToyIds.Clear();
        LoadScene(SceneMenu);
    }

    /// <summary>新遊戲：清空爪機紀錄並進爪機場景。</summary>
    public void StartGame()
    {
        ClearCarry();
        ClawToyIds.Clear();
        LoadScene(SceneClaw);
    }

    /// <summary>進對戰場景。</summary>
    public void StartBattle()
    {
        ClearCarry();
        LoadScene(SceneBattle);
    }

    /// <summary>
    /// 【階段一】進組裝：清空兩階暫存後，依 <see cref="_modelConfig"/> 與 <see cref="ClawToyIds"/> 填入進場零件清單，再載入組裝場景。
    /// </summary>
    public void StartAssembly()
    {
        ClearCarry();
        ApplyClawToCarryPartsIfPossible();
        LoadScene(SceneAssembly);
    }

    /// <summary>
    /// 【階段一】略過爪機 id，直接依型錄明細進組裝（仍走零件清單通道）。會 <see cref="ClearSnapshot"/> 後覆寫進場清單。
    /// </summary>
    public void GoToAssemblyWithCatalog(IEnumerable<(string modelId, int count)> items)
    {
        ClearSnapshot();
        FillCarryPartsFromCatalog(_modelConfig, items);
        LoadScene(SceneAssembly);
    }

    /// <summary>同步載入場景。</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MainFlowManager] LoadScene: 場景名稱為空。", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>非同步載入場景（UniTask）。</summary>
    public UniTask LoadSceneAsync(
        string sceneName,
        CancellationToken cancellationToken = default,
        PlayerLoopTiming timing = PlayerLoopTiming.Update)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MainFlowManager] LoadSceneAsync: 場景名稱為空。", this);
            return UniTask.CompletedTask;
        }

        var op = SceneManager.LoadSceneAsync(sceneName);
        return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
    }

    /// <summary>
    /// 【階段二】擷取目前組裝場景狀態後載入指定場景。會 <see cref="ClearParts"/>（清掉階段一清單），再寫入快照供下一場 <see cref="TryGetSnapshot"/>。
    /// </summary>
    public UniTask LoadSceneWithSnapshotAsync(
        string nextSceneName,
        CancellationToken cancellationToken = default,
        PlayerLoopTiming timing = PlayerLoopTiming.Update)
    {
        var snap = AssemblyStateCapture.TryCaptureActiveScene();
        ClearParts();
        if (snap != null)
            SetSnapshot(snap);
        return LoadSceneAsync(nextSceneName, cancellationToken, timing);
    }

    /// <summary>【階段二】等同 <see cref="LoadSceneWithSnapshotAsync"/> 且下一場為 <see cref="SceneBattle"/>。</summary>
    public UniTask GoToBattleWithSnapshotAsync(
        CancellationToken cancellationToken = default,
        PlayerLoopTiming timing = PlayerLoopTiming.Update) =>
        LoadSceneWithSnapshotAsync(SceneBattle, cancellationToken, timing);

    /// <summary>【階段一】取代爪機收集的 id（通常進組裝前與 <see cref="StartAssembly"/> 併用）。</summary>
    public void SetClawToyIds(IEnumerable<string> ids)
    {
        ClawToyIds.Clear();
        if (ids == null)
            return;
        foreach (var id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
                ClawToyIds.Add(id);
        }
    }

    /// <summary>【階段一 · 組裝端】取走本次進場零件清單並清空暫存（由 <c>AssemblySceneBootstrap</c> 呼叫）。</summary>
    public bool TryTakeParts(List<AssemblyPartSpawnEntry> output)
    {
        if (!_hasCarryParts || output == null)
            return false;

        var any = false;
        foreach (var entry in _carryParts)
        {
            if (entry == null || entry.Prefab == null || entry.Count <= 0)
                continue;
            output.Add(entry);
            any = true;
        }

        _carryParts.Clear();
        _hasCarryParts = false;
        return any;
    }

    /// <summary>【階段二 · 對戰端】讀取組裝快照（不會清空；用完請 <see cref="ClearSnapshot"/>）。</summary>
    public bool TryGetSnapshot(out AssemblyStateSnapshot snapshot)
    {
        if (!_hasSnapshot || _snapshot == null)
        {
            snapshot = null;
            return false;
        }

        snapshot = _snapshot;
        return true;
    }

    /// <summary>清除【階段二】組裝快照。</summary>
    public void ClearSnapshot()
    {
        _snapshot = null;
        _hasSnapshot = false;
    }

    /// <summary>清除【階段一】進場零件清單。</summary>
    public void ClearParts()
    {
        _carryParts.Clear();
        _hasCarryParts = false;
    }

    /// <summary>清除階段一與階段二暫存（回選單 / 開新局等流程起點可呼叫）。</summary>
    public void ClearCarry()
    {
        ClearParts();
        ClearSnapshot();
    }

    /// <summary>【階段二】手動寫入快照（一般用 <see cref="LoadSceneWithSnapshotAsync"/>）。</summary>
    public void SetSnapshot(AssemblyStateSnapshot snapshot)
    {
        _snapshot = snapshot;
        _hasSnapshot = snapshot != null && snapshot.pieces != null && snapshot.pieces.Length > 0;
    }

    void FillCarryPartsFromCatalog(ModelConfig catalog, IEnumerable<(string modelId, int count)> items)
    {
        _carryParts.Clear();
        _hasCarryParts = false;
        if (catalog != null && items != null)
            catalog.AppendSpawnEntriesFromCatalogIds(_carryParts, items);

        foreach (var e in _carryParts)
        {
            if (e != null && e.Prefab != null && e.Count > 0)
            {
                _hasCarryParts = true;
                break;
            }
        }
    }

    void ApplyClawToCarryPartsIfPossible()
    {
        if (_modelConfig == null || ClawToyIds.Count == 0)
            return;

        var counts = new Dictionary<string, int>();
        foreach (var id in ClawToyIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            counts.TryGetValue(id, out var n);
            counts[id] = n + 1;
        }

        if (counts.Count == 0)
            return;

        var items = counts.Select(kv => (kv.Key, kv.Value)).ToList();
        FillCarryPartsFromCatalog(_modelConfig, items);
    }
}
