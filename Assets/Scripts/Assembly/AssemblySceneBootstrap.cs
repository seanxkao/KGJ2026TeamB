using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 進場後只做 Prefab <see cref="Object.Instantiate"/>：<see cref="AssemblyHandoffSession"/> 有有效項目時優先，否則使用 Inspector 的場景預設清單。
    /// 外觀請在 Prefab 或材質資產上設定；本類別不在執行期修改 Renderer。
    /// </summary>
    public sealed class AssemblySceneBootstrap : MonoBehaviour
    {
        [Tooltip("當跨場景清單為空或無有效 Prefab 時，改由此清單生成（在場景里拖入 Prefab + 數量）。")]
        [SerializeField] AssemblyPartSpawnEntry[] _sceneDefaultSpawns;

        [SerializeField] Vector2 _spawnArea = new Vector2(5f, 5f);
        [SerializeField] float _spawnLift = 0.75f;
        [SerializeField] float _pieceSeparation = 1.1f;

        async void Start()
        {
            await UniTask.NextFrame(PlayerLoopTiming.Update);
            SpawnPieces();
        }

        void SpawnPieces()
        {
            var plan = new List<AssemblyPartSpawnEntry>();
            if (!TryFillFromHandoff(plan))
                TryFillFromSceneDefaults(plan);

            if (plan.Count == 0)
            {
                Debug.LogWarning(
                    "[AssemblySceneBootstrap] 沒有可生成的 Prefab：請設定跨場景清單，或在「Scene Default Spawns」填入 Prefab 與數量。",
                    this);
                return;
            }

            var origins = new List<Vector3>();
            var index = 0;

            foreach (var entry in plan)
            {
                var prefab = entry.Prefab;
                var count = Mathf.Max(0, entry.Count);
                for (var i = 0; i < count; i++)
                {
                    var pos = NextSpawnPoint(index++, origins);
                    InstantiateFromPrefab(prefab, pos);
                }
            }
        }

        static bool TryFillFromHandoff(List<AssemblyPartSpawnEntry> plan)
        {
            if (!AssemblyHandoffSession.HasEntries) return false;

            var any = false;
            foreach (var entry in AssemblyHandoffSession.Entries)
            {
                if (entry == null || entry.Prefab == null || entry.Count <= 0) continue;
                plan.Add(entry);
                any = true;
            }

            return any;
        }

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

        Vector3 NextSpawnPoint(int index, List<Vector3> origins)
        {
            var cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(index + 1)));
            var row = index / cols;
            var col = index % cols;
            var x = (col - cols * 0.5f) * _pieceSeparation;
            var z = row * _pieceSeparation - _spawnArea.y * 0.25f;
            x = Mathf.Clamp(x, -_spawnArea.x * 0.5f, _spawnArea.x * 0.5f);
            z = Mathf.Clamp(z, -_spawnArea.y * 0.5f, _spawnArea.y * 0.5f);
            var p = new Vector3(x, _spawnLift, z);
            origins.Add(p);
            return p;
        }

        static void InstantiateFromPrefab(GameObject prefab, Vector3 position)
        {
            var rot = prefab.transform.rotation;
            Instantiate(prefab, position, rot);
        }
    }
}
