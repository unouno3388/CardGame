using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for Linq usage
using System.Collections; // 【新增】為了協程
using UnityEngine.SceneManagement; // 【新增】為了 SceneManager
using Newtonsoft.Json;
using System.Threading.Tasks;
using UnityEngine.UI; // 【新增】為了 JSON 序列化和反序列化
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static GameMode initialGameMode = GameMode.OfflineSinglePlayer;
    public static string GameSceneName = "SampleScene";


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

    public MenuManager menuManager;
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

    #region  Game Over 處理變數
    private bool _isGameOverSequenceRunning = false; // 標記遊戲結束序列是否已啟動，防止重複觸發
    private string _pendingGameOverMessage = "";
    private bool _pendingPlayerWon = false;
    private Coroutine _gameOverDisplayCoroutine = null; // 用於儲存協程的引用，以便可以停止它
    #endregion
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

        SceneLoadingManager.Instance.RegisterSceneLoadCallback(SceneLoadingManager.GameSceneName, OnSceneLoaded); // 註冊場景載入回調

        // *** 確保這個 Awake 方法存在，並且正確獲取了 MenuManager 的引用 ***
        if (menuManager == null)
        {
            menuManager = FindObjectOfType<MenuManager>(); // 尋找場景中第一個 MenuManager 的實例
            if (menuManager == null)
            {
                Debug.LogError("GameManager: 在場景中找不到 MenuManager 實例！請確保 SceneLoader 存在並掛載 MenuManager。");
            }
        }

        //UIManager = GetComponent<UIManager>();
        UIManager = FindObjectOfType<UIManager>();
        aiManager = GetComponent<AIManager>();
        wsManager = GetComponent<WebSocketManager>(); // 確保 WebSocketManager 先被初始化
        cardPlayService = gameObject.AddComponent<CardPlayService>();
        //SetupMessageHandlers();
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

        // 在遊戲開始時確保遊戲結束 Panel 是隱藏的
        if (menuManager != null)
        {
            menuManager.gameOverManager.HideGameOverPanel();
        }
        // 確保遊戲時間刻度是正常的
        Time.timeScale = 1;
    }
    void SetupMessageHandlers()
    {
        if (wsManager == null) return;

        // 注意：這裡的處理函式需要符合 MessageHandlerDelegate 的簽章 (object data)
        // 並且在內部進行 JsonConvert.DeserializeObject<SpecificType>(data.ToString())
        wsManager.RegisterMessageHandler("gameStart", (data) =>
        {
            ServerGameState gameStartState = JsonConvert.DeserializeObject<ServerGameState>(data.ToString()); //
            HandleGameStartFromServer(gameStartState); //
        });
        wsManager.RegisterMessageHandler("gameStateUpdate", (data) =>
        {
            ServerGameState gameState = JsonConvert.DeserializeObject<ServerGameState>(data.ToString()); //
            HandleGameStateUpdateFromServer(gameState); //
        });
        wsManager.RegisterMessageHandler("roomUpdate", (data) =>
        {
            ServerRoomState roomState = JsonConvert.DeserializeObject<ServerRoomState>(data.ToString()); //
            HandleRoomUpdateFromServer(roomState); //
        });
        wsManager.RegisterMessageHandler("aiAction", (data) =>
        {
            var aiActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (aiActionData.TryGetValue("actionType", out object aiActionType) && aiActionType.ToString() == "playCard") //
            {
                if (aiActionData.TryGetValue("card", out object cardObj)) //
                {
                    ServerCard aiCard = JsonConvert.DeserializeObject<ServerCard>(cardObj.ToString()); //
                    HandleAIPlayCard(aiCard); //
                }
            }
        });
        wsManager.RegisterMessageHandler("opponentPlayCard", (data) =>
        {
            var opponentPlayData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (opponentPlayData.TryGetValue("card", out object cardData) && //
                opponentPlayData.TryGetValue("playerName", out object playerNameObj)) //
            {
                ServerCard playedCard = JsonConvert.DeserializeObject<ServerCard>(cardData.ToString()); //
                HandleOpponentPlayCard(playedCard, playerNameObj.ToString()); //
            }
        });
        wsManager.RegisterMessageHandler("roomCreated", (data) =>
        {
            // 在 GameMessage 中, roomId 和 message 是頂層欄位, 不是在 data 裡
            // 但如果伺服器確實將它們放在 data 中，則需要像這樣處理：
            // var roomData = JsonConvert.DeserializeObject<Dictionary<string, string>>(data.ToString());
            // string roomId = roomData.TryGetValue("roomId", out var r) ? r : null;
            // string message = roomData.TryGetValue("message", out var m) ? m : null;
            // Debug.Log($"Room created: {roomId}, Message: {message}");
            // -- 假設 baseMessage 仍然在 HandleMessageFromServer 的作用域內 --
            // -- 或者讓 WebSocketManager 傳遞整個 baseMessage 給處理器 --
            // 為了簡單起見，假設處理器可以訪問到 baseMessage (這需要修改委派簽章)
            // 或者，更好的方式是讓 WebSocketManager 在呼叫處理器前就解析好這些通用欄位。
            // 目前的範例是直接將 baseMessage.data 傳入，所以無法直接訪問 baseMessage.roomId。
            // 這裡我們假設 GameMessage 的 roomId 和 message 欄位由 WebSocketManager 在分發前提取。
            // 實際上，像 roomCreated, roomJoined, leftRoom, error 這類訊息，
            // 其關鍵資訊 (roomId, message) 通常不在 `data` 欄位中，而是在 `GameMessage` 的頂層欄位。
            // 這表示 `HandleMessageFromServer` 在分派給這些特定處理器時，可能需要傳遞整個 `GameMessage` 物件，
            // 或者這些處理器需要一個不同的委派簽章，例如 `Action<GameMessage>`.
            // 為了保持一致性，且不修改委派，我們先假設 `baseMessage` 透過某种方式可以被訪問，
            // 或者像原本的 switch case 一樣，直接在 `HandleMessageFromServer` 中處理這些頂層欄位。

            // 以下是基於原 switch case 的邏輯，假設這些資訊從 data 中提取 (如果後端是這樣設計的話)
            var eventData = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data)); // 重新序列化再反序列化以匹配 GameMessage 結構
            Debug.Log($"Room created: {eventData.roomId}, Message: {eventData.message}"); //
            RoomId = eventData.roomId; //
            IsInRoom = true; //
            if (UIManager != null)
            { //
                UIManager.UpdateRoomStatus($"房間創建成功！\n房間ID: {eventData.roomId}\n等待對手加入..."); //
                UIManager.SetRoomPanelActive(true); //
                UIManager.ToggleRoomJoinCreateButtons(false); //
            }
        });
        wsManager.RegisterMessageHandler("roomJoined", (data) =>
        {
            var eventData = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data));
            Debug.Log($"Joined room: {eventData.roomId}, Message: {eventData.message}"); //
            RoomId = eventData.roomId; //
            IsInRoom = true; //
            if (UIManager != null)
            { //
                UIManager.UpdateRoomStatus($"成功加入房間: {eventData.roomId}"); //
            }
        });
        wsManager.RegisterMessageHandler("leftRoom", (data) =>
        {
            var eventData = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data));
            Debug.Log($"Left room: {eventData.message}"); //
            RoomId = null; //
            IsInRoom = false; //
            if (UIManager != null)
            { //
                UIManager.UpdateRoomStatus("您已離開房間。"); //
                UIManager.SetRoomPanelActive(CurrentGameMode == GameMode.OnlineMultiplayerRoom); //
                UIManager.ToggleRoomJoinCreateButtons(true); //
            }
        });
        wsManager.RegisterMessageHandler("error", (data) =>
        {
            var eventData = JsonConvert.DeserializeObject<GameMessage>(JsonConvert.SerializeObject(data));
            Debug.LogError("Server error: " + eventData.message); //
            if (UIManager != null) //
            {
                UIManager.ShowErrorPopup(eventData.message); //
            }
        });
        wsManager.RegisterMessageHandler("playerAction", (data) =>
        {
            var playerActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data.ToString()); //
            if (playerActionData.TryGetValue("action", out object playerActionType) && playerActionType.ToString() == "playCard" && //
                playerActionData.TryGetValue("success", out object successObj) && (bool)successObj == true) //
            {
                if (playerActionData.TryGetValue("cardId", out object cardIdObj)) //
                {
                    string playedCardId = cardIdObj.ToString(); //
                    Debug.Log($"WebSocketManager: Received playerAction confirmation for cardId: {playedCardId}"); //
                    HandlePlayerCardPlayConfirmed(playedCardId); //
                }
            }
        });
    }
    #region  場景載入處理

    void OnSceneLoaded()
    {
        // 檢查載入的是否為遊戲主場景
        //if (scene.name == GameSceneName) // 使用先前定義的 GameSceneName
        //{
        //Debug.Log($"遊戲場景 '{scene.name}' 已載入。準備使用模式: {initialGameMode} 開始遊戲。");

        // 確保遊戲時間正常 (如果因為上一局遊戲結束而設為0)
        Time.timeScale = 1; //
        // 嘗試獲取 UIManager (如果它也存在於遊戲場景或 DontDestroyOnLoad)
        UIManager = FindObjectOfType<UIManager>();
        Debug.Log($"UIManager found: {UIManager != null}");
        SetupMessageHandlers();
        // 嘗試獲取 MenuManager (如果它也存在於遊戲場景或 DontDestroyOnLoad)
        // 但通常遊戲結束畫面不由主選單的 MenuManager 控制，而是由遊戲場景內的 UIManager 控制
        if (menuManager == null)
        {
            menuManager = FindObjectOfType<MenuManager>(); //
        }

        // 隱藏遊戲結束畫面 (如果它是由 UIManager 控制的)
        if (UIManager != null && UIManager.gameOverText != null && UIManager.gameOverText.gameObject.activeSelf)
        {
            UIManager.gameOverText.gameObject.SetActive(false); // 或者呼叫 UIManager.HideGameOver()
        }
        // 隱藏 MenuManager 控制的 GameOverPanel (如果 MenuManager 有效)
        else if (menuManager != null && menuManager.gameOverManager.gameOverPanel != null && menuManager.gameOverManager.gameOverPanel.activeSelf) //
        {
            menuManager.gameOverManager.HideGameOverPanel(); //
        }


        // 呼叫 StartGame 來初始化或重新初始化遊戲
        StartGame(initialGameMode); //
        //}

    }
    #endregion

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
        if (UIManager != null)
        {
            UIManager.SetRoomPanelActive(currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom);
            if (UIManager.gameOverText != null) UIManager.gameOverText.gameObject.SetActive(false); //
            UIManager.HideConnectingMessage(); //
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
            if (UIManager != null) UIManager.ShowConnectingMessage("正在連接到AI伺服器...");
        }
        else if (currentGameMode == GameMode.OnlineMultiplayerRoom)
        {
            // 連接到房間伺服器
            wsManager.ConnectToServer(ROOM_SERVER_URL);
            // UI顯示房間創建/加入界面
            if (UIManager != null) UIManager.ShowConnectingMessage("正在連接到房間伺服器...");
            // UIManager.SetRoomPanelActive(true); // 這個在 InitializeGameDefaults 後由 UIManager 自己控制
        }
        // 舊的 OnlineMultiplayer 模式的處理 (如果保留)
        // else if (currentGameMode == GameMode.OnlineMultiplayer)
        // {
        //     wsManager.ConnectToServer(ROOM_SERVER_URL); // 或者一個通用的P2P協調伺服器URL
        // }

        if (currentGameMode != GameMode.OfflineSinglePlayer)
        {
            // 線上模式的牌組和初始手牌通常由伺服器在 gameStart 或 roomUpdate 中提供
            // 此處不清空，等待伺服器訊息
        }

        if (UIManager != null) UIManager.UpdateUI(); // 初始UI更新
    }
    //離線模式測試用的
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

    //離線模式測試用的
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
        if (initialState.aiField != null) // 【新增】處理AI初始場地（如果有）
        {
            opponentField = ConvertServerCardsToClientCards(initialState.aiField);
        }
        else
        {
            opponentField.Clear();
        }

        // 清除"連接中"的訊息
        if (UIManager != null)
        {
            UIManager.HideConnectingMessage();
            UIManager.UpdateUI();
            if (initialState.gameOver) CheckGameOver(); // 如果遊戲開始時就已結束 (不太可能，但做個檢查)
        }
    }

    // 【新增】處理從伺服器收到的遊戲狀態更新 (通用於兩種線上模式)
    // 修改 HandleGameStateUpdateFromServer 方法
    public void HandleGameStateUpdateFromServer(ServerGameState updatedState)
    {
        Debug.Log("Handling GameStateUpdateFromServer. Player turn: " + updatedState.isPlayerTurn + ", GameOver: " + updatedState.gameOver); //

        // 更新自己的狀態 (假設 updatedState 是針對當前客戶端的視角)
        playerHealth = updatedState.playerHealth; //
        playerMana = updatedState.playerMana; //
        maxMana = updatedState.playerMaxMana; //
        if (updatedState.playerHand != null)
        { //
            playerHand = ConvertServerCardsToClientCards(updatedState.playerHand); //
        }

        // 更新對手的狀態
        if (currentGameMode == GameMode.OnlineSinglePlayerAI)
        { //
            opponentHealth = updatedState.aiHealth; //
            opponentMana = updatedState.aiMana; //
            opponentMaxMana = updatedState.aiMaxMana; //
            opponentServerHandCount = updatedState.aiHandCount ?? 0; //
            if (updatedState.aiField != null) //
            {
                opponentField = ConvertServerCardsToClientCards(updatedState.aiField); //
            }
            else
            {
                opponentField.Clear(); //
            }
        }
        else if (currentGameMode == GameMode.OnlineMultiplayerRoom)
        { //
            if (updatedState.opponentState != null)
            { //
                opponentHealth = updatedState.opponentState.health; //
                opponentMana = updatedState.opponentState.mana; //
                opponentMaxMana = updatedState.opponentState.maxMana; //
            }
        }

        isPlayerTurn = updatedState.isPlayerTurn; //

        // 【重要】先更新UI，讓血量等數值變化先生效
        if (UIManager != null)
        { //
            UIManager.HideConnectingMessage(); //
            UIManager.SetRoomPanelActive(currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom && !updatedState.gameStarted); //
            UIManager.UpdateUI(); // 立即更新UI以反映最新的血量等狀態
        }

        // 檢查遊戲是否結束
        if (updatedState.gameOver && !_isGameOverSequenceRunning) // 如果遊戲結束且結束序列尚未啟動
        {
            _isGameOverSequenceRunning = true; // 標記序列已啟動

            // 決定勝利訊息和狀態
            if (currentGameMode == GameMode.OnlineSinglePlayerAI) //
            {
                _pendingPlayerWon = (updatedState.winner == "Player"); //
                _pendingGameOverMessage = _pendingPlayerWon ? "You Win!" : "You lose!"; //
                if (updatedState.winner != "Player" && updatedState.winner != "AI" && !string.IsNullOrEmpty(updatedState.winner))
                {
                    // 如果 winner 不是 "Player" 或 "AI"，但也不是空的，可能是一個 player ID
                    // 在 OnlineSinglePlayerAI 模式下，這可能表示 AI 贏了（如果 winner 是 AI 的 ID）
                    // 或者是一個未預期的值，此時可以默認為對手勝利或一般結束訊息
                    _pendingGameOverMessage = "You lose!!"; // 或者 "遊戲結束!"
                    _pendingPlayerWon = false;
                }
                else if (string.IsNullOrEmpty(updatedState.winner))
                {
                    _pendingGameOverMessage = "GameOver!"; // 如果沒有winner資訊
                    _pendingPlayerWon = false; // 視情況而定
                }
            }
            else if (currentGameMode == GameMode.OnlineMultiplayerRoom)
            {
                _pendingPlayerWon = (updatedState.winner == PlayerId); // 假設 winner 是贏家 PlayerId
                _pendingGameOverMessage = _pendingPlayerWon ? "玩家勝利!" : "對手勝利!";
                if (string.IsNullOrEmpty(updatedState.winner))
                { // 如果沒有 winner 資訊
                    _pendingGameOverMessage = "遊戲結束!";
                    // _pendingPlayerWon 應根據遊戲規則設定 (例如平手)
                }
            }
            // 你可以為其他遊戲模式添加類似的邏輯

            Debug.Log($"遊戲結束狀態已收到。勝利者: {updatedState.winner}。將等待動畫播放完畢...");

            // 停止可能正在運行的舊的遊戲結束協程
            if (_gameOverDisplayCoroutine != null)
            {
                StopCoroutine(_gameOverDisplayCoroutine);
            }
            // 啟動協程來處理延遲顯示 GameOver 畫面
            _gameOverDisplayCoroutine = StartCoroutine(ShowGameOverScreenAfterAnimations());
        }
        else if (!updatedState.gameOver && _isGameOverSequenceRunning)
        {
            // 如果後續的狀態更新說遊戲尚未結束，則取消遊戲結束序列
            Debug.Log("後續狀態更新指示遊戲尚未結束，取消遊戲結束序列。");
            _isGameOverSequenceRunning = false;
            if (_gameOverDisplayCoroutine != null)
            {
                StopCoroutine(_gameOverDisplayCoroutine);
                _gameOverDisplayCoroutine = null;
            }
            Time.timeScale = 1; // 確保遊戲時間恢復正常
        }
        else if (!updatedState.gameOver)
        {
            Time.timeScale = 1; // 確保遊戲時間在非結束時正常運行
        }
        // 原本的 Time.timeScale = 0; 和 UIManager.ShowGameOver 移至協程中
    }

    // 新增協程方法
    private IEnumerator ShowGameOverScreenAfterAnimations()
    {
        Debug.Log("協程 ShowGameOverScreenAfterAnimations 已啟動。");

        // 1. 等待一小段時間，讓UI有機會開始渲染更新（例如血條動畫）
        //    yield return null; // 等待下一幀
        yield return new WaitForSeconds(0.2f); // 給予一個稍微長一點的延遲，確保UI動畫能被觀察到

        // 2. 等待所有卡牌動畫播放完畢
        if (CardAnimationManager.Instance != null) //
        {
            float waitStartTime = Time.realtimeSinceStartup;
            float maxWaitTime = 10f; // 設定一個最長等待時間，防止無限等待
            while (CardAnimationManager.Instance.IsAnimationPlaying()) //
            {
                if (Time.realtimeSinceStartup - waitStartTime > maxWaitTime)
                {
                    Debug.LogWarning("等待卡牌動畫超時，將直接顯示遊戲結束畫面。");
                    break;
                }
                Debug.Log("等待卡牌動畫播放完成...");
                yield return null; // 等待下一幀
            }
            Debug.Log("卡牌動畫播放完成或超時。");
        }
        else
        {
            Debug.LogWarning("CardAnimationManager.Instance 為空，無法等待卡牌動畫。");
        }

        // 3. 再次檢查遊戲結束標記 (以防在這期間狀態又改變了)
        if (!_isGameOverSequenceRunning)
        {
            Debug.Log("在等待動畫期間，遊戲結束狀態被重置，不再顯示GameOver畫面。");
            yield break; // 退出協程
        }

        // 4. 顯示 GameOver 畫面
        Debug.Log($"準備顯示遊戲結束畫面: {_pendingGameOverMessage}");
        if (menuManager != null) //
        {
            menuManager.gameOverManager.ShowGameOverPanel(_pendingGameOverMessage, _pendingPlayerWon); //
        }
        else if (UIManager != null) // 作為備選，如果 MenuManager 不可用
        {
            UIManager.ShowGameOver(_pendingGameOverMessage); // 但這個方法沒有 isWin 參數
            Debug.LogWarning("使用 UIManager.ShowGameOver 顯示遊戲結束訊息，此方法不包含 'isWin' 參數。");
        }
        else
        {
            Debug.LogError("MenuManager 和 UIManager 皆為空，無法顯示遊戲結束畫面！");
        }

        // 5. 暫停遊戲
        Time.timeScale = 0; //
        Debug.Log("遊戲已暫停。");

        // 6. 【新增】斷開 WebSocket 連線 (如果是在線模式)
        if (currentGameMode == GameMode.OnlineSinglePlayerAI || currentGameMode == GameMode.OnlineMultiplayerRoom) //
        {
            if (wsManager != null && wsManager.IsConnected()) //
            {
                Debug.Log("遊戲結束，準備斷開 WebSocket 連線...");
                // wsManager.CloseConnection() 是一個 async Task 方法。
                // 在協程中直接呼叫它，協程不會等待它完成，這通常是可以接受的，
                // 因為我們只是想啟動關閉流程。
                Task.Run(async () =>
                {
                    await wsManager.CloseConnection(); // 這個方法會處理斷線和清理
                    Debug.Log("WebSocket 連線已關閉。");
                });
            }
        }

        // 7. 重置標記
        _isGameOverSequenceRunning = false; //
        _gameOverDisplayCoroutine = null; //
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
    // 你可能也需要在 HandleRoomUpdateFromServer 方法中應用類似的邏輯，如果它也會處理 gameOver 狀態：
    public void HandleRoomUpdateFromServer(ServerRoomState roomState)
    {
        // ... (更新房間和玩家狀態的現有邏輯) ...
        Debug.Log($"Handling RoomUpdateFromServer. Room: {roomState.roomId}, GameStarted: {roomState.gameStarted}, IsInRoom: {IsInRoom}, GameOver: {roomState.gameOver}"); //

        if (UIManager != null)
        { //
            UIManager.HideConnectingMessage(); //
            // 注意：UpdateUI也應該在檢查gameOver之前，以便更新血量等
            UIManager.UpdateUI(); //
        }

        if (roomState.gameOver && !_isGameOverSequenceRunning)
        { //
            _isGameOverSequenceRunning = true;
            _pendingPlayerWon = (roomState.winnerId == PlayerId); //
            _pendingGameOverMessage = _pendingPlayerWon ? "玩家勝利!" : "對手勝利!"; //
            if (string.IsNullOrEmpty(roomState.winnerId) && roomState.gameStarted)
            { // 如果遊戲開始了但沒有明確的winnerId
                _pendingGameOverMessage = "遊戲結束!"; //
                // _pendingPlayerWon 需要根據遊戲規則定義，例如平手或基於其他條件
            }

            Debug.Log($"遊戲結束狀態(房間更新)已收到。勝利者ID: {roomState.winnerId}。將等待動畫播放完畢...");
            if (_gameOverDisplayCoroutine != null) StopCoroutine(_gameOverDisplayCoroutine);
            _gameOverDisplayCoroutine = StartCoroutine(ShowGameOverScreenAfterAnimations());
        }
        else if (!roomState.gameOver && _isGameOverSequenceRunning)
        {
            Debug.Log("後續房間狀態更新指示遊戲尚未結束，取消遊戲結束序列。");
            _isGameOverSequenceRunning = false;
            if (_gameOverDisplayCoroutine != null)
            {
                StopCoroutine(_gameOverDisplayCoroutine);
                _gameOverDisplayCoroutine = null;
            }
            Time.timeScale = 1; // 確保遊戲時間恢復正常
        }
        else if (!roomState.gameOver)
        {
            Time.timeScale = 1; //
        }
    }

    // 【新增】處理對手出牌的通知 (來自伺服器)
    public void HandleOpponentPlayCard(ServerCard cardPlayed, string opponentPlayerName)
    {
        Debug.Log($"{opponentPlayerName} (Opponent) played card: {cardPlayed.name}");

        // 模擬對手出牌動畫，需要對手手牌面板的引用和卡牌物件
        // 1. 從對手手牌數據中移除 (如果客戶端也維護對手手牌數據的話，通常只維護數量)
        // 2. UIManager 播放動畫，將卡牌移動到場地
        Card clientCard = ConvertServerCardToClientCard(cardPlayed);
        if (clientCard != null)
        {
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
    public void HandleAIPlayCard(ServerCard serverCardPlayed)
    {
        Debug.Log($"GameManager: Handling online AI card play: {serverCardPlayed.name}");
        Card clientCard = ConvertServerCardToClientCard(serverCardPlayed);

        if (clientCard != null && UIManager != null)
        {
            // 創建臨時卡牌物件到動畫層
            GameObject tempAICardObject = Instantiate(UIManager.cardPrefab, UIManager.animationContainer);
            CardUI aiCardUI = tempAICardObject.GetComponent<CardUI>();
            RectTransform tempAIRectTransform = tempAICardObject.GetComponent<RectTransform>(); // 獲取 RectTransform

            if (aiCardUI != null && tempAIRectTransform != null)
            { // 確保 RectTransform 也存在
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

                if (targetForAIAnimation != null)
                {
                    Debug.Log($"GameManager.HandleAIPlayCard: Target for AI animation is '{targetForAIAnimation.name}' (InstanceID: {targetForAIAnimation.GetInstanceID()})");
                }
                else
                {
                    Debug.LogError("GameManager.HandleAIPlayCard: UIManager.opponentFieldPanel IS NULL!");
                    if (tempAICardObject != null) Destroy(tempAICardObject);
                    return;
                }

                UIManager.PlayCardWithAnimation(clientCard, false, tempAICardObject, sourceForAIAnimation, targetForAIAnimation);
            }
            else
            {
                Debug.LogError("GameManager: Failed to get CardUI or RectTransform on instantiated tempAICardObject for AI card.");
                if (tempAICardObject != null) Destroy(tempAICardObject);
            }
        }
        else
        {
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
        Debug.Log("CheckGameOver() 方法被呼叫了！"); // <-- 添加這行

        Debug.Log("當前遊戲模式: " + currentGameMode); // 確認遊戲模式
        Debug.Log("玩家生命值: " + playerHealth);       // 確認玩家血量
        Debug.Log("對手生命值: " + opponentHealth);     // 確認對手血量
        // 確保我們有 MenuManager 的引用
        if (menuManager == null)
        {
            Debug.LogError("GameManager: MenuManager 引用丟失，無法顯示遊戲結束畫面。");
            return; // 提前退出，避免空引用錯誤
        }

        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            bool gameEnded = false;
            bool playerWon = false;

            if (playerHealth <= 0)
            {
                playerWon = false; // 玩家輸了
                gameEnded = true;
                menuManager.gameOverManager.ShowGameOverPanel("GameOver!", playerWon);
            }
            else if (opponentHealth <= 0)
            {
                playerWon = true; // 玩家贏了
                gameEnded = true;
                menuManager.gameOverManager.ShowGameOverPanel("YouWin!", playerWon);
            }

            if (gameEnded)
            {
                Time.timeScale = 0; // 暫停遊戲時間
                // 你可以在這裡添加其他遊戲結束的邏輯，例如禁用玩家輸入等
            }
        }
        // 在線上模式下，這裡可能會有伺服器通知的處理

        /*// 在線上模式，遊戲結束主要由伺服器判斷和通知
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
        }*/
    }

    // 【輔助方法】將伺服器卡牌列表轉換為客戶端卡牌列表
    public List<Card> ConvertServerCardsToClientCards(List<ServerCard> serverCards)
    {
        if (serverCards == null) return new List<Card>();
        List<Card> clientCards = new List<Card>();
        foreach (var sc in serverCards)
        {
            clientCards.Add(ConvertServerCardToClientCard(sc));
        }
        return clientCards;
    }

    // 【輔助方法】將單個伺服器卡牌轉換為客戶端卡牌
    public Card ConvertServerCardToClientCard(ServerCard serverCard)
    {
        if (serverCard == null) return null;
        // 你需要一個機制來從卡牌數據 (例如 serverCard.name 或 serverCard.id) 找到對應的 Sprite
        // 這裡僅作轉換，Sprite 需要額外處理
        Sprite cardSprite = UIManager.GetCardSpriteByName(serverCard.name); // 假設 UIManager 有此方法

        return new Card
        {
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
    public void RequestCreateRoom(string playerName)
    {
        if (currentGameMode == GameMode.OnlineMultiplayerRoom && wsManager != null && wsManager.IsConnected())
        {
            wsManager.SendCreateRoomRequest(playerName);
        }
        else
        {
            Debug.LogError("Cannot create room: Not in room mode or not connected.");
        }
    }

    // 【新增】玩家請求加入房間
    public void RequestJoinRoom(string targetRoomId, string playerName)
    {
        if (string.IsNullOrEmpty(targetRoomId))
        {
            UIManager.UpdateRoomStatus("錯誤：房間ID不能為空！");
            return;
        }
        if (currentGameMode == GameMode.OnlineMultiplayerRoom && wsManager != null && wsManager.IsConnected())
        {
            wsManager.SendJoinRoomRequest(targetRoomId, playerName);
        }
        else
        {
            Debug.LogError("Cannot join room: Not in room mode or not connected.");
        }
    }
    // 【新增】玩家請求離開房間
    public void RequestLeaveRoom()
    {
        if (IsInRoom && wsManager != null && wsManager.IsConnected())
        {
            wsManager.SendLeaveRoomRequest(RoomId);
            // 離開房間後的狀態清理，例如重置 IsInRoom 等，可以在收到伺服器 leftRoom 確認後進行
        }
    }


    // 【新增】當 WebSocket 連接成功時由 WebSocketManager 調用
    public void OnWebSocketConnected()
    {
        Debug.Log("GameManager: WebSocket Connected.");
        if (UIManager != null)
        {
            UIManager.HideConnectingMessage();
            if (currentGameMode == GameMode.OnlineMultiplayerRoom && !IsInRoom)
            {
                UIManager.SetRoomPanelActive(true); // 連接成功後，如果是房間模式且還沒在房間裡，顯示房間面板
                UIManager.UpdateRoomStatus("已連接到房間伺服器。\n請創建或加入房間。");
            }
        }
    }

    // 【新增】當 WebSocket 連接失敗或斷開時由 WebSocketManager 調用
    public void OnWebSocketDisconnected(string reason)
    {
        Debug.LogWarning($"GameManager: WebSocket Disconnected. Reason: {reason}");
        if (UIManager != null)
        {
            if (currentGameMode == GameMode.OnlineSinglePlayerAI || currentGameMode == GameMode.OnlineMultiplayerRoom)
            {
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
public class ServerGameState
{
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
public class ServerPlayerState
{ // 用於房間模式中表示一個玩家的公開狀態
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
public class ServerRoomState
{ // 用於接收房間更新
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