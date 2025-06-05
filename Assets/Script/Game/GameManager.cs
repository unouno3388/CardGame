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
            //DontDestroyOnLoad(gameObject);
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
        Debug.Log($"TurnProcessor: {TurnProcessor != null}.");
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

        //StartGame(initialGameMode);

        if (menuManager != null && menuManager.gameOverManager != null)
        {
            menuManager.gameOverManager.HideGameOverPanel(); //
        }
        //Time.timeScale = 1; //
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
        WebSocketManager.RegisterMessageHandler("roomCreated", (data) =>
        { //
          // 我們需要從 data (object) 重建成 GameMessage 才能獲取 roomId, playerId 等頂層欄位
          // 這是原始 GameManager 中的權宜之計。理想情況下，WebSocketManager 應該傳遞整個 GameMessage。
            
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            Debug.LogWarning($"GameManager: Handling roomCreated message with data: {JsonConvert.SerializeObject(data)}"); //
            RoomActionHandler.HandleRoomCreatedResponse(gameMessage); //
        });
        WebSocketManager.RegisterMessageHandler("roomJoined", (data) =>
        { //
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleRoomJoinedResponse(gameMessage); //
        });
        WebSocketManager.RegisterMessageHandler("leftRoom", (data) =>
        { //
            var gameMessage = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); //
            RoomActionHandler.HandleLeftRoomResponse(gameMessage); //
        });
        WebSocketManager.RegisterMessageHandler("error", (data) =>
        { //
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
        Debug.Log($"Current Time.timeScale: {Time.timeScale} at {this.GetType().Name}");
        Time.timeScale = 1; //

        // 重新獲取場景中可能改變的組件引用
        UIManager = FindObjectOfType<UIManager>(); //
        Debug.Log($"UIManager found: {UIManager != null}");
        CardAnimationManager = FindObjectOfType<CardAnimationManager>();
        if (menuManager == null) menuManager = FindObjectOfType<MenuManager>(); //
        if (aiManager == null) aiManager = GetComponent<AIManager>(); //
        if (WebSocketManager == null) WebSocketManager = GetComponent<WebSocketManager>(); //
        if (CardPlayService == null) CardPlayService = gameObject.AddComponent<CardPlayService>(); //
        // 確保服務依賴更新
        GameOverHandler.InitializeDependencies(this, menuManager, UIManager, CardAnimationManager, CurrentState);
        TurnProcessor.InitializeDependencies(this, CurrentState, aiManager, WebSocketManager, UIManager);
        DeckOperator.InitializeDependencies(CurrentState, UIManager, GameOverHandler);
        RoomActionHandler.InitializeDependencies(CurrentState, WebSocketManager, UIManager, this);
        // CardPlayService 可能也需要重新獲取 UIManager
        if (CardPlayService != null && UIManager != null)
        {
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
            // 根據模式決定初始顯示哪個主界面
            if (CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom)
            {
                UIManager.SetRoomPanelActive(true); // 初始顯示房間面板
                if (UIManager.gameplayPanel != null) UIManager.gameplayPanel.SetActive(false); // 確保遊戲面板隱藏
            }
            else
            {
                // 其他模式（如AI或離線）可能直接顯示遊戲面板
                if (UIManager.gameplayPanel != null) UIManager.gameplayPanel.SetActive(true);
                UIManager.SetRoomPanelActive(false);
            }

            if (UIManager.gameOverText != null) UIManager.gameOverText.gameObject.SetActive(false);
            UIManager.HideConnectingMessage();
        }

        if (CurrentState.CurrentGameMode == GameMode.OfflineSinglePlayer)
        {
            InitializeOfflineGame();
        }
        else if (CurrentState.CurrentGameMode == GameMode.OnlineSinglePlayerAI || CurrentState.CurrentGameMode == GameMode.OnlineMultiplayerRoom)
        {
            if (WebSocketManager != null)
            {
                string serverUrl = (CurrentState.CurrentGameMode == GameMode.OnlineSinglePlayerAI) ? AI_SERVER_URL : ROOM_SERVER_URL;
                // 在連接前，確保WebSocketManager的狀態允許新連接
                // WebSocketManager 內部已有 isConnecting 和 ws.State 的檢查
                WebSocketManager.ConnectToServer(serverUrl); // WebSocketManager 內部會處理是否已連接
                if (UIManager != null) UIManager.ShowConnectingMessage($"正在連接到伺服器 ({CurrentState.CurrentGameMode})...");
            }
            else
            {
                Debug.LogError($"GameManager StartGame: WebSocketManager is null for online mode {CurrentState.CurrentGameMode}!");
            }
        }

        if (UIManager != null) UIManager.UpdateUI(); //
    }

    #region 離線模式功能函數區
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
    #endregion

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
    /// <summary>
    /// 處理來自伺服器的房間更新。
    /// 這個方法會更新遊戲狀態，並根據遊戲是否開始或結束來調整 UI。
    /// 如果遊戲已開始且未結束，則顯示遊戲畫面；如果遊戲未開始，則顯示房間面板。
    /// 如果遊戲結束，則處理遊戲結束邏輯。
    /// </summary>
    /// <param name="roomState"></param>
    public void HandleRoomUpdateFromServer(ServerRoomState roomState) //
    {
        Debug.Log($"GameManager: Handling RoomUpdateFromServer. Room: {roomState.roomId}, GameStarted: {roomState.gameStarted}, GameOver: {roomState.gameOver}");

        // PlayerId 應該在加入房間或創建房間時，由 RoomActionHandler 設定到 CurrentState
        // 如果 CurrentState.PlayerId 仍然為空，可能需要從 roomState.self.playerId 設定
        if (string.IsNullOrEmpty(this.PlayerId) && roomState.self != null &&
            !string.IsNullOrEmpty(roomState.self.playerId))
        {
            this.CurrentState.PlayerId = roomState.self.playerId; // 假設 CurrentState 有 PlayerId setter 或 GameManager 直接管理
            Debug.Log($"GameManager: PlayerId set/confirmed from roomUpdate.self: {this.PlayerId}");
        }
        Debug.Log($"[PlayerB-GM] HandleRoomUpdateFromServer: gameStarted={roomState.gameStarted}, MyCurrentPlayerId_BeforeUpdate={this.PlayerId}, roomState.self.id={roomState.self?.playerId}");
        CurrentState.UpdateFromRoomStateServer(roomState, DataConverter, UIManager, CurrentState.PlayerId); //
        Debug.Log($"[PlayerB-GM] After State Update: CurrentState.GameStarted={CurrentState.GameStarted}, CurrentState.PlayerId={CurrentState.PlayerId}, IsMyTurn={CurrentState.IsPlayerTurn}");
        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); // 隱藏連接中訊息

            if (CurrentState.GameStarted && !roomState.gameOver) // **遊戲已開始且未結束**
            {
                Debug.Log("[PlayerB-GM] Condition to show game screen MET. Calling UIManager.ShowGameScreen().");
                // **觸發切換到遊戲畫面的邏輯**
                UIManager.ShowGameScreen(); // << 您需要實現這個方法或類似的邏輯
                UIManager.SetRoomPanelActive(false); // 確保房間面板被隱藏
                Debug.Log("GameManager: Game has started, attempting to show game screen.");
            }
            else if (!CurrentState.GameStarted && IsInRoom) // 仍在房間但遊戲未開始 (例如等待玩家)
            {
                Debug.Log($"[PlayerB-GM] Condition to show game screen NOT MET. CurrentState.GameStarted={CurrentState.GameStarted}, roomState.gameOver={roomState.gameOver}");
                UIManager.SetRoomPanelActive(true); // 保持或顯示房間面板
                // 可能需要更新房間內的玩家列表或狀態
                UIManager.UpdateRoomStatus(roomState.message); // 或從roomState.players構建玩家列表
            }
            // GameOver 的處理應該在下面

            UIManager.UpdateUI(); // 更新所有UI元素（包括遊戲畫面或房間畫面的內容）
        }
        /*
        if (UIManager != null)
        {
            UIManager.HideConnectingMessage(); //
            UIManager.UpdateUI(); //
        }*/

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
            UIManager.UpdateUI();
        }
        // 實際狀態更新依賴 gameStateUpdate
    }

    public void HandleOpponentPlayCard(ServerCard serverCardPlayed, string opponentPlayerName)
    {
        Debug.Log($"[GameManager] HandleOpponentPlayCard: Opponent '{opponentPlayerName}' played '{serverCardPlayed?.name}'. Triggering animation.");
        if (UIManager == null || CardAnimationManager == null || DataConverter == null)
        {
            Debug.LogError("[GameManager] HandleOpponentPlayCard: Missing UIManager, CardAnimationManager, or DataConverter.");
            return;
        }

        Card clientCard = DataConverter.ConvertServerCardToClientCard(serverCardPlayed, UIManager);
        if (clientCard == null)
        {
            Debug.LogError($"[GameManager] HandleOpponentPlayCard: Failed to convert ServerCard '{serverCardPlayed?.name}'.");
            return;
        }

        GameObject tempOpponentCardObject = Instantiate(UIManager.cardPrefab, UIManager.animationContainer);
        CardUI opponentCardUI = tempOpponentCardObject.GetComponent<CardUI>();
        RectTransform opponentCardRect = tempOpponentCardObject.GetComponent<RectTransform>();

        if (opponentCardUI != null && opponentCardRect != null)
        {
            opponentCardUI.Initialize(clientCard, false, false); // isPlayer=false, on "hand"
            if (opponentCardUI.cardDetailsContainer != null) opponentCardUI.cardDetailsContainer.SetActive(true); // Show front for animation
            if (opponentCardUI.artworkImage != null && clientCard.sprite != null) opponentCardUI.artworkImage.sprite = clientCard.sprite;


            // 設置動畫起始位置 (簡化為對手手牌面板中心)
            Vector3 startPos = UIManager.opponentHandPanel.position;
            if (UIManager.animationContainer.GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceCamera ||
                UIManager.animationContainer.GetComponentInParent<Canvas>().renderMode == RenderMode.WorldSpace) {
                startPos = UIManager.animationContainer.InverseTransformPoint(startPos); // Convert to local if animation container is not overlay
                opponentCardRect.localPosition = startPos;
            } else { // ScreenSpaceOverlay
                opponentCardRect.position = startPos;
            }
            opponentCardRect.localScale = UIManager.playerHandPanel.GetChild(0).localScale; // 假設和玩家手牌大小一致


            Debug.Log($"[GameManager] Playing animation for opponent's card: {clientCard.name}");
            UIManager.PlayCardWithAnimation(clientCard, false, tempOpponentCardObject, UIManager.opponentHandPanel, UIManager.opponentFieldPanel);
        }
        else
        {
            Debug.LogError("[GameManager] HandleOpponentPlayCard: Failed to get CardUI or RectTransform on tempOpponentCardObject.");
            if (tempOpponentCardObject != null) Destroy(tempOpponentCardObject);
        }
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
                }
                else tempAIRectTransform.localPosition = Vector3.zero;

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
            UIManager.HideConnectingMessage();
            if (CurrentState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom
                && !CurrentState.IsInRoom && !CurrentState.GameStarted) // 新增 !CurrentState.gameStarted 條件
            {
                // UIManager.ShowRoomScreen(); // 或者通過 SetRoomPanelActive(true) 間接實現
                UIManager.SetRoomPanelActive(true);
                UIManager.UpdateRoomStatus("已連接到房間伺服器。\n請創建或加入房間。");
            }
            // 如果是重連到已開始的遊戲，後續的 roomUpdate 會處理界面切換
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