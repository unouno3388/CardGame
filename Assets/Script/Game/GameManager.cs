// GameManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using System.Threading.Tasks; // 保持 Task 用於 WebSocketManager 的 CloseConnection

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static GameMode initialGameMode = GameMode.OfflineSinglePlayer;
    public static string GameSceneName = "SampleScene"; // 遊戲場景名稱

    // --- 依賴注入的服務 ---
    public IGameState CurrentState { get; private set; }
    public IDataConverter DataConverter { get; private set; }
    public IGameOverHandler GameOverHandler { get; private set; }
    public ITurnProcessor TurnProcessor { get; private set; }
    public IDeckOperator DeckOperator { get; private set; }
    public IRoomActionHandler RoomActionHandler { get; private set; }

    // --- Unity 組件引用 ---
    public UIManager UIManager { get; private set; }
    private AIManager aiManager; // 離線AI管理器
    public WebSocketManager WebSocketManager { get; private set; } // 修改為公有屬性
    public CardPlayService CardPlayService { get; private set; } // 修改為公有屬性
    public CardAnimationManager CardAnimationManager { get; private set; } // 新增

    // --- 其他組件 ---
    public MenuManager menuManager; // 嘗試在 Awake 查找

    public enum GameMode
    {
        OfflineSinglePlayer,
        OnlineSinglePlayerAI,
        OnlineMultiplayerRoom
        // OnlineMultiplayer, // 如果不再使用可以移除
    }
    // 當前遊戲模式，通過 CurrentState 訪問
    public GameMode CurrentGameMode => CurrentState != null ? CurrentState.CurrentGameMode : initialGameMode;
    // 玩家ID，通過 CurrentState 訪問
    public string PlayerId => CurrentState?.PlayerId;
    // 是否在房間，通過 CurrentState 訪問
    public bool IsInRoom => CurrentState != null && CurrentState.IsInRoom;


    private bool uiAutoUpdate = true; // 這個標誌似乎與 UIManager 的內部邏輯有關
    public bool UIAutoUpdate { get => uiAutoUpdate; set => uiAutoUpdate = value; }

    private const string AI_SERVER_URL = "ws://localhost:8080/game/ai";
    private const string ROOM_SERVER_URL = "ws://localhost:8080/game/room";


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.LogWarning($"GameManager Awake: Instance {this.GetInstanceID()} is now THE singleton instance. TurnProcessor will be initialized on this instance.");
        }
        else
        {
            Debug.LogWarning($"GameManager Awake: Instance {this.GetInstanceID()} is a DUPLICATE and will be destroyed. Its TurnProcessor will remain null.");
            Destroy(gameObject);
            return; // 非常重要，確保後續代碼不執行
        }
        // 初始化服務
        CurrentState = new GameState();
        DataConverter = new DataConverter();
        GameOverHandler = new GameOverHandler();
        TurnProcessor = new TurnProcessor();
        DeckOperator = new DeckOperator();
        RoomActionHandler = new RoomActionHandler();
        Debug.Log("GameManager: Services initialized.");
        Debug.Log($"TurnProcessor: {TurnProcessor !=null}.");
        // 獲取 Unity 組件
        UIManager = FindObjectOfType<UIManager>(); //
        aiManager = GetComponent<AIManager>(); //
        WebSocketManager = GetComponent<WebSocketManager>(); //
        CardPlayService = gameObject.AddComponent<CardPlayService>(); //
        CardAnimationManager = FindObjectOfType<CardAnimationManager>();


        if (UIManager == null) Debug.LogError("GameManager: UIManager not found!");
        if (aiManager == null && initialGameMode == GameMode.OfflineSinglePlayer) Debug.LogWarning("GameManager: AIManager not found (needed for offline AI).");
        if (WebSocketManager == null && (initialGameMode == GameMode.OnlineSinglePlayerAI || initialGameMode == GameMode.OnlineMultiplayerRoom)) Debug.LogError("GameManager: WebSocketManager not found!");
        if (CardPlayService == null) Debug.LogError("GameManager: CardPlayService could not be added/found!");
        if (CardAnimationManager == null) Debug.LogWarning("GameManager: CardAnimationManager not found. Animations might not work.");


        // 嘗試獲取 MenuManager
        if (menuManager == null)
        {
            menuManager = FindObjectOfType<MenuManager>(); //
            // 不需要 LogError，因為 MenuManager 可能只在主選單場景
        }

        // 初始化服務依賴 (注意順序和可用性)
        // GameState 不需要其他服務的依賴進行初始化
        // DataConverter 也不需要
        GameOverHandler.InitializeDependencies(this, menuManager, UIManager, CardAnimationManager, CurrentState);
        TurnProcessor.InitializeDependencies(this, CurrentState, aiManager, WebSocketManager, UIManager);
        DeckOperator.InitializeDependencies(CurrentState, UIManager, GameOverHandler);
        RoomActionHandler.InitializeDependencies(CurrentState, WebSocketManager, UIManager, this);


        // 註冊場景載入回調
        if (SceneLoadingManager.Instance != null) //
        {
            SceneLoadingManager.Instance.RegisterSceneLoadCallback(GameSceneName, OnGameSceneLoaded); //
        }
        else
        {
            Debug.LogError("GameManager: SceneLoadingManager.Instance is null. Cannot register scene load callback.");
        }
    }

    void Start()
    {
        // PlayerId 的生成/分配可以在連接成功後或由 RoomActionHandler 處理
        // CurrentState.PlayerId = System.Guid.NewGuid().ToString().Substring(0, 8); // 移至更合適的地方

        StartGame(initialGameMode);

        if (menuManager != null && menuManager.gameOverManager != null)
        {
            menuManager.gameOverManager.HideGameOverPanel(); //
        }
        Time.timeScale = 1; //
    }
    /// <summary>
    /// 設置 WebSocketManager 的消息註冊
    /// </summary>
    void SetupMessageHandlers()
    {
        if (WebSocketManager == null) return;

        WebSocketManager.RegisterMessageHandler("gameStart", (data) => //
        {
            ServerGameState gameStartState = JsonConvert.DeserializeObject<ServerGameState>(data.ToString()); //
            HandleGameStartFromServer(gameStartState);
        });
        WebSocketManager.RegisterMessageHandler("gameStateUpdate", (data) => //
        {
            ServerGameState gameState = JsonConvert.DeserializeObject<ServerGameState>(data.ToString()); //
            HandleGameStateUpdateFromServer(gameState);
        });
        WebSocketManager.RegisterMessageHandler("roomUpdate", (data) => //
        {
            ServerRoomState roomState = JsonConvert.DeserializeObject<ServerRoomState>(data.ToString()); //
            HandleRoomUpdateFromServer(roomState);
        });
        WebSocketManager.RegisterMessageHandler("aiAction", (data) => //
        {
            var aiActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (aiActionData.TryGetValue("actionType", out object aiActionType) && aiActionType.ToString() == "playCard") //
            {
                if (aiActionData.TryGetValue("card", out object cardObj)) //
                {
                    ServerCard aiCard = JsonConvert.DeserializeObject<ServerCard>(cardObj.ToString()); //
                    HandleAIPlayCard(aiCard);
                }
            }
        });
        WebSocketManager.RegisterMessageHandler("opponentPlayCard", (data) => //
        {
            // 假設 data 是一個 JSON 字串，代表包含 "card" 和 "playerName" 的物件
            var eventData = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); // 有點繞，但如果data就是GameMessage.data的內容
            // 或者，如果 data 就是 GameMessage 本身（修改委派）
            // ServerCard playedCard = JsonConvert.DeserializeObject<ServerCard>(eventData.data.ToString()); // 如果 card 在 data.data 裡
            // string playerName = eventData.playerId; // 如果 playerName 是 GameMessage.playerId

            // 假設 data 包含 card (ServerCard) 和 playerName
            var opponentPlayData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (opponentPlayData.TryGetValue("card", out object cardData) && //
                opponentPlayData.TryGetValue("playerName", out object playerNameObj)) //
            {
                ServerCard playedCard = JsonConvert.DeserializeObject<ServerCard>(cardData.ToString()); //
                HandleOpponentPlayCard(playedCard, playerNameObj.ToString());
            }
        });

        // 房間相關訊息由 RoomActionHandler 處理
        // WebSocketManager.RegisterMessageHandler("roomCreated", (data) => RoomActionHandler.HandleRoomCreatedResponse(JsonConvert.DeserializeObject<GameMessage>(data.ToString())));
        // WebSocketManager.RegisterMessageHandler("roomJoined", (data) => RoomActionHandler.HandleRoomJoinedResponse(JsonConvert.DeserializeObject<GameMessage>(data.ToString())));
        // WebSocketManager.RegisterMessageHandler("leftRoom", (data) => RoomActionHandler.HandleLeftRoomResponse(JsonConvert.DeserializeObject<GameMessage>(data.ToString())));
        // WebSocketManager.RegisterMessageHandler("error", (data) => RoomActionHandler.HandleErrorResponse(JsonConvert.DeserializeObject<GameMessage>(data.ToString())));
        // 注意：上面直接將 data.ToString() 反序列化為 GameMessage 可能不對，因為 data 參數本身就是 GameMessage.data 的內容。
        // WebSocketManager 的 HandleMessageFromServer 會傳遞 baseMessage.data。
        // 所以，處理器應該直接使用 GameMessage message 參數，而不是 object data
        // 這需要修改 WebSocketManager 中的 MessageHandlerDelegate 和 RegisterMessageHandler 的調用方式。
        // 暫時保持原樣，但這是一個潛在問題點。
        // 更穩妥的方式是讓 WebSocketManager 傳遞整個 GameMessage:
        // public delegate void MessageHandlerDelegate(GameMessage message);
        // wsManager.RegisterMessageHandler("roomCreated", RoomActionHandler.HandleRoomCreatedResponse);

         // 假設 WebSocketManager 傳遞的是 GameMessage.data 部分
        WebSocketManager.RegisterMessageHandler("roomCreated", (data) => { //
            // 我們需要從 data (object) 重建成 GameMessage 才能獲取 roomId, playerId 等頂層欄位
            // 這是原始 GameManager 中的權宜之計。理想情況下，WebSocketManager 應該傳遞整個 GameMessage。
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleRoomCreatedResponse(gameMessage); //
        });
         WebSocketManager.RegisterMessageHandler("roomJoined", (data) => { //
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleRoomJoinedResponse(gameMessage); //
        });
        WebSocketManager.RegisterMessageHandler("leftRoom", (data) => { //
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleLeftRoomResponse(gameMessage); //
        });
        WebSocketManager.RegisterMessageHandler("error", (data) => { //
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleErrorResponse(gameMessage); //
        });


        WebSocketManager.RegisterMessageHandler("playerAction", (data) => //
        {
            var playerActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (playerActionData.TryGetValue("action", out object playerActionType) && playerActionType.ToString() == "playCard" && //
                playerActionData.TryGetValue("success", out object successObj) && (bool)successObj == true) //
            {
                if (playerActionData.TryGetValue("cardId", out object cardIdObj)) //
                {
                    string playedCardId = cardIdObj.ToString(); //
                    HandlePlayerCardPlayConfirmed(playedCardId);
                }
            }
        });
    }

    void OnGameSceneLoaded() // 原 OnSceneLoaded
    {
        Time.timeScale = 1; //

        // 重新獲取場景中可能改變的組件引用
        UIManager = FindObjectOfType<UIManager>(); //
        CardAnimationManager = FindObjectOfType<CardAnimationManager>();
        if (menuManager == null) menuManager = FindObjectOfType<MenuManager>(); //

        // 確保服務依賴更新
        GameOverHandler.InitializeDependencies(this, menuManager, UIManager, CardAnimationManager, CurrentState);
        TurnProcessor.InitializeDependencies(this, CurrentState, aiManager, WebSocketManager, UIManager);
        DeckOperator.InitializeDependencies(CurrentState, UIManager, GameOverHandler);
        RoomActionHandler.InitializeDependencies(CurrentState, WebSocketManager, UIManager, this);
        // CardPlayService 可能也需要重新獲取 UIManager
        if (CardPlayService != null && UIManager != null) {
            // CardPlayService.Start() 應能處理 UIManager 的獲取，或提供一個 Reinitialize 方法
        }


        SetupMessageHandlers(); //

        if (UIManager != null && UIManager.gameOverText != null && UIManager.gameOverText.gameObject.activeSelf) //
        {
            UIManager.gameOverText.gameObject.SetActive(false); //
        }
        else if (menuManager != null && menuManager.gameOverManager != null && menuManager.gameOverManager.gameOverPanel != null && menuManager.gameOverManager.gameOverPanel.activeSelf) //
        {
            menuManager.gameOverManager.HideGameOverPanel(); //
        }

        GameOverHandler.ResetGameOverState(); // 重置遊戲結束狀態
        StartGame(initialGameMode); //
    }


    public void StartGame(GameMode mode)
    {
        CurrentState.ResetState(mode); //
        GameOverHandler.ResetGameOverState(); // 確保遊戲開始時結束狀態被重置

        Debug.Log($"GameManager: Starting game with mode: {CurrentState.CurrentGameMode}");

        if (UIManager != null)
        {
            UIManager.SetRoomPanelActive(CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom && !CurrentState.IsInRoom); //
            if (UIManager.gameOverText != null) UIManager.gameOverText.gameObject.SetActive(false); //
            UIManager.HideConnectingMessage(); //
        }

        if (CurrentState.CurrentGameMode == GameMode.OfflineSinglePlayer)
        {
            InitializeOfflineGame();
        }
        else if (CurrentState.CurrentGameMode == GameMode.OnlineSinglePlayerAI)
        {
            if (WebSocketManager != null) WebSocketManager.ConnectToServer(AI_SERVER_URL); //
            if (UIManager != null) UIManager.ShowConnectingMessage("正在連接到AI伺服器..."); //
        }
        else if (CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom)
        {
            if (WebSocketManager != null) WebSocketManager.ConnectToServer(ROOM_SERVER_URL); //
            if (UIManager != null) UIManager.ShowConnectingMessage("正在連接到房間伺服器..."); //
        }

        if (UIManager != null) UIManager.UpdateUI(); //
    }

    void InitializeOfflineGame()
    {
        CurrentState.PlayerDeck.AddRange(DeckOperator.GenerateRandomDeck(30)); //
        CurrentState.OpponentDeck.AddRange(DeckOperator.GenerateRandomDeck(30)); //
        DeckOperator.DrawCardLocal(true, 5); //
        DeckOperator.DrawCardLocal(false, 5); //
        CurrentState.IsPlayerTurn = true; //
        if (UIManager != null) UIManager.UpdateUI(); //
        Debug.Log($"GameManager: Offline game initialized. PlayerDeck: {CurrentState.PlayerDeck.Count}, OpponentDeck: {CurrentState.OpponentDeck.Count}");
    }

    // EndTurn 由 TurnProcessor 處理，GameManager 中的同名方法現在調用它
    public void EndTurn()
    {
        //Debug.LogWarning($"GameManager EndTurn: Called on instance {this.GetInstanceID()}. GameManager.Instance is {GameManager.Instance.GetInstanceID()}. TurnProcessor on THIS instance is null? {(TurnProcessor == null)}. TurnProcessor on GameManager.Instance is null? {(GameManager.Instance.TurnProcessor == null)}");

        if (TurnProcessor == null) // 這是針對 this.TurnProcessor 的檢查
        {
            Debug.LogError($"GameManager.EndTurn() on instance {this.GetInstanceID()}: this.TurnProcessor is NULL!");
            // 如果是按鈕調用，它應該是通過 GameManager.Instance.EndTurn() 調用的
            // 所以我們更關心 GameManager.Instance.TurnProcessor
            if (GameManager.Instance != null && GameManager.Instance.TurnProcessor == null)
            {
                Debug.LogError($"GameManager.EndTurn(): GameManager.Instance.TurnProcessor is ALSO NULL! This is the problem. Check Awake() on instance {GameManager.Instance.GetInstanceID()}.");
            }
            return;
        }
        TurnProcessor.EndPlayerTurn();
    }

    // EndAITurn 由 TurnProcessor 處理 (主要供 AIManager 調用)
    public void EndAITurn() //
    {
        TurnProcessor.EndAITurnOffline();
    }


    public void HandleGameStartFromServer(ServerGameState initialState) //
    {
        Debug.Log("GameManager: Handling GameStartFromServer");
        CurrentState.UpdateFromGameStartServer(initialState, DataConverter, UIManager); //

        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); //
            UIManager.UpdateUI(); //
            // 遊戲開始時的結束檢查現在由 CurrentState.UpdateFromGameStartServer 間接觸發 GameOverHandler
            if (initialState.gameOver) GameOverHandler.ProcessGameOverUpdate(initialState, CurrentState.PlayerId); //
        }
    }

    public void HandleGameStateUpdateFromServer(ServerGameState updatedState) //
    {
        Debug.Log($"GameManager: Handling GameStateUpdateFromServer. Player turn: {updatedState.isPlayerTurn}, GameOver: {updatedState.gameOver}");

        CurrentState.UpdateFromServer(updatedState, DataConverter, UIManager); //

        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); //
            // 在房間模式下，如果遊戲尚未開始且不在房間，顯示房間面板
            UIManager.SetRoomPanelActive(CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom && !CurrentState.IsInRoom && !updatedState.gameStarted); //
            UIManager.UpdateUI(); //
        }

        if (updatedState.gameOver)
        {
            GameOverHandler.ProcessGameOverUpdate(updatedState, CurrentState.PlayerId);
        }
        else if (GameOverHandler.IsGameOverSequenceRunning) // 如果之前是結束狀態，現在不是了
        {
             Debug.Log("GameManager: GameStateUpdate indicates game is NOT over, resetting GameOver sequence.");
             GameOverHandler.ResetGameOverState();
        }
         else
        {
            Time.timeScale = 1; // 確保非遊戲結束時時間正常
        }
    }

    public void HandleRoomUpdateFromServer(ServerRoomState roomState) //
    {
        Debug.Log($"GameManager: Handling RoomUpdateFromServer. Room: {roomState.roomId}, GameStarted: {roomState.gameStarted}, GameOver: {roomState.gameOver}");

        // PlayerId 應該在加入房間或創建房間時，由 RoomActionHandler 設定到 CurrentState
        // 如果 CurrentState.PlayerId 仍然為空，可能需要從 roomState.self.playerId 設定
        if (string.IsNullOrEmpty(CurrentState.PlayerId) && roomState.self != null)
        {
            CurrentState.PlayerId = roomState.self.playerId;
        }
        CurrentState.UpdateFromRoomStateServer(roomState, DataConverter, UIManager, CurrentState.PlayerId); //


        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); //
            UIManager.UpdateUI(); //
        }

        if (roomState.gameOver)
        {
            GameOverHandler.ProcessRoomGameOverUpdate(roomState, CurrentState.PlayerId);
        }
         else if (GameOverHandler.IsGameOverSequenceRunning) // 如果之前是結束狀態，現在不是了
        {
             Debug.Log("GameManager: RoomUpdate indicates game is NOT over, resetting GameOver sequence.");
             GameOverHandler.ResetGameOverState();
        }
        else
        {
            Time.timeScale = 1;
        }
    }


    public void HandlePlayerCardPlayConfirmed(string playedCardId) //
    {
        Debug.Log($"GameManager: Player card play confirmed for card ID: {playedCardId}. Triggering animation.");
        if (UIManager == null || CardAnimationManager == null)
        {
            Debug.LogError("GameManager: UIManager or CardAnimationManager is null, cannot trigger animation.");
            return;
        }

        GameObject cardObjectToAnimate = UIManager.FindPlayerHandCardObjectById(playedCardId); //

        if (cardObjectToAnimate != null)
        {
            CardUI cardUI = cardObjectToAnimate.GetComponent<CardUI>(); //
            if (cardUI != null && cardUI.card != null) //
            {
                Card cardData = cardUI.card; //
                Debug.Log($"GameManager: Found card '{cardData.name}' in hand to animate.");
                UIManager.PlayCardWithAnimation(cardData, true, cardObjectToAnimate, UIManager.playerHandPanel, UIManager.playerFieldPanel); //
            }
            else Debug.LogError($"GameManager: CardUI or Card data missing on GameObject for card ID: {playedCardId}");
        }
        else
        {
            Debug.LogWarning($"GameManager: Could not find card GameObject in player's hand UI for ID: {playedCardId} to animate.");
        }
        // 實際狀態更新依賴 gameStateUpdate
    }

    public void HandleOpponentPlayCard(ServerCard cardPlayed, string opponentPlayerName) //
    {
        Debug.Log($"GameManager: {opponentPlayerName} (Opponent) played card: {cardPlayed.name}");
        Card clientCard = DataConverter.ConvertServerCardToClientCard(cardPlayed, UIManager); //

        if (clientCard != null)
        {
            // 簡單處理：直接加入數據，UI更新時會顯示，或觸發一個通用動畫
            // CurrentState.OpponentField.Add(clientCard); // 依賴 GameStateUpdateFromServer
            // TODO: 考慮為對手出牌觸發一個更通用的動畫，不依賴找到手牌物件
            Debug.LogWarning("GameManager: HandleOpponentPlayCard - animation for opponent card play needs review. Card added to data, UI will update.");
        }
        if (UIManager != null) UIManager.UpdateUI(); //
    }

    public void HandleAIPlayCard(ServerCard serverCardPlayed) //
    {
        Debug.Log($"GameManager: Handling online AI card play: {serverCardPlayed.name}");
        Card clientCard = DataConverter.ConvertServerCardToClientCard(serverCardPlayed, UIManager); //

        if (clientCard != null && UIManager != null && CardAnimationManager != null)
        {
            GameObject tempAICardObject = Instantiate(UIManager.cardPrefab, UIManager.animationContainer); //
            CardUI aiCardUI = tempAICardObject.GetComponent<CardUI>(); //
            RectTransform tempAIRectTransform = tempAICardObject.GetComponent<RectTransform>();

            if (aiCardUI != null && tempAIRectTransform != null)
            {
                if (UIManager.opponentHandPanel != null && UIManager.animationContainer != null) //
                {
                    Vector3 opponentHandPanelCenterWorld = UIManager.opponentHandPanel.position; //
                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        UIManager.animationContainer as RectTransform, //
                        RectTransformUtility.WorldToScreenPoint(null, opponentHandPanelCenterWorld),
                        null,
                        out localPoint
                    );
                    tempAIRectTransform.anchoredPosition = localPoint;
                } else tempAIRectTransform.localPosition = Vector3.zero;

                aiCardUI.Initialize(clientCard, false, false); //
                if (aiCardUI.cardDetailsContainer != null) aiCardUI.cardDetailsContainer.SetActive(true); //

                UIManager.PlayCardWithAnimation(clientCard, false, tempAICardObject, UIManager.opponentHandPanel, UIManager.opponentFieldPanel); //
            }
            else
            {
                Debug.LogError("GameManager: Failed to get CardUI or RectTransform on tempAICardObject for AI.");
                if (tempAICardObject != null) Destroy(tempAICardObject);
            }
        }
        else Debug.LogError($"GameManager: Failed to convert ServerCard '{serverCardPlayed.name}' or UIManager/CardAnimationManager is null.");
    }


    // CheckGameOver 由 GameOverHandler 處理離線情況，線上情況由伺服器訊息觸發 GameOverHandler
    public void CheckGameOver() //
    {
        if (CurrentState.CurrentGameMode == GameMode.OfflineSinglePlayer)
        {
            GameOverHandler.CheckOfflineGameOver();
        }
        // 線上模式的遊戲結束由 HandleGameStateUpdateFromServer 或 HandleRoomUpdateFromServer 觸發 GameOverHandler
    }

    // --- 房間操作請求，轉交給 RoomActionHandler ---
    public void RequestCreateRoom(string playerName) //
    {
        // playerName 應該從 UI 輸入或預設值獲取
        string effectivePlayerName = string.IsNullOrEmpty(playerName) ? (PlayerId ?? "玩家") : playerName;
        RoomActionHandler.RequestCreateRoom(effectivePlayerName);
    }

    public void RequestJoinRoom(string targetRoomId, string playerName) //
    {
        string effectivePlayerName = string.IsNullOrEmpty(playerName) ? (PlayerId ?? "玩家") : playerName;
        RoomActionHandler.RequestJoinRoom(targetRoomId, effectivePlayerName);
    }
    public void RequestLeaveRoom() //
    {
        RoomActionHandler.RequestLeaveRoom();
    }


    // --- WebSocketManager 回調 ---
    public void OnWebSocketConnected() //
    {
        Debug.Log("GameManager: WebSocket Connected.");
        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); //
            if (CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom && !CurrentState.IsInRoom)
            {
                UIManager.SetRoomPanelActive(true); //
                UIManager.UpdateRoomStatus("已連接到房間伺服器。\n請創建或加入房間。"); //
            }
        }
         // 如果 PlayerId 尚未設定 (例如，之前是離線模式或首次啟動)，可以在這裡設定一個臨時的
        if (string.IsNullOrEmpty(CurrentState.PlayerId))
        {
            CurrentState.PlayerId = "Player" + Random.Range(1000, 9999);
            Debug.Log($"GameManager: Assigned temporary PlayerId: {CurrentState.PlayerId}");
        }
    }

    public void OnWebSocketDisconnected(string reason) //
    {
        Debug.LogWarning($"GameManager: WebSocket Disconnected. Reason: {reason}");
        if (UIManager != null)
        {
            if (CurrentState.CurrentGameMode == GameMode.OnlineSinglePlayerAI || CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom)
            {
                UIManager.ShowConnectingMessage($"連接已斷開: {reason}\n請檢查網絡並嘗試返回主選單重連。"); //
            }
        }
        // 重置部分狀態
        CurrentState.IsPlayerTurn = false;
        // IsInRoom 的重置應該由成功的 LeaveRoom 響應或 RoomUpdate 表明玩家不在房間時處理
        // 如果是非正常斷線，IsInRoom 可能保持 true 直到重新連接或返回主選單
        // CurrentState.IsInRoom = false; // 謹慎處理，避免與正常離開房間邏輯衝突
    }


    
}

