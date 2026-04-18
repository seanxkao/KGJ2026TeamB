using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 進場後只做 Prefab <see cref="Object.Instantiate"/>：<see cref="AssemblyHandoffSession"/> 有「本次切場景帶進來」的有效項目時優先且只消耗一次，否則使用 Inspector 的場景預設清單。
    /// 外觀請在 Prefab 或材質資產上設定；本類別不在執行期修改 Renderer。組裝用的 <see cref="Rigidbody"/>
    /// 由 <see cref="AssemblyPiece"/> 在 play mode 自動補上。
    /// </summary>
    public sealed class AssemblySceneBootstrap : MonoBehaviour
    {
        static int s_playSessionStamp;

        [Tooltip("當跨場景清單為空或無有效 Prefab 時，改由此清單生成（在場景里拖入 Prefab + 數量）。")]
        [SerializeField] AssemblyPartSpawnEntry[] _sceneDefaultSpawns;

        [SerializeField] Vector2 _spawnArea = new Vector2(5f, 5f);
        [SerializeField] float _spawnLift = 0.75f;
        [SerializeField] float _pieceSeparation = 1.1f;
        [SerializeField] float _minAutoFootprint = 0.8f;
        [SerializeField] float _maxAutoFootprint = 1.8f;
        [SerializeField] float _spawnHeightPerFootprint = 0.5f;

        int _appliedPlaySessionStamp = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySessionStamp() => s_playSessionStamp = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RefreshLoadedBootstrapsOnPlayEnter()
        {
            s_playSessionStamp++;
            var bootstraps = Object.FindObjectsByType<AssemblySceneBootstrap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < bootstraps.Length; i++)
                bootstraps[i].ApplyCurrentPlaySessionIfNeeded();
        }

        async void Start()
        {
            await UniTask.NextFrame(PlayerLoopTiming.Update);
            ApplyCurrentPlaySessionIfNeeded();
        }

        void ApplyCurrentPlaySessionIfNeeded()
        {
            if (!Application.isPlaying) return;
            if (_appliedPlaySessionStamp == s_playSessionStamp) return;
            _appliedPlaySessionStamp = s_playSessionStamp;
            SpawnPieces();
        }

        void SpawnPieces()
        {
            var plan = new List<AssemblyPartSpawnEntry>();
            var source = TryFillFromHandoff(plan) ? "handoff" : (TryFillFromSceneDefaults(plan) ? "scene defaults" : "none");

            if (plan.Count == 0)
            {
                Debug.LogWarning(
                    "[AssemblySceneBootstrap] 沒有可生成的 Prefab：請設定跨場景清單，或在「Scene Default Spawns」填入 Prefab 與數量。",
                    this);
                return;
            }

            ClearExistingPiecesInScene();
            DebugSpawnPlan(source, plan);
            SpawnPiecesWithFootprintSpacing(plan);
        }

        static bool TryFillFromHandoff(List<AssemblyPartSpawnEntry> plan) =>
            AssemblyHandoffSession.TryConsumeEntries(plan);

        bool TryFillFromSceneDefaults(List<AssemblyPartSpawnEntry> plan)
        {
            if (_sceneDefaultSpawns == null || _sceneDefaultSpawns.Length == 0) return false;

            var any = false;
            foreach (var entry in _sceneDefaultSpawns)
            {
                if (entry == null || entry.Prefab == null || entry.Count <= 0) continue;
                plan.Add(entry);
                any = true;
            }

            return any;
        }

        void ClearExistingPiecesInScene()
        {
            var pieces = Object.FindObjectsByType<AssemblyPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece == null || piece.gameObject.scene != gameObject.scene) continue;
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
        }

        void DebugSpawnPlan(string source, List<AssemblyPartSpawnEntry> plan)
        {
            var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
            var parts = new List<string>(plan.Count);
            var total = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                var entry = plan[i];
                if (entry == null || entry.Prefab == null || entry.Count <= 0) continue;
                parts.Add($"{entry.Prefab.name}x{entry.Count}");
                total += entry.Count;
            }

            Debug.Log($"[AssemblySceneBootstrap] Scene={sceneName}, source={source}, total={total}, plan=[{string.Join(", ", parts)}]", this);
        }

        void SpawnPiecesWithFootprintSpacing(List<AssemblyPartSpawnEntry> plan)
        {
            var surfaceY = GetSpawnSurfaceY();
            var spawnHeightPerFootprint = _spawnHeightPerFootprint > 0f ? _spawnHeightPerFootprint : 0.5f;
            var left = -_spawnArea.x * 0.5f;
            var right = _spawnArea.x * 0.5f;
            var cursorX = left;
            var cursorZ = -_spawnArea.y * 0.25f;
            var rowDepth = 0f;

            foreach (var entry in plan)
            {
                var prefab = entry.Prefab;
                var count = Mathf.Max(0, entry.Count);
                var footprint = GetSpawnFootprint(entry);
                for (var i = 0; i < count; i++)
                {
                    if (cursorX > left && cursorX + footprint > right)
                    {
                        cursorX = left;
                        cursorZ += rowDepth + _pieceSeparation;
                        rowDepth = 0f;
                    }

                    var position = new Vector3(
                        cursorX + footprint * 0.5f,
                        surfaceY + _spawnLift + footprint * spawnHeightPerFootprint,
                        cursorZ + footprint * 0.5f);

                    InstantiateFromPrefab(prefab, position, entry);
                    cursorX += footprint + _pieceSeparation;
                    rowDepth = Mathf.Max(rowDepth, footprint);
                }
            }
        }

        float GetSpawnSurfaceY()
        {
            var platform = GameObject.Find("AssemblyPlatform");
            if (platform == null) return 0f;

            var col = platform.GetComponent<Collider>();
            if (col != null && col.enabled) return col.bounds.max.y;

            var renderer = platform.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled) return renderer.bounds.max.y;

            return platform.transform.position.y;
        }

        float GetSpawnFootprint(AssemblyPartSpawnEntry entry)
        {
            if (entry != null && entry.SpawnFootprint > 0f)
                return Mathf.Clamp(entry.SpawnFootprint, _minAutoFootprint, _maxAutoFootprint);

            var prefab = entry != null ? entry.Prefab : null;
            if (prefab == null) return _minAutoFootprint;

            var scale = prefab.transform.localScale;
            var auto = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            if (auto < 1e-3f) auto = 1f;
            return Mathf.Clamp(auto, _minAutoFootprint, _maxAutoFootprint);
        }

        static GameObject InstantiateFromPrefab(GameObject prefab, Vector3 position, AssemblyPartSpawnEntry entry)
        {
            var rot = prefab.transform.rotation;
            var instance = Instantiate(prefab, position, rot);
            var piece = instance.GetComponent<AssemblyPiece>();
            if (piece != null)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.CatalogId))
                    piece.SetRuntimeCatalogId(entry.CatalogId);
                piece.EnsureRuntimeRigidbody();
            }

            return instance;
        }
    }
}
