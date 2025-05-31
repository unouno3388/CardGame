using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for Linq usage
using System.Collections; // 【新增】為了協程

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static GameMode initialGameMode = GameMode.OfflineSinglePlayer;

    public int playerHealth = 30;
    public int opponentHealth = 30;
    public int playerMana = 1; // 【修改】初始法力通常從1開始
    public int maxMana = 1;    // 【修改】
    public int opponentMana = 1; // 【修改】
    public int opponentMaxMana = 1; // 【修改】
    public bool isPlayerTurn = true;
    public int opponentServerHandCount; // 【新增】用於儲存在線AI的手牌數量
    public List<Card> playerHand = new List<Card>();
    public List<Card> opponentHand = new List<Card>(); // 在線上模式，這裡可能只存對手手牌的數量或基本信息
    public List<Card> playerField = new List<Card>();
    public List<Card> opponentField = new List<Card>();
    public List<Card> playerDeck = new List<Card>();
    public List<Card> opponentDeck = new List<Card>();

    public UIManager UIManager { get; private set; }
    private AIManager aiManager; // 在 OnlineSinglePlayerAI 模式下，這個客戶端 AI 不會被使用
    private WebSocketManager wsManager;
    public WebSocketManager WebSocketManager => wsManager;
    private CardPlayService cardPlayService;
    public CardPlayService CardPlayService => cardPlayService;

    // 【修改】遊戲模式枚舉
    public enum GameMode
    {
        OfflineSinglePlayer,
        OnlineMultiplayer, // 這個可以保留給舊的P2P模式，或者移除
        OnlineSinglePlayerAI,   // 【新增】線上AI對戰
        OnlineMultiplayerRoom   // 【新增】線上房間對戰
    }

    private GameMode currentGameMode;
    public GameMode CurrentGameMode => currentGameMode;

    // 【新增】線上模式相關狀態
    public string PlayerId { get; set; } // 由伺服器分配或客戶端生成
    public string RoomId { get; set; }   // 當前所在的房間ID
    public bool IsInRoom { get; set; } = false;
    public string OpponentPlayerId { get; set; } // 對手的ID (房間模式中)

    private bool uiAutoUpdate = true;
    public bool UIAutoUpdate
    {
        get => uiAutoUpdate;
        set => uiAutoUpdate = value;
    }

    // 【新增】後端伺服器 URL 常量
    private const string AI_SERVER_URL = "ws://localhost:8080/game/ai";
    private const string ROOM_SERVER_URL = "ws://localhost:8080/game/room";


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
            return; // 【新增】避免後續的 GetComponent 報錯
        }

        UIManager = GetComponent<UIManager>();
        aiManager = GetComponent<AIManager>();
        wsManager = GetComponent<WebSocketManager>(); // 確保 WebSocketManager 先被初始化
        cardPlayService = gameObject.AddComponent<CardPlayService>();

        if (UIManager == null || aiManager == null || wsManager == null || cardPlayService == null)
        {
            Debug.LogError("GameManager: Missing required components!");
        }
    }

    void Start()
    {
        // PlayerId 可以考慮在連接成功後由伺服器分配，或在這裡簡單生成
        // PlayerId = System.Guid.NewGuid().ToString().Substring(0, 8);
        StartGame(initialGameMode);
    }

    void InitializeGameDefaults() // 【新增】一個通用的重置方法
    {
        playerHealth = 100;
        opponentHealth = 1;
        playerMana = 30;
        maxMana = 30;
        opponentMana = 30;
        opponentMaxMana = 30;
        isPlayerTurn = true; // 初始回合歸屬可能由伺服器決定

        playerHand.Clear();
        opponentHand.Clear();
        playerField.Clear();
        opponentField.Clear();
        playerDeck.Clear();
        opponentDeck.Clear();

        IsInRoom = false;
        RoomId = null;
        OpponentPlayerId = null;
    }

    // 【修改】啟動遊戲的統一入口點
    public void StartGame(GameMode mode)
    {
        currentGameMode = mode;
        InitializeGameDefaults(); // 重置遊戲狀態

        Debug.Log($"Starting game with mode: {currentGameMode}");

        // 確保 UI Manager 在遊戲開始時更新一次房間面板的顯示狀態
        if (UIManager != null) {
            UIManager.SetRoomPanelActive(currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom);
        }


        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            InitializeOfflineGame();
        }
        else if (currentGameMode == GameMode.OnlineSinglePlayerAI)
        {
            // 連接到AI伺服器
            wsManager.ConnectToServer(AI_SERVER_URL);
            // UI可以顯示 "連接中..."
            if(UIManager != null) UIManager.ShowConnectingMessage("正在連接到AI伺服器...");
        }
        else if (currentGameMode == GameMode.OnlineMultiplayerRoom)
        {
            // 連接到房間伺服器
            wsManager.ConnectToServer(ROOM_SERVER_URL);
            // UI顯示房間創建/加入界面
             if(UIManager != null) UIManager.ShowConnectingMessage("正在連接到房間伺服器...");
            // UIManager.SetRoomPanelActive(true); // 這個在 InitializeGameDefaults 後由 UIManager 自己控制
        }
        // 舊的 OnlineMultiplayer 模式的處理 (如果保留)
        // else if (currentGameMode == GameMode.OnlineMultiplayer)
        // {
        //     wsManager.ConnectToServer(ROOM_SERVER_URL); // 或者一個通用的P2P協調伺服器URL
        // }

        if (currentGameMode != GameMode.OfflineSinglePlayer) {
             // 線上模式的牌組和初始手牌通常由伺服器在 gameStart 或 roomUpdate 中提供
             // 此處不清空，等待伺服器訊息
        }

        if(UIManager != null) UIManager.UpdateUI(); // 初始UI更新
    }

    void InitializeOfflineGame()
    {
        playerDeck = GenerateRandomDeck(30); //
        opponentDeck = GenerateRandomDeck(30); //
        DrawCardLocal(true, 5); //
        DrawCardLocal(false, 5); //
        isPlayerTurn = true; // 單機模式玩家先手
        if (UIManager != null) UIManager.UpdateUI();
        Debug.Log($"Offline game initialized. PlayerDeck: {playerDeck.Count}, OpponentDeck: {opponentDeck.Count}");
    }


    List<Card> GenerateRandomDeck(int count)
    {
        // ... (此方法保持不變)
        List<Card> deck = new List<Card>();
        string[] cardNames = { "Fireball", "Ice Blast", "Thunder Strike", "Heal Wave", "Shadow Bolt", "Light Heal", "Flame Slash", "Frost Shield" }; //
        string[] effects = { "Deal", "Heal" }; //

        for (int i = 0; i < count; i++)
        {
            string name = cardNames[Random.Range(0, cardNames.Length)]; //
            string effect = effects[Random.Range(0, effects.Length)]; //
            int cost = Random.Range(1, 6); //
            int attack = effect == "Deal" ? Random.Range(1, 6) : 0; //
            int value = effect == "Heal" ? Random.Range(1, 6) : 0; //

            deck.Add(new Card
            {
                id = i.ToString(), //
                name = $"{name} #{i + 1}", // 稍微修改名稱以更好地區分
                cost = cost, //
                attack = attack, //
                value = value, //
                effect = $"{effect} {value} {(effect == "Deal" ? "damage" : "health")}", //
                // sprite = ... // 你需要從資源加載卡牌圖片
            });
        }
        return deck;
    }

    // 【修改】玩家結束回合
    public void EndTurn()
    {
        if (!isPlayerTurn && currentGameMode != GameMode.OnlineSinglePlayerAI && currentGameMode != GameMode.OnlineMultiplayerRoom) // 在線模式下，回合由伺服器控制是否可以結束
        {
            Debug.LogWarning("Not player's turn to end or waiting for server!");
            return;
        }

        Debug.Log("Player ends turn button pressed.");
        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            isPlayerTurn = false;
            // 以下是單機模式的AI回合處理
            opponentMaxMana = Mathf.Min(opponentMaxMana + 1, 10); //
            opponentMana = opponentMaxMana; //
            DrawCardLocal(false, 1); //
            if (UIManager != null) UIManager.UpdateUI(); // 更新UI顯示AI的資源
            StartCoroutine(aiManager.PlayAITurn()); //
        }
        else if (currentGameMode == GameMode.OnlineSinglePlayerAI || currentGameMode == GameMode.OnlineMultiplayerRoom)
        {
            // 向伺服器發送結束回合的請求
            wsManager.SendEndTurnRequest(RoomId); // RoomId 在 AI 模式下可能為 null 或不需要
            // 客戶端不應立即改變 isPlayerTurn，等待伺服器確認和狀態更新
            Debug.Log("Sent EndTurn request to server.");
        }
    }

    // 由 AIManager 在離線模式下調用
    public void EndAITurn() // 這個主要用於離線模式
    {
        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            Debug.Log("AI ends turn (offline).");
            isPlayerTurn = true;
            maxMana = Mathf.Min(maxMana + 1, 10); //
            playerMana = maxMana; //
            DrawCardLocal(true, 1); //
            if (UIManager != null) UIManager.UpdateUI();
        }
    }


    // 【新增】處理從伺服器收到的開始遊戲訊息 (主要用於 OnlineSinglePlayerAI)
    public void HandleGameStartFromServer(ServerGameState initialState)
    {
        Debug.Log("Handling GameStartFromServer");
        PlayerId = initialState.playerId;
        playerHealth = initialState.playerHealth;
        opponentHealth = initialState.aiHealth; // 在AI模式，對手是AI
        playerMana = initialState.playerMana;
        maxMana = initialState.playerMaxMana;
        opponentMana = initialState.aiMana; // 假設伺服器會發送AI的初始法力
        opponentMaxMana = initialState.aiMaxMana; // 假設伺服器會發送AI的初始最大法力
        isPlayerTurn = initialState.isPlayerTurn;

        playerHand = ConvertServerCardsToClientCards(initialState.playerHand);
        // opponentHand.Clear(); // AI手牌通常不直接顯示，但可以顯示數量
        // if (initialState.aiHandCount.HasValue) UIManager.UpdateOpponentHandCount(initialState.aiHandCount.Value);
        opponentServerHandCount = initialState.aiHandCount ?? 0; // 【新增】存儲AI手牌數量
        if(initialState.aiField != null) // 【新增】處理AI初始場地（如果有）
        {
            opponentField = ConvertServerCardsToClientCards(initialState.aiField);
        } else {
            opponentField.Clear();
        }

        // 清除"連接中"的訊息
        if (UIManager != null) {
            UIManager.HideConnectingMessage();
            UIManager.UpdateUI();
            if(initialState.gameOver) CheckGameOver(); // 如果遊戲開始時就已結束 (不太可能，但做個檢查)
        }
    }

    // 【新增】處理從伺服器收到的遊戲狀態更新 (通用於兩種線上模式)
    public void HandleGameStateUpdateFromServer(ServerGameState updatedState)
    {
        Debug.Log("Handling GameStateUpdateFromServer. Player turn: " + updatedState.isPlayerTurn);
        // PlayerId 通常在連接時就設定好了，或者在 gameStart 時
        if(!string.IsNullOrEmpty(updatedState.playerId) && PlayerId != updatedState.playerId && currentGameMode == GameMode.OnlineMultiplayerRoom) {
            // 這是對手的狀態更新，或者是給特定玩家的狀態，需要區分
            // 在房間模式，伺服器應該發送雙方的狀態，或者客戶端根據 currentPlayerId 判斷
        }

        // 更新自己的狀態 (假設 updatedState 是針對當前客戶端的視角)
        playerHealth = updatedState.playerHealth;
        playerMana = updatedState.playerMana;
        maxMana = updatedState.playerMaxMana;
        if (updatedState.playerHand != null) { // 伺服器可能只更新變動的部分，或者總是發送完整手牌
            playerHand = ConvertServerCardsToClientCards(updatedState.playerHand);
        }

        // 更新對手的狀態
        if (currentGameMode == GameMode.OnlineSinglePlayerAI) {
            opponentHealth = updatedState.aiHealth;
            opponentMana = updatedState.aiMana; // 假設可見
            opponentMaxMana = updatedState.aiMaxMana; // 假設可見
            // if (updatedState.aiHandCount.HasValue) UIManager.UpdateOpponentHandCount(updatedState.aiHandCount.Value);
            opponentServerHandCount = updatedState.aiHandCount ?? 0; // 【新增】更新AI手牌數量
            if (updatedState.aiField != null) // 【新增】更新AI場地
            {
                opponentField = ConvertServerCardsToClientCards(updatedState.aiField);
            } else {
                opponentField.Clear();
            }
            
        } else if (currentGameMode == GameMode.OnlineMultiplayerRoom) {
            // 在房間模式，updatedState.opponentState 應該包含對手的公開資訊
            if (updatedState.opponentState != null) {
                opponentHealth = updatedState.opponentState.health;
                opponentMana = updatedState.opponentState.mana;
                opponentMaxMana = updatedState.opponentState.maxMana;
                // if (updatedState.opponentState.handCount.HasValue) UIManager.UpdateOpponentHandCount(updatedState.opponentState.handCount.Value);
                // OpponentPlayerId = updatedState.opponentState.playerId; // 如果有
            }
        }

        isPlayerTurn = updatedState.isPlayerTurn;

        if (UIManager != null) {
            UIManager.HideConnectingMessage(); // 確保連接訊息被清除
            UIManager.SetRoomPanelActive(currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom && !updatedState.gameStarted);
            UIManager.UpdateUI();
        }

        if (updatedState.gameOver) {
            if (UIManager != null) {
                UIManager.ShowGameOver(updatedState.winner == PlayerId || (currentGameMode == GameMode.OnlineSinglePlayerAI && updatedState.winner == "Player") ? "玩家勝利!" : "對手勝利!");
            }
            Time.timeScale = 0; // 暫停遊戲
        } else {
            Time.timeScale = 1; // 確保遊戲在非結束時正常運行
        }
    }
    // 【新增】處理玩家出牌的確認 (來自 CardPlayService)
    public void HandlePlayerCardPlayConfirmed(string playedCardId)
    {
        Debug.Log($"GameManager: Player card play confirmed for card ID: {playedCardId}. Triggering animation.");
        if (UIManager == null)
        {
            Debug.LogError("GameManager: UIManager is null, cannot trigger animation.");
            return;
        }

        GameObject cardObjectToAnimate = UIManager.FindPlayerHandCardObjectById(playedCardId);

        if (cardObjectToAnimate != null)
        {
            CardUI cardUI = cardObjectToAnimate.GetComponent<CardUI>();
            if (cardUI != null && cardUI.card != null)
            {
                Card cardData = cardUI.card;
                Debug.Log($"GameManager: Found card '{cardData.name}' in hand to animate.");

                // 從 UIManager 的手牌追蹤列表中移除，因為它要開始動畫了
                // UIManager.PlayCardWithAnimation 內部會做這個移除
                // UIManager.playerHandCardObjects.Remove(cardObjectToAnimate); // 這一行 PlayCardWithAnimation 會處理

                // 決定目標面板 (法術牌可能飛向場地中央或對手，單位牌飛向己方場地)
                // 為了演示，我們先假設所有牌都飛向玩家場地，然後消失 (法術牌的行為)
                Transform targetPanel = UIManager.playerFieldPanel; // 或者一個專用的 "spell cast target"

                UIManager.PlayCardWithAnimation(cardData, true, cardObjectToAnimate, UIManager.playerHandPanel, targetPanel);
            }
            else
            {
                Debug.LogError($"GameManager: CardUI or Card data missing on GameObject for card ID: {playedCardId}");
            }
        }
        else
        {
            Debug.LogWarning($"GameManager: Could not find card GameObject in player's hand UI for ID: {playedCardId} to animate. It might have been already updated by a gameStateUpdate.");
            // 如果 gameStateUpdate 比 playerAction 先到，並且已經從手牌移除了卡牌UI，這裡就找不到物件了。
            // 這種情況下，可以考慮播放一個通用的“施法動畫”而不是特定卡牌飛出的動畫。
            // 或者，確保 playerAction 訊息優先處理或在 gameStateUpdate 中包含足夠信息來觸發動畫。
        }
        // 玩家的法力值、實際手牌數據列表 (playerHand) 的更新，應該依賴後續的 gameStateUpdate
        // 這裡的動畫主要是視覺表現。
    }

// ...
    // 【新增】處理從伺服器收到的房間狀態更新 (OnlineMultiplayerRoom)
    public void HandleRoomUpdateFromServer(ServerRoomState roomState) {
        Debug.Log($"Handling RoomUpdateFromServer. Room: {roomState.roomId}, GameStarted: {roomState.gameStarted}, IsInRoom: {IsInRoom}");
        RoomId = roomState.roomId;
        IsInRoom = true; // 假設一旦收到 RoomUpdate，就表示已在房間內或剛加入

        // 更新玩家列表等UI (UIManager 中實現)
        // UIManager.UpdatePlayerList(roomState.players);

        if (roomState.gameStarted) {
            UIManager.SetRoomPanelActive(false); // 遊戲開始，隱藏房間面板
            // 遊戲開始的狀態由 roomState.self 和 roomState.opponent 提供
            playerHealth = roomState.self.health;
            playerMana = roomState.self.mana;
            maxMana = roomState.self.maxMana;
            playerHand = ConvertServerCardsToClientCards(roomState.self.hand);

            if (roomState.opponent != null) {
                opponentHealth = roomState.opponent.health;
                opponentMana = roomState.opponent.mana;
                opponentMaxMana = roomState.opponent.maxMana;
                // UIManager.UpdateOpponentHandCount(roomState.opponent.handCount);
                // OpponentPlayerId = roomState.opponent.playerId;
            } else {
                // 可能對手剛離開，或者在等待對手
                opponentHealth = 0; // 或一個預設值
                opponentMana = 0;
                opponentMaxMana = 0;
            }
            isPlayerTurn = (roomState.currentPlayerId == PlayerId);

        } else {
            // 遊戲未開始，仍在房間等待界面
            UIManager.SetRoomPanelActive(true);
            UIManager.UpdateRoomStatus($"房間ID: {RoomId}\n{(roomState.players != null ? string.Join(", ", roomState.players.Select(p=>p.Value)) : "等待玩家...")}\n{roomState.message}");
        }

        if (UIManager != null) {
            UIManager.HideConnectingMessage();
            UIManager.UpdateUI();
        }

        if (roomState.gameOver) {
            UIManager.ShowGameOver(roomState.winnerId == PlayerId ? "玩家勝利!" : "對手勝利!");
            Time.timeScale = 0;
        } else {
            Time.timeScale = 1;
        }
    }

    // 【新增】處理對手出牌的通知 (來自伺服器)
    public void HandleOpponentPlayCard(ServerCard cardPlayed, string opponentPlayerName) {
        Debug.Log($"{opponentPlayerName} (Opponent) played card: {cardPlayed.name}");

        // 模擬對手出牌動畫，需要對手手牌面板的引用和卡牌物件
        // 1. 從對手手牌數據中移除 (如果客戶端也維護對手手牌數據的話，通常只維護數量)
        // 2. UIManager 播放動畫，將卡牌移動到場地
        Card clientCard = ConvertServerCardToClientCard(cardPlayed);
        if (clientCard != null) {
            // 這一步比較複雜，因為我們沒有對手手牌的實際 GameObject
            // 理想情況下，UIManager 會收到這個 clientCard，然後在 opponentHandPanel 附近生成一個卡牌背面，
            // 播放它翻轉並移動到 opponentFieldPanel 的動畫，然後顯示卡牌內容。
            // 簡單處理：直接在對手場地上顯示卡牌，或者依賴 GameStateUpdateFromServer 的結果。

            // 這裡假設 CardPlayService 或 UIManager 有方法處理“遠程卡牌播放”
            // CardPlayService.DisplayOpponentCardPlay(clientCard);
            opponentField.Add(clientCard); // 簡單地加入數據，UI更新時會顯示
        }
        // 實際的血量、法力變化等，應該由接下來的 GameStateUpdateFromServer 處理
        if (UIManager != null) UIManager.UpdateUI(); // 更新UI以顯示對手場地上的牌
    }

    // 【新增】處理AI出牌的通知 (來自伺服器)
    public void HandleAIPlayCard(ServerCard serverCardPlayed) {
        Debug.Log($"GameManager: Handling online AI card play: {serverCardPlayed.name}");
        Card clientCard = ConvertServerCardToClientCard(serverCardPlayed);

        if (clientCard != null && UIManager != null) {
            // 創建臨時卡牌物件到動畫層
            GameObject tempAICardObject = Instantiate(UIManager.cardPrefab, UIManager.animationContainer);
            CardUI aiCardUI = tempAICardObject.GetComponent<CardUI>();
            RectTransform tempAIRectTransform = tempAICardObject.GetComponent<RectTransform>(); // 獲取 RectTransform

            if (aiCardUI != null && tempAIRectTransform != null) { // 確保 RectTransform 也存在
                // 【新增】設置臨時卡牌的初始位置到對手手牌區的中心或一個起始點
                // 我們需要將 opponentHandPanel 的世界座標轉換為 animationContainer 下的局部座標
                if (UIManager.opponentHandPanel != null && UIManager.animationContainer != null)
                {
                    // 計算 opponentHandPanel 中心點在 animationContainer 中的局部位置
                    Vector3 opponentHandPanelCenterWorld = UIManager.opponentHandPanel.position; // 通常是 Panel 的中心點
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        UIManager.animationContainer as RectTransform, // target canvas rect transform
                        RectTransformUtility.WorldToScreenPoint(null, opponentHandPanelCenterWorld), // screen point of the target
                        null, // camera (null for ScreenSpaceOverlay)
                        out localPoint
                    );
                    tempAIRectTransform.anchoredPosition = localPoint; // 設置其在動畫層的初始位置
                    Debug.Log($"GameManager.HandleAIPlayCard: Set temp AI card initial anchoredPosition to: {localPoint} (relative to animationContainer)");
                }
                else
                {
                    Debug.LogWarning("GameManager.HandleAIPlayCard: OpponentHandPanel or AnimationContainer is null, cannot set initial position for AI card animation.");
                    tempAIRectTransform.localPosition = Vector3.zero; // 默認到動畫層中心
                }

                aiCardUI.Initialize(clientCard, false, false); // isPlayer=false, isField=false
                if (aiCardUI.cardDetailsContainer != null) aiCardUI.cardDetailsContainer.SetActive(true);

                Debug.Log($"GameManager: Online AI playing '{clientCard.name}'. Triggering animation.");
                
                // 動畫的 sourcePanel 參數實際上在 CardAnimationManager 中沒有被用來決定起始位置，
                // 因為卡牌已經在 animationContainer 中了。重要的是 cardObject 的當前位置。
                // targetForAIAnimation 仍然是 opponentFieldPanel。
                Transform sourceForAIAnimation = UIManager.opponentHandPanel; // 這個參數可以保留，但實際起點是上面設置的
                Transform targetForAIAnimation = UIManager.opponentFieldPanel;

                if (targetForAIAnimation != null) {
                    Debug.Log($"GameManager.HandleAIPlayCard: Target for AI animation is '{targetForAIAnimation.name}' (InstanceID: {targetForAIAnimation.GetInstanceID()})");
                } else {
                    Debug.LogError("GameManager.HandleAIPlayCard: UIManager.opponentFieldPanel IS NULL!");
                    if(tempAICardObject != null) Destroy(tempAICardObject);
                    return;
                }

                UIManager.PlayCardWithAnimation(clientCard, false, tempAICardObject, sourceForAIAnimation, targetForAIAnimation);
            } else {
                Debug.LogError("GameManager: Failed to get CardUI or RectTransform on instantiated tempAICardObject for AI card.");
                if(tempAICardObject != null) Destroy(tempAICardObject);
            }
        } else {
            Debug.LogError($"GameManager: Failed to convert ServerCard '{serverCardPlayed.name}' or UIManager is null.");
        }
    }


    // 【本地】抽牌邏輯 (主要用於離線模式，或線上模式的視覺表現)
    void DrawCardLocal(bool isPlayer, int count)
    {
        List<Card> deck = isPlayer ? playerDeck : opponentDeck;
        List<Card> hand = isPlayer ? playerHand : opponentHand;

        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                Debug.LogWarning($"{(isPlayer ? "Player" : "Opponent")} deck is empty! Cannot draw more cards.");
                CheckGameOver(); // 牌庫耗盡也可能導致遊戲結束
                return;
            }

            Card card = deck[Random.Range(0, deck.Count)]; //
            deck.Remove(card); //
            hand.Add(card); //
            // Debug.Log($"Drew card {card.name} to {(isPlayer ? "player" : "opponent")}Hand. Hand count: {hand.Count}");
        }
    }

    public void CheckGameOver()
    {
        // 在線上模式，遊戲結束主要由伺服器判斷和通知
        // 這個方法在離線模式下仍然有用
        if (currentGameMode == GameMode.OfflineSinglePlayer) {
            if (playerHealth <= 0)
            {
                UIManager.ShowGameOver("對手勝利!"); //
                Time.timeScale = 0; //
            }
            else if (opponentHealth <= 0)
            {
                UIManager.ShowGameOver("玩家勝利!"); //
                Time.timeScale = 0; //
            }
        }
    }

    // 【輔助方法】將伺服器卡牌列表轉換為客戶端卡牌列表
    public List<Card> ConvertServerCardsToClientCards(List<ServerCard> serverCards) {
        if (serverCards == null) return new List<Card>();
        List<Card> clientCards = new List<Card>();
        foreach (var sc in serverCards) {
            clientCards.Add(ConvertServerCardToClientCard(sc));
        }
        return clientCards;
    }

    // 【輔助方法】將單個伺服器卡牌轉換為客戶端卡牌
    public Card ConvertServerCardToClientCard(ServerCard serverCard) {
        if (serverCard == null) return null;
        // 你需要一個機制來從卡牌數據 (例如 serverCard.name 或 serverCard.id) 找到對應的 Sprite
        // 這裡僅作轉換，Sprite 需要額外處理
        Sprite cardSprite = UIManager.GetCardSpriteByName(serverCard.name); // 假設 UIManager 有此方法

        return new Card {
            id = serverCard.id,//string.IsNullOrEmpty(serverCard.id) ? Random.Range(1000,9999) : Animator.StringToHash(serverCard.id), // 客戶端 ID 可以重新生成或使用伺服器 ID 的 Hash
            name = serverCard.name,
            cost = serverCard.cost,
            attack = serverCard.attack,
            value = serverCard.value,
            effect = serverCard.effect,
            cardType = serverCard.cardType, // 【新增】
            sprite = cardSprite // 【重要】你需要實現獲取 Sprite 的邏輯
        };
    }

    // 【新增】玩家請求創建房間
    public void RequestCreateRoom(string playerName) {
        if (currentGameMode == GameMode.OnlineMultiplayerRoom && wsManager != null && wsManager.IsConnected()) {
            wsManager.SendCreateRoomRequest(playerName);
        } else {
            Debug.LogError("Cannot create room: Not in room mode or not connected.");
        }
    }

    // 【新增】玩家請求加入房間
    public void RequestJoinRoom(string targetRoomId, string playerName) {
        if (string.IsNullOrEmpty(targetRoomId)) {
            UIManager.UpdateRoomStatus("錯誤：房間ID不能為空！");
            return;
        }
        if (currentGameMode == GameMode.OnlineMultiplayerRoom && wsManager != null && wsManager.IsConnected()) {
            wsManager.SendJoinRoomRequest(targetRoomId, playerName);
        } else {
            Debug.LogError("Cannot join room: Not in room mode or not connected.");
        }
    }
     // 【新增】玩家請求離開房間
    public void RequestLeaveRoom() {
        if (IsInRoom && wsManager != null && wsManager.IsConnected()) {
            wsManager.SendLeaveRoomRequest(RoomId);
            // 離開房間後的狀態清理，例如重置 IsInRoom 等，可以在收到伺服器 leftRoom 確認後進行
        }
    }


    // 【新增】當 WebSocket 連接成功時由 WebSocketManager 調用
    public void OnWebSocketConnected() {
        Debug.Log("GameManager: WebSocket Connected.");
        if (UIManager != null) {
            UIManager.HideConnectingMessage();
            if (currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom) {
                UIManager.SetRoomPanelActive(true); // 連接成功後，如果是房間模式且還沒在房間裡，顯示房間面板
                UIManager.UpdateRoomStatus("已連接到房間伺服器。\n請創建或加入房間。");
            }
        }
    }

    // 【新增】當 WebSocket 連接失敗或斷開時由 WebSocketManager 調用
    public void OnWebSocketDisconnected(string reason) {
        Debug.LogWarning($"GameManager: WebSocket Disconnected. Reason: {reason}");
        if (UIManager != null) {
            if (currentGameMode == GameMode.OnlineSinglePlayerAI || currentGameMode == GameMode.OnlineMultiplayerRoom) {
                UIManager.ShowConnectingMessage($"連接已斷開: {reason}\n請檢查網絡並嘗試返回主選單重連。");
                // 可以考慮彈出一個返回主選單的按鈕
            }
        }
        // 可能需要重置一些遊戲狀態
        isPlayerTurn = false; // 避免玩家在斷線後還能操作
        IsInRoom = false;

    }
}

// 【新增】用於接收伺服器遊戲狀態的輔助類 (需要與後端 GameMessage 中的 data 結構對應)
// 這些結構需要根據你後端實際發送的 JSON 結構來定義
[System.Serializable]
public class ServerGameState {
    public string playerId; // 當前玩家的ID (在AI模式中) 或房間中某個玩家的ID
    public int playerHealth;
    public int playerMana;
    public int playerMaxMana;
    public List<ServerCard> playerHand;

    // AI 模式特有
    public int aiHealth;
    public int aiMana;
    public int aiMaxMana;
    public int? aiHandCount; // AI手牌數量 (可選)
    public List<ServerCard> aiField; // 【新增】接收AI場地牌
    // 通用
    public bool isPlayerTurn;
    public bool gameOver;
    public string winner; // "Player", "AI", 或 playerId

    // 房間模式下，這個頂層結構可能代表 "self" 的狀態，然後有一個嵌套的 "opponentState"
    public ServerPlayerState opponentState; // 用於房間模式中對手的公開狀態
    public bool gameStarted; // 房間模式中，標識遊戲是否已在房間內開始
}

[System.Serializable]
public class ServerPlayerState { // 用於房間模式中表示一個玩家的公開狀態
    public string playerId;
    public int health;
    public int mana;
    public int maxMana;
    public int handCount; // 對手手牌數量
    public List<ServerCard> hand; // 通常只有自己的手牌才有完整列表
}


[System.Serializable]
public class ServerCard
{ // 與後端 ServerCard 對應
    public string id;
    public string name;
    public int cost;
    public int attack;
    public int value;
    public string effect;
    public string cardType; // 【新增】
}

[System.Serializable]
public class ServerRoomState { // 用於接收房間更新
    public string roomId;
    public Dictionary<string, string> players; // <playerId, playerName>
    public bool gameStarted;
    public bool gameOver;
    public string winnerId;
    public string currentPlayerId;
    public string message; // 伺服器發來的額外訊息

    public ServerPlayerState self; // 玩家自己的詳細狀態
    public ServerPlayerState opponent; // 對手的公開狀態
}