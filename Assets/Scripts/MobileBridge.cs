using UnityEngine;
using System.Runtime.InteropServices; // 必須加這行，才能跟 JS 通訊

public class MotionBridge : MonoBehaviour
{
    // 1. 【宣告】這行是連接 .jslib 的橋樑
    [DllImport("__Internal")]
    private static extern void StartHeartbeat();

    [Header("原始數據監控")]
    public string latestRawValue;

    void Start()
    {
        // 2. 【開關】程式啟動時，去叫 JS 開始跑計時器
        // 只有在 WebGL 網頁版才會真的執行
        #if !UNITY_EDITOR && UNITY_WEBGL
            StartHeartbeat();
        #endif
    }

    // 3. 【接收】JS 的 SendMessage 會把資料丟進這裡
    public void SetSensorValue(string input)
    {
        latestRawValue = input;
        Debug.Log($"[管線測試] 收到字串: {input}");
    }
}