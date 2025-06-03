using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class MenuManager : MonoBehaviour
{
    const string startOnePlayerButtonName = "Single player";
    const string startOnlineSinglePlayerAIButtonName = "OnlineSinglePlayerAI";
    const string startOnlineMultiplayerRoomButtonName = "OnlineMultiplayerRoom";
    const string quitGameButtonName = "Exit";
    [HideInInspector]
    public GameOverManager gameOverManager;
    public Button startOnePlayerButton;
    public Button startOnlineSinglePlayerAIButton;
    public Button startOnlineMultiplayerRoomButton;
    public Button quitGameButton;
    void Start()
    {
        //OnSceneLoaded();

        //SceneLoadingManager.Instance.RegisterSceneLoadCallback(SceneLoadingManager.MainMenuSceneName, OnSceneLoaded);
        
        if (startOnePlayerButton == null) Debug.LogError("MenuManager: startOnePlayerButton not found!");
        else startOnePlayerButton.onClick.AddListener(StartOnePlayer); // 設定按鈕點擊事件
        if (startOnlineSinglePlayerAIButton == null) Debug.LogError("MenuManager: startOnlineSinglePlayerAIButton not found!");
        else startOnlineSinglePlayerAIButton.onClick.AddListener(StartOnlineSinglePlayerAI); // 設定按鈕點擊事件
        if (startOnlineMultiplayerRoomButton == null) Debug.LogError("MenuManager: startOnlineMultiplayerRoomButton not found!");
        else startOnlineMultiplayerRoomButton.onClick.AddListener(StartOnlineMultiplayerRoom); // 設定按鈕點擊事件
        if (quitGameButton == null) Debug.LogError("MenuManager: quitGameButton not found!");
        else quitGameButton.onClick.AddListener(QuitGame); // 設定按鈕點擊事件
    }
    public void StartOnePlayer()
    {
        Debug.Log("Starting offline single-player game...");
        GameManager.initialGameMode = GameManager.GameMode.OfflineSinglePlayer; //
        SceneManager.LoadScene("SampleScene"); // 載入名稱為 "SampleScene" 的場景
    }

    // 【新增】啟動線上單人 AI 對戰模式
    public void StartOnlineSinglePlayerAI()
    {
        Debug.Log("Starting online single-player game (Server AI)...");
        GameManager.initialGameMode = GameManager.GameMode.OnlineSinglePlayerAI; // 新的遊戲模式
        //SceneManager.LoadScene("SampleScene");
        SceneLoadingManager.Instance.LoadGameScene(); // 使用 SceneLoadingManager 來載入遊戲場景
    }

    // 【新增】啟動線上雙人房間模式
    public void StartOnlineMultiplayerRoom()
    {
        Debug.Log("Starting online two-player (Room) game...");
        GameManager.initialGameMode = GameManager.GameMode.OnlineMultiplayerRoom; // 新的遊戲模式
        //SceneManager.LoadScene("SampleScene");
        SceneLoadingManager.Instance.LoadGameScene();
    }

    // 原始的 StartTwoPlayer 方法，如果仍然需要，可以保留或根據新的房間模式調整
    // public void StartTwoPlayer()
    // {
    //     Debug.Log("Starting two-player (online) game...");
    //     // 在載入場景前設定遊戲模式
    //     GameManager.initialGameMode = GameManager.GameMode.OnlineMultiplayer; // // 這個模式可能被 OnlineMultiplayerRoom 取代
    //     SceneManager.LoadScene("SampleScene"); //
    // }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }


}