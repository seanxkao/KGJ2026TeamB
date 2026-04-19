#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;

/// <summary>
/// 進入 Play Mode 時把 Editor 的 Input System 路由切到與 Player 較一致的行為，
/// 避免指標與鍵盤輸入被其他 EditorWindow 吃掉。
/// </summary>
[InitializeOnLoad]
internal static class AssemblyPlayModeInputRouting
{
    static AssemblyPlayModeInputRouting()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;

        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
}
#endif
