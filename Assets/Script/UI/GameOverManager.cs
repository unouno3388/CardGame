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
            returnToMainMenuButton.onClick.AddListener(BackToMainMenu); // �]�w���s�I���ƥ�       
    }
    public void ShowgameOverText()
    {
        gameOverPanel.SetActive(true);
    }

    public void BackToMainMenu()
    {
        Debug.Log("MenuManager: BackToMainMenu() ��k�w�Q�I�s�C���ո��J Main �����C");
        // �b��^�D�������e�A�T�O�ɶ���׫�_���`�A�H�����e�Q�Ȱ�
        Time.timeScale = 1;
        //SceneManager.LoadScene("Main");
        SceneLoadingManager.Instance.LoadMainMenuScene(); // �ϥ� SceneLoadingManager �Ӹ��J�D������
    }
        // ============== �H�U�O���C�����������K�[����k ==============

    // �b�C�������ɩI�s����k����ܵ�������
    public void ShowGameOverPanel(string message, bool isWin)
    {
        Debug.Log("MenuManager: ShowgameOverPanel() �Q�I�s�F�C�T��: " + message);

        if (gameOverPanel != null)
        {
            Debug.Log("MenuManager: gameOverPanel �s�b�A���ձҥΥ��C��e���A (�b�ҥΫe): " + gameOverPanel.activeSelf);
            gameOverPanel.SetActive(true); // �ҥ� Panel
            Debug.Log("MenuManager: gameOverPanel �ҥΫ᪬�A: " + gameOverPanel.activeSelf);

            // �ˬd Panel �ۨ��� Image �z����
            /*Image panelImage = gameOverPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                Debug.Log("gameOverPanel Image Alpha: " + panelImage.color.a);
            }*/

            if (gameOverText != null)
            {
                Debug.Log("MenuManager: gameOverText Text �s�b�A�]�w���r��: " + message);
                gameOverText.text = message; // �]�w�����T��

                // �ˬd��r�C��M�z����
                Debug.Log("gameOverText Text Color: " + gameOverText.color + " (Alpha: " + gameOverText.color.a + ")");
                // ���F���աA�j���r�ܦ�A�o�˥i�H�T�O�����O�]���C����D�ݤ���
                gameOverText.color = Color.red; // �j�����
            }
            else
            {
                Debug.LogWarning("MenuManager: gameOverText (Text) ���]�w�C�N�L�k��ܹC�������T���C");
            }

            // �ˬd Panel �Ψ�Ҧ��l���󪺱ҥΪ��A�M��V���A
            Debug.Log("--- �ˬd gameOverPanel �l���󪺪��A ---");
            foreach (Transform child in gameOverPanel.transform)
            {
                Debug.Log("  �l����W��: " + child.name + ", GameObject �ҥΪ��A: " + child.gameObject.activeSelf);
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    Debug.Log("  �l���� " + child.name + " �� Renderer �ҥΪ��A: " + childRenderer.enabled);
                }
                CanvasRenderer childCanvasRenderer = child.GetComponent<CanvasRenderer>();
                if (childCanvasRenderer != null)
                {
                    Debug.Log("  �l���� " + child.name + " �� CanvasRenderer �z����: " + childCanvasRenderer.GetAlpha());
                }
            }
            Debug.Log("--- �����ˬd ---");

        }
        else
        {
            Debug.LogError("MenuManager: gameOverPanel ���b MenuManager ���]�w�I"); // �o���A�����|�X�{�F
        }
    }

    // �b�C�������ɩI�s����k�����õ�������
    public void HideGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false); // �T�� Panel
        }
    }
}
