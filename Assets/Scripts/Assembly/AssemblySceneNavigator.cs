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
    }
}
