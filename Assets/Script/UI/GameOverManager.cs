using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
public class GameOverManager : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public Button returnToMainMenuButton;

    public void Awake()
    {
        if (returnToMainMenuButton != null)
            returnToMainMenuButton.onClick.AddListener(BackToMainMenu); // 設定按鈕點擊事件       
    }
    public void ShowgameOverText()
    {
        gameOverPanel.SetActive(true);
    }

    public void BackToMainMenu()
    {
        Debug.Log("MenuManager: BackToMainMenu() 方法已被呼叫。嘗試載入 Main 場景。");
        // 在返回主介面之前，確保時間刻度恢復正常，以防之前被暫停
        Time.timeScale = 1;
        //SceneManager.LoadScene("Main");
        SceneLoadingManager.Instance.LoadMainMenuScene(); // 使用 SceneLoadingManager 來載入主選單場景
    }
        // ============== 以下是為遊戲結束視窗添加的方法 ==============

    // 在遊戲結束時呼叫此方法來顯示結束視窗
    public void ShowGameOverPanel(string message, bool isWin)
    {
        Debug.Log("MenuManager: ShowgameOverPanel() 被呼叫了。訊息: " + message);

        if (gameOverPanel != null)
        {
            Debug.Log("MenuManager: gameOverPanel 存在，嘗試啟用它。當前狀態 (在啟用前): " + gameOverPanel.activeSelf);
            gameOverPanel.SetActive(true); // 啟用 Panel
            Debug.Log("MenuManager: gameOverPanel 啟用後狀態: " + gameOverPanel.activeSelf);

            // 檢查 Panel 自身的 Image 透明度
            /*Image panelImage = gameOverPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                Debug.Log("gameOverPanel Image Alpha: " + panelImage.color.a);
            }*/

            if (gameOverText != null)
            {
                Debug.Log("MenuManager: gameOverText Text 存在，設定其文字為: " + message);
                gameOverText.text = message; // 設定結束訊息

                // 檢查文字顏色和透明度
                Debug.Log("gameOverText Text Color: " + gameOverText.color + " (Alpha: " + gameOverText.color.a + ")");
                // 為了測試，強制文字變色，這樣可以確保它不是因為顏色問題看不到
                gameOverText.color = Color.red; // 強制為紅色
            }
            else
            {
                Debug.LogWarning("MenuManager: gameOverText (Text) 未設定。將無法顯示遊戲結束訊息。");
            }

            // 檢查 Panel 及其所有子物件的啟用狀態和渲染狀態
            Debug.Log("--- 檢查 gameOverPanel 子物件的狀態 ---");
            foreach (Transform child in gameOverPanel.transform)
            {
                Debug.Log("  子物件名稱: " + child.name + ", GameObject 啟用狀態: " + child.gameObject.activeSelf);
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    Debug.Log("  子物件 " + child.name + " 的 Renderer 啟用狀態: " + childRenderer.enabled);
                }
                CanvasRenderer childCanvasRenderer = child.GetComponent<CanvasRenderer>();
                if (childCanvasRenderer != null)
                {
                    Debug.Log("  子物件 " + child.name + " 的 CanvasRenderer 透明度: " + childCanvasRenderer.GetAlpha());
                }
            }
            Debug.Log("--- 結束檢查 ---");

        }
        else
        {
            Debug.LogError("MenuManager: gameOverPanel 未在 MenuManager 中設定！"); // 這條你說不會出現了
        }
    }

    // 在遊戲結束時呼叫此方法來隱藏結束視窗
    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false); // 禁用 Panel
        }
    }
}
