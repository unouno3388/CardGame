// SceneLoadingManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadingManager : MonoBehaviour
{
    public static SceneLoadingManager Instance { get; private set; }

    // 在這裡定義場景名稱的常數，方便管理和修改
    public const string MainMenuSceneName = "Main";
    public const string GameSceneName = "SampleScene"; 
    // 遊戲場景名稱可以從 GameManager 獲取或在這裡也定義一個
    // 為了簡單起見，我們假設 GameManager.GameSceneName 是可訪問的靜態成員或常數
    // public const string GameSceneName = "SampleScene"; // 或者 GameManager.GameSceneName
    //public Action onSceneLoadedEvent; // 可以用來通知其他系統場景已經載入
    public Dictionary<string, Action> sceneLoadCallbacks = new Dictionary<string, Action>();
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #region  場景載入處理
    void OnEnable()
    {
        Debug.Log("MenuManager: OnEnable() 方法被呼叫。註冊場景載入事件。");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        Debug.Log("MenuManager: OnDisable() 方法被呼叫。取消註冊場景載入事件。");
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
       // 【新增】場景載入完成時的處理函式
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"SceneLoadingManager: OnSceneLoaded called for scene '{scene.name}' with mode '{mode}'.");
        if (sceneLoadCallbacks.TryGetValue(scene.name, out Action callback))
        {
            callback?.Invoke(); // 如果有註冊的回調，則執行它
        }
        //Debug.Log($"SceneLoadingManager: Scene '{scene.name}' loaded with mode '{mode}'.");
       
    }
    public void RegisterSceneLoadCallback(string sceneName, Action callback)
    {
        if (string.IsNullOrEmpty(sceneName) || callback == null)
        {
            Debug.LogError("SceneLoadingManager: Invalid scene name or callback!");
            return;
        }
        if (!sceneLoadCallbacks.ContainsKey(sceneName))
        {
            sceneLoadCallbacks[sceneName] = callback;
            Debug.Log($"SceneLoadingManager: Registered callback for scene '{sceneName}'.");
        }
        else
        {
            Debug.LogWarning($"SceneLoadingManager: Callback for scene '{sceneName}' already exists. Overwriting.");
            sceneLoadCallbacks[sceneName] = callback; // 覆蓋已存在的回調
        }
    }
    #endregion
    public void LoadMainMenuScene()
    {
        Debug.Log($"SceneLoadingManager: Loading Main Menu Scene ({MainMenuSceneName})...");
        // 返回主選單前，確保時間恢復正常
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void LoadGameScene()
    {
        // 遊戲場景的名稱可以從 GameManager 獲取，以保持一致性
        // 假設 GameManager.GameSceneName 存在且已設定
        string gameSceneToLoad = GameSceneName;//GameManager.GameSceneName; //
        Debug.Log($"SceneLoadingManager: Loading Game Scene ({gameSceneToLoad})...");
        // 在載入遊戲場景前，GameManager.initialGameMode 應該已經由呼叫者（如MenuManager）設定好了
        SceneManager.LoadScene(gameSceneToLoad);
    }

    // 你也可以提供一個通用的按名稱載入場景的方法
    public void LoadSceneByName(string sceneName)
    {
        Debug.Log($"SceneLoadingManager: Loading Scene by name ({sceneName})...");
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("SceneLoadingManager: Scene name cannot be null or empty!");
            return;
        }
        // 如果是主選單，確保時間正確
        if (sceneName == MainMenuSceneName)
        {
            Time.timeScale = 1f;
        }
        SceneManager.LoadScene(sceneName);
    }

    // 未來可以擴展非同步載入等功能
    // public IEnumerator LoadSceneAsync(string sceneName)
    // {
    //     // 顯示載入畫面
    //     AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
    //     while (!operation.isDone)
    //     {
    //         // 更新進度條
    //         float progress = Mathf.Clamp01(operation.progress / 0.9f);
    //         Debug.Log("Loading progress: " + (progress * 100) + "%");
    //         yield return null;
    //     }
    //     // 隱藏載入畫面
    // }
}