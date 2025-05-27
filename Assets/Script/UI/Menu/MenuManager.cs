using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void StartOnePlayer()
    {
        Debug.Log("Starting single-player game...");
        // 在載入場景前設定遊戲模式
        GameManager.initialGameMode = GameManager.GameMode.OfflineSinglePlayer;
        SceneManager.LoadScene("SampleScene"); // 載入名稱為 "SampleScene" 的場景
    }

    public void StartTwoPlayer()
    {
        Debug.Log("Starting two-player (online) game...");
        // 在載入場景前設定遊戲模式
        GameManager.initialGameMode = GameManager.GameMode.OnlineMultiplayer;
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}