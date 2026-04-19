using UnityEngine;

public class MenuCanvasUI : MonoBehaviour
{
    public void OnStartButtonClicked()
    {
        Debug.Log("[MenuCanvasUI] Start Button Clicked!");
        MainFlowManager.Instance.StartGame();
    }
    
    public void OnExitButtonClicked()
    {
        Debug.Log("[MenuCanvasUI] Exit Button Clicked!");
        Application.Quit();
    }
}
