using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Added for Linq usage

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public static GameMode initialGameMode = GameMode.OfflineSinglePlayer; // 預設為單人模式

    public int playerHealth = 30;
    public int opponentHealth = 30;
    public int playerMana = 15;
    public int maxMana = 15;
    public int opponentMana = 15;
    public int opponentMaxMana = 15;
    public bool isPlayerTurn = true;
    public List<Card> playerHand = new List<Card>();
    public List<Card> opponentHand = new List<Card>();
    public List<Card> playerField = new List<Card>();
    public List<Card> opponentField = new List<Card>();
    public List<Card> playerDeck = new List<Card>();
    public List<Card> opponentDeck = new List<Card>();

    public UIManager UIManager { get; private set; }
    private AIManager aiManager;
    private WebSocketManager wsManager;
    public WebSocketManager WebSocketManager => wsManager;
    private CardPlayService cardPlayService;
    public CardPlayService CardPlayService => cardPlayService;

    // 新增：遊戲模式的枚舉
    public enum GameMode
    {
        OfflineSinglePlayer, // 離線單人模式 (對戰AI)
        OnlineMultiplayer    // 線上雙人模式 (對戰其他玩家)
    }

    private GameMode currentGameMode; // 當前遊戲模式的變數
    public GameMode CurrentGameMode
    {
        get => currentGameMode;
        //set
        //{
        //    currentGameMode = value;
         //   Debug.Log($"Current game mode set to: {currentGameMode}");
        //}
    }
    private bool uiAutoUpdate = true; // Added for UI update control
    public bool UIAutoUpdate
    {
        get => uiAutoUpdate;
        set
        {
            uiAutoUpdate = value;
            if (uiAutoUpdate)
            {
                UIManager.UpdateUI();
            }
        }
    }

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
        }

        UIManager = GetComponent<UIManager>();
        aiManager = GetComponent<AIManager>();
        wsManager = GetComponent<WebSocketManager>();
        cardPlayService = gameObject.AddComponent<CardPlayService>();

        if (UIManager == null || aiManager == null || wsManager == null || cardPlayService == null)
        {
            Debug.LogError("GameManager: Missing required components (UIManager, AIManager, WebSocketManager, or CardPlayService)!");
        }
    }

    // 將 Start 移到這裡，因為 StartGame 會在 MenuManager 中調用
    void Start()
    {
        //InitializeGame();
        StartGame(initialGameMode);
    }

    // 初始化遊戲，準備牌組和抽牌，不涉及模式選擇
    void InitializeGame()
    {
        playerHealth = 30; // Reset health
        opponentHealth = 30; // Reset health
        playerMana = 15; // Reset mana
        maxMana = 15; // Reset max mana
        opponentMana = 15; // Reset opponent mana
        opponentMaxMana = 15; // Reset opponent max mana
        isPlayerTurn = true;

        playerHand.Clear();
        opponentHand.Clear();
        playerField.Clear();
        opponentField.Clear();

        playerDeck = GenerateRandomDeck(30);
        opponentDeck = GenerateRandomDeck(30);

        DrawCard(true, 5);
        DrawCard(false, 5);
        UIManager.UpdateUI();
        Debug.Log($"Game initialized. PlayerDeck: {playerDeck.Count}, OpponentDeck: {opponentDeck.Count}");
    }

    // 啟動遊戲的統一入口點
    public void StartGame(GameMode mode)
    {
        currentGameMode = mode; // 設定遊戲模式
        InitializeGame(); // 初始化遊戲狀態（牌組、手牌、生命值等）

        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            StartOfflineGame();
        }
        else if (currentGameMode == GameMode.OnlineMultiplayer)
        {
            StartOnlineGame();
        }
        else
        {
            Debug.LogError("Unsupported game mode!");
        }
    }

    // 處理離線單人模式的啟動邏輯
    private void StartOfflineGame()
    {
        Debug.Log("Starting offline single-player game (vs AI).");
        // AI 在離線模式下初始不進行任何操作，等待玩家結束回合
        // StartCoroutine(aiManager.PlayAITurn()); // 不在遊戲開始時直接啟動AI回合
        UIManager.UpdateUI(); // 確保UI在遊戲開始時更新
    }

    // 處理線上雙人模式的啟動邏輯
    private void StartOnlineGame()
    {
        Debug.Log("Starting online multiplayer game.");
        // 連接 WebSocket 服務器
        wsManager.Connect();
        // 在線模式下，遊戲流程由伺服器控制，初始狀態可能需要等待伺服器通知
        // 這裡不需要立即啟動AI或發送結束回合，等待伺服器通知
        UIManager.UpdateUI(); // 確保UI在遊戲開始時更新
    }

    List<Card> GenerateRandomDeck(int count)
    {
        List<Card> deck = new List<Card>();
        string[] cardNames = { "Fireball", "Ice Blast", "Thunder Strike", "Heal Wave", "Shadow Bolt", "Light Heal", "Flame Slash", "Frost Shield" };
        string[] effects = { "Deal", "Heal" };

        for (int i = 0; i < count; i++)
        {
            string name = cardNames[Random.Range(0, cardNames.Length)];
            string effect = effects[Random.Range(0, effects.Length)];
            int cost = Random.Range(1, 6);
            int attack = effect == "Deal" ? Random.Range(1, 6) : 0;
            int value = effect == "Heal" ? Random.Range(1, 6) : 0;

            // 確保每張卡牌有唯一的ID，以便在線上模式中識別
            deck.Add(new Card
            {
                id = i, // Use 'i' for unique ID in this context
                name = $"{name} {i}",
                cost = cost,
                attack = attack,
                value = value,
                effect = $"{effect} {value} {(effect == "Deal" ? "damage" : "health")}"
            });
        }
        return deck;
    }

    // PlayCard 的邏輯現在主要交由 CardPlayService 處理，GameManager 不再直接包含其邏輯。
    // PlayCard2 方法已移除。

    // 玩家結束回合
    public void EndTurn()
    {
        if (!isPlayerTurn)
        {
            Debug.LogWarning("Not player's turn to end!");
            return;
        }

        Debug.Log("Player ends turn.");
        isPlayerTurn = false;

        // 回合結束時，魔法值和抽牌邏輯
        maxMana = Mathf.Min(maxMana + 1, 10);
        playerMana = maxMana; // 重置玩家的魔法值
        DrawCard(true, 1);

        if (currentGameMode == GameMode.OfflineSinglePlayer)
        {
            StartCoroutine(aiManager.PlayAITurn()); // 離線模式直接啟動AI回合
        }
        else if (currentGameMode == GameMode.OnlineMultiplayer)
        {
            wsManager.SendEndTurn(); // 線上模式發送結束回合訊息
        }

        UIManager.UpdateUI(); // 保留回合結束時的 UI 更新
    }

    // AI 結束回合 (僅用於離線模式)
    public void EndAITurn()
    {
        Debug.Log("AI ends turn.");
        isPlayerTurn = true;

        // 回合結束時，魔法值和抽牌邏輯
        opponentMaxMana = Mathf.Min(opponentMaxMana + 1, 10);
        opponentMana = opponentMaxMana; // 重置對手的魔法值
        DrawCard(false, 1);

        UIManager.UpdateUI(); // 確保 UI 更新
    }

    // 接收線上對手結束回合訊息 (僅用於線上模式)
    public void ReceiveEndTurn()
    {
        Debug.Log("Received endTurn from opponent (online).");
        isPlayerTurn = true;

        // 回合結束時，魔法值和抽牌邏輯
        maxMana = Mathf.Min(maxMana + 1, 10);
        playerMana = maxMana; // 重置玩家的魔法值
        opponentMaxMana = Mathf.Min(opponentMaxMana + 1, 10);
        opponentMana = opponentMaxMana; // 重置對手的魔法值
        DrawCard(true, 1); // 玩家抽牌

        UIManager.UpdateUI();
    }

    // 移除牌組中的卡牌 (現在主要用於測試或特殊效果，因為手牌移除已直接處理)
    public void RemoveCardFromDeck(Card card, bool isPlayer)
    {
        List<Card> deck = isPlayer ? playerDeck : opponentDeck;
        if (deck.Count == 0)
        {
            Debug.LogWarning($"{(isPlayer ? "Player" : "Opponent")} deck is already empty!");
            return;
        }

        // 記錄 Deck 內容進行調試
        Debug.Log($"{(isPlayer ? "Player" : "Opponent")} deck before removal: {string.Join(", ", deck.Select(c => c.id))}");
        int removedCount = deck.RemoveAll(c => c.id == card.id);
        if (removedCount > 0)
        {
            Debug.Log($"{(isPlayer ? "Player" : "Opponent")} deck removed card with id: {card.id}, remaining: {deck.Count}");
        }
        else
        {
            Debug.LogWarning($"{(isPlayer ? "Player" : "Opponent")} deck does not contain card with id: {card.id}");
        }
    }

    // 抽牌邏輯
    void DrawCard(bool isPlayer, int count)
    {
        List<Card> deck = isPlayer ? playerDeck : opponentDeck;
        List<Card> hand = isPlayer ? playerHand : opponentHand;

        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                Debug.LogWarning($"{(isPlayer ? "Player" : "Opponent")} deck is empty! Cannot draw more cards.");
                // 處理牌庫耗盡的情況，例如判斷遊戲結束
                CheckGameOver(); // 牌庫耗盡也可能導致遊戲結束
                return;
            }

            Card card = deck[Random.Range(0, deck.Count)];
            deck.Remove(card);
            hand.Add(card);
            Debug.Log($"Drew card {card.name} to {(isPlayer ? "player" : "opponent")}Hand. {(isPlayer ? "player" : "opponent")}Hand count: {hand.Count}");
        }
    }

    public void CheckGameOver()
    {
        if (playerHealth <= 0)
        {
            UIManager.ShowGameOver("對手勝利!");
            Time.timeScale = 0; // 暫停遊戲
        }
        else if (opponentHealth <= 0)
        {
            UIManager.ShowGameOver("玩家勝利!");
            Time.timeScale = 0; // 暫停遊戲
        }
    }

    // 接收線上對手打出卡牌訊息 (僅用於線上模式)
    public void ReceivePlayCard(Card card)
    {
        Debug.Log($"Received playCard: {card.name} for opponent (online).");
        // online 模式下，收到對手的打牌訊息，直接讓 CardPlayService 處理
        Card actualCardInOpponentHand = opponentHand.Find(c => c.id == card.id);
        if (actualCardInOpponentHand != null)
        {
            // 在線上模式中，對手打牌可能沒有實際的 CardObject，因為我們沒有對手手牌的UI實例
            // UIManager 會根據 CardPlayService 的需求來處理動畫
            cardPlayService.PlayCard(actualCardInOpponentHand, false, null); // 傳遞 null 或一個 placeholder object
        }
        else
        {
            Debug.LogError($"Received card {card.name} (ID: {card.id}) not found in opponent's hand for online play. This should not happen.");
            // 在此情況下，可能需要重新同步遊戲狀態，或強制應用效果
            card.ApplyCardEffect(false);
            CheckGameOver();
        }
    }
}