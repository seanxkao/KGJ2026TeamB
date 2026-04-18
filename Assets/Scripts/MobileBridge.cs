using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine.UI;

public class MotionBridge : MonoBehaviour
{
    public enum ConnectionState { Idle, Waiting, Connected, Error }

    [DllImport("__Internal")]
    private static extern void InitPeerReceiver();

    [Header("狀態監控")]
    public ConnectionState connectionState = ConnectionState.Idle;
    public float acc;
    public float peak;

    [Header("UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider gaugeSlider;

    // 呼叫此方法開始建立 Peer（綁定到 UI 按鈕）
    public void Connect()
    {
        #if !UNITY_EDITOR && UNITY_WEBGL
            SetState(ConnectionState.Waiting);
            InitPeerReceiver();
        #else
            Debug.Log("[MobileBridge] Connect() 只在 WebGL 執行");
        #endif
    }

    // JS 的 SendMessage 呼叫此方法傳入資料或狀態訊號
    public void SetSensorValue(string input)
    {
        switch (input)
        {
            case "CONNECTED":
                SetState(ConnectionState.Connected);
                break;
            case "DISCONNECTED":
                SetState(ConnectionState.Waiting);
                break;
            case "ERROR":
                SetState(ConnectionState.Error);
                break;
            default:
                Debug.Log($"[MobileBridge] 未知訊號: {input}");
                break;
        }
    }

    public void SetAccValue(string input)
    {
        if (float.TryParse(input, out float val))
        {
            acc = val;
            if (gaugeSlider != null) gaugeSlider.value = val;
        }
    }

    public void SetPeakValue(string input)
    {
        if (float.TryParse(input, out float val))
            peak = val;
    }

    private void SetState(ConnectionState state)
    {
        connectionState = state;
        if (statusText == null) return;
        statusText.text = state switch
        {
            ConnectionState.Idle      => "Idle",
            ConnectionState.Waiting   => "Waiting for mobile...",
            ConnectionState.Connected => "Connected",
            ConnectionState.Error     => "Error",
            _                         => ""
        };
    }
}