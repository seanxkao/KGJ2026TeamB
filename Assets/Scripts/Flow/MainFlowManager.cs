using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainFlowManager : MonoBehaviour
{
    private static MainFlowManager instance;

    public static MainFlowManager Instance => instance;

    public List<string> ClawToyIds
    {
        get; private set;
    } = new List<string>();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Claw");
        ClawToyIds.Clear();
    }

    public void StartAssembly()
    {
        SceneManager.LoadScene("AssemblyScene");
    }

    public void StartBattle()
    {
        SceneManager.LoadScene("Battle");
    }

    public void SetClawToyIds(List<string> ids)
    {
        ClawToyIds.AddRange(ids);
    }
}
