using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 以 UniTask 載入組裝相關場景。實際載入優先經由 <see cref="MainFlowManager"/>（Menu 場景常駐），
    /// 若專案中尚未建立 Manager 則 fallback 直接 <see cref="SceneManager"/>，方便單場景除錯。
    /// </summary>
    public static class AssemblySceneNavigator
    {
        public static UniTask LoadAssemblySceneAsync(int buildIndex, CancellationToken cancellationToken = default, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (MainFlowManager.Instance != null)
                return MainFlowManager.Instance.LoadSceneAsync(ScenePathFromBuildIndex(buildIndex), cancellationToken, timing);

            var op = SceneManager.LoadSceneAsync(buildIndex);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        public static UniTask LoadAssemblySceneAsync(string sceneName, CancellationToken cancellationToken = default, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (MainFlowManager.Instance != null)
                return MainFlowManager.Instance.LoadSceneAsync(sceneName, cancellationToken, timing);

            var op = SceneManager.LoadSceneAsync(sceneName);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        /// <summary>
        /// 【階段二 · 組裝→下一場】擷取組裝快照後載入場景（離開組裝關時用）。
        /// </summary>
        public static UniTask LoadSceneWithSnapshotAsync(
            int buildIndex,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (MainFlowManager.Instance != null)
                return MainFlowManager.Instance.LoadSceneWithSnapshotAsync(ScenePathFromBuildIndex(buildIndex), cancellationToken, timing);

            Debug.LogWarning("[AssemblySceneNavigator] 未找到 MainFlowManager：無法帶出組裝快照，僅直接載入場景（請從 Menu 開始或於場景內放置 Manager）。");
            var op = SceneManager.LoadSceneAsync(buildIndex);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        /// <inheritdoc cref="LoadSceneWithSnapshotAsync(int,System.Threading.CancellationToken,PlayerLoopTiming)"/>
        public static UniTask LoadSceneWithSnapshotAsync(
            string sceneName,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (MainFlowManager.Instance != null)
                return MainFlowManager.Instance.LoadSceneWithSnapshotAsync(sceneName, cancellationToken, timing);

            Debug.LogWarning("[AssemblySceneNavigator] 未找到 MainFlowManager：無法帶出組裝快照，僅直接載入場景（請從 Menu 開始或於場景內放置 Manager）。");
            var op = SceneManager.LoadSceneAsync(sceneName);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        /// <summary>【階段二】等同 <see cref="MainFlowManager.GoToBattleWithSnapshotAsync"/>。</summary>
        public static UniTask GoToBattleWithSnapshotAsync(
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (MainFlowManager.Instance != null)
                return MainFlowManager.Instance.GoToBattleWithSnapshotAsync(cancellationToken, timing);

            Debug.LogWarning("[AssemblySceneNavigator] 未找到 MainFlowManager：無法帶出組裝快照，僅直接載入 Battle。");
            var op = SceneManager.LoadSceneAsync(MainFlowManager.SceneBattle);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        static string ScenePathFromBuildIndex(int buildIndex)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (string.IsNullOrEmpty(path))
                return buildIndex.ToString();
            var slash = path.LastIndexOf('/');
            var dot = path.LastIndexOf('.');
            if (slash < 0 || dot <= slash)
                return path;
            return path.Substring(slash + 1, dot - slash - 1);
        }
    }
}
