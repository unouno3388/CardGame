using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening; // 如果還在使用 DOTween
using System.Collections;
using System.Linq; // 【新增】

public class UIManager : MonoBehaviour
{
    [Header("手牌面板")]
    public Transform playerHandPanel;
    public Transform opponentHandPanel;
    [Header("場地面板")]
    public Transform playerFieldPanel;
    public Transform opponentFieldPanel;
    [Header("數值與訊息面板")]
    public Text playerHealthText;
    public Text opponentHealthText;
    public Text playerManaText;
    public Text opponentManaText; // 【新增】如果需要顯示對手（AI）的法力
    public Text gameOverText;
    public Image playerHealthBar; // 玩家生命條
    public Image opponentHealthBar; // 對手生命條
    [Header("卡牌預置體")]
    public GameObject cardPrefab;
    [Header("卡牌間距")]
    public float cardSpacing = 10f;

    // 【新增】房間 UI 元素引用
    public GameObject roomPanel; // 整個房間控制面板
    public InputField roomIdInputField; // 輸入房間ID的輸入框
    public Button createRoomButton;
    public Button joinRoomButton;
    public Button leaveRoomButton; // 離開房間按鈕
    public Text roomStatusText; // 顯示房間ID、狀態、錯誤訊息等

    // 【新增】連接狀態/通用訊息提示
    public GameObject connectingMessagePanel; // 一個用於顯示"連接中..."或"已斷開"的面板
    public Text connectingMessageText;


    private List<GameObject> playerHandCardObjects = new List<GameObject>();
    private List<GameObject> opponentHandCardObjects = new List<GameObject>();
    private List<GameObject> playerFieldCardObjects = new List<GameObject>();
    private List<GameObject> opponentFieldCardObjects = new List<GameObject>();

    public Transform animationContainer;

    void Awake()
    {
        // ... (原有的 Awake 內容保持不變)
        EnsureLayoutGroup(playerHandPanel); //
        EnsureLayoutGroup(opponentHandPanel); //
        EnsureLayoutGroup(playerFieldPanel); //
        EnsureLayoutGroup(opponentFieldPanel); //

        // 【新增】為房間按鈕添加監聽器
        if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);

        // 初始時隱藏一些面板
        if (roomPanel != null) roomPanel.SetActive(false);
        if (connectingMessagePanel != null) connectingMessagePanel.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(false);
    }

    void EnsureLayoutGroup(Transform panel)
    {
        // ... (原有的 EnsureLayoutGroup 內容保持不變)
        if (panel == null) return; //
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>(); //
        if (layout == null) //
        {
            layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>(); //
        }
        layout.padding = new RectOffset(10, 10, 10, 10); //
        layout.spacing = cardSpacing; //
        layout.childAlignment = TextAnchor.MiddleCenter; //
        layout.childForceExpandWidth = false; //
        layout.childForceExpandHeight = false; //
    }

    public void UpdateUI()
    {
        if (GameManager.Instance == null) return;

        GameManager gm = GameManager.Instance;

        if (playerHealthText != null) playerHealthText.text = "玩家生命: " + gm.playerHealth;
        if (opponentHealthText != null) opponentHealthText.text = "對手生命: " + gm.opponentHealth;
        if (playerManaText != null) playerManaText.text = "法力: " + gm.playerMana + "/" + gm.maxMana;
        if (playerHealthBar != null) playerHealthBar.fillAmount = gm.playerHealth / 30f;//(float)gm.maxHealth;
        if (opponentHealthBar != null) opponentHealthBar.fillAmount = gm.opponentHealth / 30f;//(float)gm.maxHealth;
        // 【新增】顯示對手（AI或線上玩家）的法力（如果遊戲設計如此）
        if (opponentManaText != null) {
            if (gm.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer ||
                gm.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI ||
                (gm.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && gm.IsInRoom && gm.OpponentPlayerId != null)) // 確保在房間且有對手
            {
                opponentManaText.text = "對手法力: " + gm.opponentMana + "/" + gm.opponentMaxMana;
                opponentManaText.gameObject.SetActive(true);
            } else {
                opponentManaText.gameObject.SetActive(false); // 其他情況下隱藏
            }
        }


        UpdateHand(gm.playerHand, playerHandPanel, true, playerHandCardObjects);

        // 【修改】對手手牌的處理
        // 【修改】對手手牌的處理，針對 OnlineSinglePlayerAI
        if (gm.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
        {
            UpdateHand(gm.opponentHand, opponentHandPanel, false, opponentHandCardObjects); // 離線AI顯示實際手牌（通常是卡背）
        }
        else if (gm.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI)
        {
            // 線上AI模式，根據數量顯示卡背
            UpdateOpponentHandOnlineAI(gm.opponentServerHandCount, opponentHandPanel, opponentHandCardObjects);
        }
        else if (gm.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom) // 線上房間模式
        {
            // 房間模式的對手手牌處理 (可能也是顯示數量或卡背，或根據已有邏輯)
            // ClearPanel(opponentHandPanel, opponentHandCardObjects); // 簡單清空
            // int opponentRoomHandCount = ... // 從房間狀態獲取
            // for (int i = 0; i < opponentRoomHandCount; i++) { CreateCardBack(...); }
            // 暫時保持您之前的 ClearPanel 邏輯，您可以後續擴展
            ClearPanel(opponentHandPanel, opponentHandCardObjects);
            // 如果您想在房間模式也顯示對手卡背數量，可以類似 OnlineSinglePlayerAI 處理
        }


        UpdateField(gm.playerField, playerFieldPanel, true, playerFieldCardObjects);
        UpdateField(gm.opponentField, opponentFieldPanel, false, opponentFieldCardObjects);

        // 更新房間按鈕的顯示狀態
        if (roomPanel != null && roomPanel.activeSelf) {
             if (leaveRoomButton != null) leaveRoomButton.gameObject.SetActive(gm.IsInRoom); // 只有在房間內才顯示離開按鈕
             // ToggleRoomJoinCreateButtons(!gm.IsInRoom); // 在房間內時隱藏創建和加入按鈕
        }
    }

    void UpdateHand(List<Card> handData, Transform panel, bool isPlayer, List<GameObject> handCardObjectsList)
    {
        // ... (此方法大部分保持不變，主要是 Initialize 中的 isPlayer 參數很重要)
        if (panel == null) return;
        // 確保 isPlayer 為 true 時才詳細處理玩家手牌，對手手牌由 UpdateOpponentHandOnlineAI 處理
        if (!isPlayer && GameManager.Instance.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI) {
            // 在Online AI模式下，對手手牌由UpdateOpponentHandOnlineAI處理，這裡直接返回或只做清理
            // ClearPanel(panel, handCardObjectsList); // 避免這裡干擾
            return;
        }
        if (!isPlayer && GameManager.Instance.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom) {
            // 在房間模式下，對手手牌也可能由特定邏輯處理，這裡暫時返回
            return;
        }

        Debug.Log($"UpdateHand for {(isPlayer ? "PLAYER" : "OFFLINE_AI")}: Data count = {handData.Count}, UI Object count = {handCardObjectsList.Count}");

        // 1. 銷毀不再存在於手牌數據中的舊UI物件    
        List<GameObject> toRemove = new List<GameObject>(); //
        foreach (var cardObj in handCardObjectsList) //
        {
            if (cardObj == null) continue; //
            CardUI cardUIComp = cardObj.GetComponent<CardUI>(); //
            if (cardUIComp == null || cardUIComp.card == null || !handData.Any(c => c.id == cardUIComp.card.id)) // 改用ID比較，因為物件實例可能不同
            {
                toRemove.Add(cardObj); //
                //Debug.Log("cardObj Name" + cardObj.name + " is no longer in handData, will be removed."); //
            }
        }

        foreach (var cardObjToRemove in toRemove) //
        {
            handCardObjectsList.Remove(cardObjToRemove); //
            Destroy(cardObjToRemove); //
        }

        // 2. 更新現有或創建新的UI物件以匹配手牌數據
        foreach (var cardData in handData) //
        {
            if (cardData == null) continue; //

            GameObject existingCardObj = handCardObjectsList.Find(obj => obj != null && obj.GetComponent<CardUI>()?.card?.id == cardData.id); //

            if (existingCardObj == null) //
            {
                GameObject newCardObj = Instantiate(cardPrefab, panel); //
                CardUI cardUI = newCardObj.GetComponent<CardUI>(); //
                if (cardUI != null) //
                {
                    cardUI.Initialize(cardData, isPlayer, false); // isPlayer 參數很重要
                    handCardObjectsList.Add(newCardObj); //

                    // ... (原有的 RectTransform 設置)
                }
                else
                {
                    Destroy(newCardObj); //
                }
            } else {
                 // 【可選】如果卡牌數據可能更新（例如狀態變化），在這裡更新現有卡牌UI
                 // existingCardObj.GetComponent<CardUI>().Initialize(cardData, isPlayer, false);
            }
        }
        // ... (原有的 LayoutRebuilder)
    }
    // 【新增】專門用於更新線上AI對手手牌UI（顯示卡背）
    void UpdateOpponentHandOnlineAI(int handCount, Transform panel, List<GameObject> handCardObjectsList) {
        if (panel == null) return;
        // Debug.Log($"Updating Online AI Opponent Hand: {handCount} cards.");

        // 1. 清理現有的UI物件
        ClearPanel(panel, handCardObjectsList);

        // 2. 根據數量創建卡背
        for (int i = 0; i < handCount; i++) {
            GameObject cardBackObj = Instantiate(cardPrefab, panel);
            CardUI cardUI = cardBackObj.GetComponent<CardUI>();
            if (cardUI != null) {
                // 使用一個空的或標記性的 Card 數據來初始化為卡背
                // CardUI.Initialize 方法需要能處理 cardData 為 null 或 cardData.sprite 為 null 的情況，並顯示卡背
                cardUI.Initialize(new Card { name = "OpponentCardBack" }, false, false); // isPlayer=false, isField=false
            }
            handCardObjectsList.Add(cardBackObj);
        }
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
        }
    }
    void UpdateField(List<Card> fieldData, Transform panel, bool isPlayer, List<GameObject> fieldCardObjectsList)
    {
        // ... (此方法與 UpdateHand 類似，比較時也建議用卡牌 ID)
        if (panel == null) return;

        List<GameObject> toRemove = new List<GameObject>();
        foreach (var cardObj in fieldCardObjectsList)
        {
            if (cardObj == null) continue;
            CardUI cardUIComp = cardObj.GetComponent<CardUI>();
            if (cardUIComp == null || cardUIComp.card == null || !fieldData.Any(c => c.id == cardUIComp.card.id))
            {
                toRemove.Add(cardObj);
            }
        }
        foreach (var cardObjToRemove in toRemove)
        {
            fieldCardObjectsList.Remove(cardObjToRemove);
            Destroy(cardObjToRemove);
        }

        foreach (var cardData in fieldData)
        {
            if (cardData == null) continue;
            GameObject existingCardObj = fieldCardObjectsList.Find(obj => obj != null && obj.GetComponent<CardUI>()?.card?.id == cardData.id);
            if (existingCardObj == null)
            {
                GameObject newCardObj = Instantiate(cardPrefab, panel);
                CardUI cardUI = newCardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(cardData, isPlayer, true); // isField 為 true
                    fieldCardObjectsList.Add(newCardObj);
                    // ... (原有的 RectTransform 設置)
                }
                else
                {
                    Destroy(newCardObj);
                }
            }
        }
        // ... (原有的 LayoutRebuilder)
    }

    // 【新增】根據卡牌名稱獲取 Sprite (你需要實現這個邏輯)
    public Sprite GetCardSpriteByName(string cardName) {
        // 示例：假設你所有的卡牌 Sprite 都放在 Resources/CardSprites/ 文件夾下
        // 並且 Sprite 的名稱與卡牌的 name 完全對應 (可能需要去除 "#數字" 後綴)
        // string spriteName = cardName.Split('#')[0].Trim(); // 簡單處理
        // return Resources.Load<Sprite>("CardSprites/" + spriteName);
        // 更健壯的做法是使用 Addressables 或一個 Sprite Atlas 管理器
        // Debug.LogWarning($"GetCardSpriteByName: Sprite for '{cardName}' not implemented yet. Returning null.");
        return null; // 暫時返回 null
    }
        public GameObject FindPlayerHandCardObjectById(string cardId) {
        foreach (var cardObj in playerHandCardObjects) { // 假設 playerHandCardObjects 是最新的
            if (cardObj != null) {
                CardUI cui = cardObj.GetComponent<CardUI>();
                if (cui != null && cui.card != null && cui.card.id == cardId) {
                    return cardObj;
                }
            }
        }
        // 如果 playerHandCardObjects 不是最新的，或者作為備用方案，可以遍歷 panel 的子物件
        // Debug.LogWarning($"UIManager: Card with ID {cardId} not found in playerHandCardObjects tracking list. Trying direct panel search (less efficient).");
        // foreach (Transform child in playerHandPanel) {
        //     if (child == null) continue;
        //     CardUI cui = child.GetComponent<CardUI>();
        //     if (cui != null && cui.card != null && cui.card.id == cardId) {
        //         return child.gameObject;
        //     }
        // }
        Debug.LogWarning($"UIManager: Card with ID {cardId} not found in player's hand UI for animation.");
        return null;
    }

    public void PlayCardWithAnimation(Card card, bool isPlayer, GameObject cardObject, Transform sourcePanel, Transform targetPanel)
    {
        // ... (此方法大部分保持不變，但其調用時機在線上模式下可能由伺服器確認後觸發)
        if (card == null || cardObject == null || targetPanel == null) //
        {
            //if (cardObject != null) CardUI.IsAnimationPlaying = false; //
            if (CardAnimationManager.Instance != null) CardAnimationManager.Instance.SetAnimationPlaying(false);
            return; //
        }

        bool removed = false; //
        if (isPlayer) //
        {
            removed = playerHandCardObjects.Remove(cardObject); //
        }
        else
        {
            removed = opponentHandCardObjects.Remove(cardObject); //
        }

        //CardUI.IsAnimationPlaying = true; //
        if (CardAnimationManager.Instance != null) CardAnimationManager.Instance.SetAnimationPlaying(true);

        CardAnimationManager.Instance.PlayCardAnimation(card, isPlayer, cardObject, targetPanel, () =>
        {
            // 在線上模式，卡牌效果的實際應用和狀態檢查由伺服器完成。
            // 客戶端這裡主要是在動畫結束後，如果需要，可以觸發一次UI刷新或等待伺服器的gameStateUpdate
            if (GameManager.Instance.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer) {
                card.ApplyCardEffect(isPlayer); // 離線模式才直接應用效果
                GameManager.Instance.CheckGameOver(); //
            }
            //CardUI.IsAnimationPlaying = false; //
            if (CardAnimationManager.Instance != null) CardAnimationManager.Instance.SetAnimationPlaying(false); // 【新增】重置動畫狀態

            StartCoroutine(DelayedUpdateUI()); //
        });
    }

    private IEnumerator DelayedUpdateUI()
    {
        // ... (原方法)
        yield return new WaitForSeconds(0.1f); //
        if (GameManager.Instance != null && GameManager.Instance.UIAutoUpdate) //
        {
            UpdateUI(); // 【修改】直接調用 UIManager 自己的 UpdateUI
        }
    }


    public GameObject GetOpponentHandCardObject(Card card)
    {
        // ... (原方法)
        foreach (var cardObj in opponentHandCardObjects) //
        {
            if (cardObj != null) //
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>(); //
                if (cardUI != null && cardUI.card == card && cardObj.activeInHierarchy) //
                {
                    return cardObj; //
                }
            }
        }
        return null; //
    }

    public void ShowGameOver(string message)
    {
        if (gameOverText != null)
        {
            gameOverText.text = message;
            gameOverText.gameObject.SetActive(true);
        }
        // 【新增】遊戲結束時，可能需要隱藏房間面板
        if (roomPanel != null) roomPanel.SetActive(false);
        if (connectingMessagePanel != null) connectingMessagePanel.SetActive(false);
    }

    // --- 【新增】房間 UI 相關方法 ---
    public void SetRoomPanelActive(bool isActive) {
        if (roomPanel != null) {
            roomPanel.SetActive(isActive);
            if (isActive) {
                // 當房間面板激活時，根據是否已在房間內來決定顯示哪些按鈕
                bool isInRoom = GameManager.Instance.IsInRoom;
                ToggleRoomJoinCreateButtons(!isInRoom);
                if (leaveRoomButton != null) leaveRoomButton.gameObject.SetActive(isInRoom);
            }
        }
    }

    public void UpdateRoomStatus(string status) {
        if (roomStatusText != null) {
            roomStatusText.text = status;
        }
    }

    // 切換創建/加入按鈕的可用性
    public void ToggleRoomJoinCreateButtons(bool showJoinCreate) {
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(showJoinCreate);
        if (joinRoomButton != null) joinRoomButton.gameObject.SetActive(showJoinCreate);
        if (roomIdInputField != null) roomIdInputField.gameObject.SetActive(showJoinCreate);
    }


    private void OnCreateRoomClicked() {
        // 這裡可以讓玩家輸入自己的名字，或者使用 GameManager.Instance.PlayerId (如果它是代表玩家名)
        // 為了簡單，我們暫時不處理玩家名輸入
        GameManager.Instance.RequestCreateRoom(GameManager.Instance.PlayerId ?? "玩家");
        UpdateRoomStatus("正在創建房間...");
        ToggleRoomJoinCreateButtons(false); // 點擊後暫時隱藏，等待伺服器回應
    }

    private void OnJoinRoomClicked() {
        string targetRoomId = roomIdInputField.text;
        if (string.IsNullOrWhiteSpace(targetRoomId)) {
            UpdateRoomStatus("錯誤：請輸入房間ID！");
            return;
        }
        GameManager.Instance.RequestJoinRoom(targetRoomId, GameManager.Instance.PlayerId ?? "玩家");
        UpdateRoomStatus($"正在加入房間 {targetRoomId}...");
        ToggleRoomJoinCreateButtons(false); // 點擊後暫時隱藏
    }

    private void OnLeaveRoomClicked() {
        GameManager.Instance.RequestLeaveRoom();
        UpdateRoomStatus("正在離開房間...");
        // UI的最終狀態會在收到伺服器 "leftRoom" 確認後更新
    }

    // --- 【新增】連接訊息提示 ---
    public void ShowConnectingMessage(string message) {
        if (connectingMessagePanel != null && connectingMessageText != null) {
            connectingMessageText.text = message;
            connectingMessagePanel.SetActive(true);
        }
        if (roomPanel != null) roomPanel.SetActive(false); // 連接時隱藏房間面板
    }

    public void HideConnectingMessage() {
        if (connectingMessagePanel != null) {
            connectingMessagePanel.SetActive(false);
        }
    }
     // 【新增】顯示通用錯誤彈窗或訊息
    public void ShowErrorPopup(string errorMessage) {
        // 你可以實現一個更精美的彈出視窗，這裡暫時用 roomStatusText
        if (GameManager.Instance.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && roomStatusText != null && roomPanel.activeSelf) {
            roomStatusText.text = "錯誤:\n" + errorMessage;
        } else if (connectingMessagePanel != null && connectingMessageText != null && connectingMessagePanel.activeSelf) {
             connectingMessageText.text = "錯誤:\n" + errorMessage; // 如果正在顯示連接訊息，則更新它
        }
        else {
            //  fallback: 如果沒有特定地方顯示，就用Debug.Log
            Debug.LogError("UI ShowErrorPopup: " + errorMessage);
            // 考慮一個全局的錯誤提示UI元素
        }
    }


    // 清理指定的 Panel 及其追蹤列表
    private void ClearPanel(Transform panel, List<GameObject> trackingList)
    {
        foreach (var obj in trackingList)
        {
            if (obj != null) Destroy(obj);
        }
        trackingList.Clear();
        // 如果 panel 仍有子物件不是由 trackingList 管理的，也一併清理
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Destroy(panel.GetChild(i).gameObject);
        }
        // 確保 Panel 下沒有殘留的子物件 (如果它們不是由 trackingList 管理的)
        // for (int i = panel.childCount - 1; i >= 0; i--) {
        //     Destroy(panel.GetChild(i).gameObject);
        // }
    }
}