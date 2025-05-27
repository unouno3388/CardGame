using UnityEngine;
using UnityEngine.SceneManagement; // 這是載入場景所需的核心命名空間

public class MenuManager : MonoBehaviour
{
    // 這個方法用於從主選單跳轉到遊戲場景 (SampleScene)
    public void StartGame()
    {
        Debug.Log("載入遊戲場景...");
        SceneManager.LoadScene("SampleScene"); // 載入名稱為 "SampleScene" 的場景
    }
    public void StartOnePlayer()
    {
        Debug.Log("載入遊戲場景...");
        SceneManager.LoadScene("SampleScene"); // 載入名稱為 "SampleScene" 的場景
        GameManager.Instance.PlayMod = GameManager.playMode.one;
    }
    public void StartTwoPlayer() 
    {
        Debug.Log("載入遊戲場景...");
        SceneManager.LoadScene("SampleScene"); // 載入名稱為 "SampleScene" 的場景
    }
    /*
    // 這個方法用於從遊戲場景返回主選單 (Main)
    public void BackToMainMenu()
    {
        Debug.Log("返回主選單...");
        SceneManager.LoadScene("Main"); // 載入名稱為 "Main" 的場景
    }
    */
    // 這個方法用於退出遊戲
    public void QuitGame()
    {
        Debug.Log("退出遊戲...");
        Application.Quit();

        // 以下代碼只在 Unity 編輯器中運行時生效，用於停止播放模式
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}