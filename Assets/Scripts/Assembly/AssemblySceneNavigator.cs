using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KGJ.AssemblyScene
{
    /// <summary>
    /// 以 UniTask 載入組裝場景（供前一場景呼叫）。
    /// </summary>
    public static class AssemblySceneNavigator
    {
        public static UniTask LoadAssemblySceneAsync(int buildIndex, CancellationToken cancellationToken = default, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var op = SceneManager.LoadSceneAsync(buildIndex);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        public static UniTask LoadAssemblySceneAsync(string sceneName, CancellationToken cancellationToken = default, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            return op.ToUniTask(cancellationToken: cancellationToken, timing: timing);
        }

        /// <summary>
        /// 先擷取作用中場景之組裝快照並寫入 <see cref="AssemblyHandoffSession"/>，再載入場景（供離開組裝關時呼叫）。
        /// </summary>
        public static UniTask LoadSceneCarryingAssemblySnapshotAsync(
            int buildIndex,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var snap = AssemblyStateCapture.TryCaptureActiveScene();
            if (snap != null)
                AssemblyHandoffSession.SetAssemblySnapshot(snap);
            return LoadAssemblySceneAsync(buildIndex, cancellationToken, timing);
        }

        /// <inheritdoc cref="LoadSceneCarryingAssemblySnapshotAsync(int,System.Threading.CancellationToken,PlayerLoopTiming)"/>
        public static UniTask LoadSceneCarryingAssemblySnapshotAsync(
            string sceneName,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var snap = AssemblyStateCapture.TryCaptureActiveScene();
            if (snap != null)
                AssemblyHandoffSession.SetAssemblySnapshot(snap);
            return LoadAssemblySceneAsync(sceneName, cancellationToken, timing);
        }
    }
}
