using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuCanvasUI : MonoBehaviour
{
    public void OnStartButtonClicked()
    {
        Debug.Log("[MenuCanvasUI] Start Button Clicked!");
        var flow = MainFlowManager.Instance;
        if (flow != null)
        {
            flow.StartGame();
            return;
        }

        Debug.LogWarning(
            "[MenuCanvasUI] 找不到 MainFlowManager（可能未從含 Manager 的 Menu 場景進入）。改為直接載入爪機場景，跨場流程資料可能未初始化。",
            this);
        SceneManager.LoadScene(MainFlowManager.SceneClaw);
    }

    public void OnExitButtonClicked()
    {
        Debug.Log("[MenuCanvasUI] Exit Button Clicked!");
        Application.Quit();
    }
}
