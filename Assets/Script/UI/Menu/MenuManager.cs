using UnityEngine;
using UnityEngine.SceneManagement; // �o�O���J�����һݪ��֤ߩR�W�Ŷ�

public class MenuManager : MonoBehaviour
{
    // �o�Ӥ�k�Ω�q�D�������C������ (SampleScene)
    public void StartGame()
    {
        Debug.Log("���J�C������...");
        SceneManager.LoadScene("SampleScene"); // ���J�W�٬� "SampleScene" ������
    }
    public void StartOnePlayer()
    {
        Debug.Log("���J�C������...");
        SceneManager.LoadScene("SampleScene"); // ���J�W�٬� "SampleScene" ������
        GameManager.Instance.PlayMod = GameManager.playMode.one;
    }
    public void StartTwoPlayer() 
    {
        Debug.Log("���J�C������...");
        SceneManager.LoadScene("SampleScene"); // ���J�W�٬� "SampleScene" ������
    }
    /*
    // �o�Ӥ�k�Ω�q�C��������^�D��� (Main)
    public void BackToMainMenu()
    {
        Debug.Log("��^�D���...");
        SceneManager.LoadScene("Main"); // ���J�W�٬� "Main" ������
    }
    */
    // �o�Ӥ�k�Ω�h�X�C��
    public void QuitGame()
    {
        Debug.Log("�h�X�C��...");
        Application.Quit();

        // �H�U�N�X�u�b Unity �s�边���B��ɥͮġA�Ω󰱤��Ҧ�
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}