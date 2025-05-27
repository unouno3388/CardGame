using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public int playerHealth = 30;
    public int opponentHealth = 30;
    public int playerMana = 1;
    public int maxMana = 1;
    public int opponentMana = 1; // 新增對手的魔法值
    public int opponentMaxMana = 1; // 新增對手的最大魔法值
    public bool isPlayerTurn = true;
    public List<Card> playerHand = new List<Card>();
    public List<Card> opponentHand = new List<Card>();
    public List<Card> playerField = new List<Card>(); // 保留列表，但不會使用
    public List<Card> opponentField = new List<Card>(); // 保留列表，但不會使用
    public List<Card> playerDeck = new List<Card>();
    public List<Card> opponentDeck = new List<Card>();
    public UIManager UIManager { get; private set; }
    private AIManager aiManager;
    private WebSocketManager wsManager;
    public WebSocketManager WebSocketManager => wsManager;
    private CardPlayService cardPlayService;
    public CardPlayService CardPlayService => cardPlayService;
    private bool isOnline = false;
    public bool IsOnline => isOnline;
    private bool uiAutoUpdate = true;

    public enum playMode 
    {
        one, two
    };
    private playMode play_Mode = playMode.one;
    public playMode PlayMod 
    {
        get { return play_Mode; }
        set { play_Mode = value; }
    }

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

        if (UIManager == null || aiManager == null || wsManager == null)
        {
            Debug.LogError("GameManager: Missing required components (UIManager, AIManager, or WebSocketManager)!");
        }
    }

    void Start()
    {
        InitializeGame();
    }

    void InitializeGame()
    {
        playerDeck = GenerateRandomDeck(30);
        opponentDeck = GenerateRandomDeck(30);

        DrawCard(true, 5);
        DrawCard(false, 5);
        UIManager.UpdateUI();
        Debug.Log($"Game initialized. Player turn: {isPlayerTurn}. PlayerDeck: {playerDeck.Count}, OpponentDeck: {opponentDeck.Count}");
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

            deck.Add(new Card
            {
                id = i,
                name = $"{name} {i}",
                cost = cost,
                attack = attack,
                value = value,
                effect = $"{effect} {value} {(effect == "Deal" ? "damage" : "health")}"
            });
        }
        return deck;
    }

    public void StartGame(bool online)
    {
        isOnline = online;
        if (!isOnline)
        {
            Debug.Log("Starting offline game.");
        }
        else
        {
            Debug.Log("Starting online game.");
            wsManager.Connect();
        }
    }

    public void PlayCard2(Card card, bool isPlayer, GameObject cardObject = null)
    {
        if (card == null)
        {
            Debug.LogError("PlayCard: Card is null!");
            return;
        }

        if (isPlayer && !isPlayerTurn)
        {
            Debug.LogWarning("Not player's turn!");
            return;
        }

        // 檢查魔法值是否足夠
        if (isPlayer)
        {
            if (playerMana < card.cost)
            {
                Debug.LogWarning($"Not enough mana for player! Current: {playerMana}, Required: {card.cost}");
                return;
            }
        }
        else
        {
            if (opponentMana < card.cost)
            {
                Debug.LogWarning($"Not enough mana for opponent! Current: {opponentMana}, Required: {card.cost}");
                return;
            }
        }

        if (isPlayer)
        {
            
            playerHand.Remove(card);
            playerMana -= card.cost; // 減少玩家的魔法值
            Debug.Log($"Player played {card.name}. Mana before: {playerMana + card.cost}, Mana after: {playerMana}.");
            if (isOnline)
            {
                wsManager.SendPlayCard(card);
            }
            UIManager.UpdateUI(); // 立即更新 UI
            //RemoveCardFromDeck(card, true); // 從玩家的牌組中移除（已註釋）
            
        }
        else
        {
            // AI or online opponent card
            Debug.Log($"Opponent card :{opponentHand.Contains(card)} = {card.name}");
            if (opponentHand.Contains(card))
            {
                Transform sourcePanel = UIManager.opponentHandPanel;
                Debug.Log($"Opponent card {card.name} found in opponentHandPanel. Source: {sourcePanel.position}");
                Transform targetPanel = UIManager.opponentFieldPanel;
                if (targetPanel != UIManager.opponentFieldPanel)
                {
                    Debug.LogError($"Invalid targetPanel for opponent card {card.name}! Expected opponentFieldPanel, got {targetPanel.name}");
                    targetPanel = UIManager.opponentFieldPanel;
                }

                if (cardObject == null)
                {
                    // Find the card object in opponentHandPanel
                    foreach (Transform child in sourcePanel)
                    {
                        CardUI cardUI = child.GetComponent<CardUI>();
                        if (cardUI != null && cardUI.card == card)
                        {
                            cardObject = child.gameObject;
                            break;
                        }
                    }
                }

                if (cardObject == null)
                {
                    Debug.LogWarning($"Card object for {card.name} not found in opponentHandPanel, skipping animation.");
                    opponentHand.Remove(card);
                    opponentMana -= card.cost; // 減少對手的魔法值
                    Debug.Log($"Opponent played {card.name}. Mana before: {opponentMana + card.cost}, Mana after: {opponentMana}.");
                    //RemoveCardFromDeck(card, false); // 從對手的牌組中移除（已註釋）
                    card.ApplyCardEffect(isPlayer); // 手動應用效果
                    UIManager.UpdateUI(); // 確保 UI 更新
                    CheckGameOver();
                }
                else
                {
                    opponentHand.Remove(card);
                    opponentMana -= card.cost; // 減少對手的魔法值
                    Debug.Log($"Opponent played {card.name}. Mana before: {opponentMana + card.cost}, Mana after: {opponentMana}.");
                    UIManager.PlayCardWithAnimation(card, false, cardObject, sourcePanel, targetPanel);
                    //RemoveCardFromDeck(card, false); // 從對手的牌組中移除（已註釋）
                    Debug.Log($"Opponent played {card.name}. Animation started from opponentHandPanel (Pos: {sourcePanel.position}) to opponentFieldPanel (Pos: {targetPanel.position}).");
                }
            }
            else
            {
                Debug.LogWarning($"Card {card.name} not found in opponentHand!");
                return;
            }
        }
    }

    public void EndTurn()
    {
        if (!isPlayerTurn)
        {
            Debug.LogWarning("Not player's turn to end!");
            return;
        }

        Debug.Log("Player ends turn.");
        isPlayerTurn = false;
        //maxMana = Mathf.Min(maxMana + 1, 10);
        playerMana += 2; // 重置玩家的魔法值
        //if(playerHand.Count<5)
            DrawCard(true, 1);

        if (!isOnline)
        {
            StartCoroutine(aiManager.PlayAITurn());
        }
        else
        {
            wsManager.SendEndTurn();
        }

        UIManager.UpdateUI(); // 保留回合結束時的更新
    }

    public void StartAITurn()
    {
        Debug.Log("Starting AI turn.");
        isPlayerTurn = false;
        StartCoroutine(aiManager.PlayAITurn());
    }

    public void EndAITurn()
    {
        Debug.Log("AI ends turn.");
        isPlayerTurn = true;
        //maxMana = Mathf.Min(maxMana + 1, 10);
       //playerMana = maxMana; // 重置玩家的魔法值
        //opponentMaxMana = Mathf.Min(opponentMaxMana + 1, 10); // 增加對手的最大魔法值
        opponentMana += 2; // 重置對手的魔法值
        Debug.Log($"Opponent mana reset. Max: {opponentMaxMana}, Current: {opponentMana}");
        DrawCard(false, 1);
        UIManager.UpdateUI();
    }

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

    void DrawCard(bool isPlayer, int count)
    {
        List<Card> deck = isPlayer ? playerDeck : opponentDeck;
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                Debug.LogWarning($"{(isPlayer ? "Player" : "Opponent")} deck is empty!");
                return;
            }

            Card card = deck[Random.Range(0, deck.Count)];
            deck.Remove(card);
            if (isPlayer)
            {
                playerHand.Add(card);
                Debug.Log($"Drew card {card.name} to playerHand. playerHand count: {playerHand.Count}");
            }
            else
            {
                opponentHand.Add(card);
                Debug.Log($"Drew card {card.name} to opponentHand. opponentHand count: {opponentHand.Count}");
            }
        }
    }

    public void CheckGameOver()
    {
        if (playerHealth <= 0)
        {
            UIManager.ShowGameOver("對手勝利!");
        }
        else if (opponentHealth <= 0)
        {
            UIManager.ShowGameOver("玩家勝利!");
        }
    }

    public void ReceivePlayCard(Card card)
    {
        Debug.Log($"Received playCard: {card.name} for opponent.");
        //PlayCard(card, false);
        cardPlayService.PlayCard(card, false);
    }

    public void ReceiveEndTurn()
    {
        Debug.Log("Received endTurn from opponent.");
        isPlayerTurn = true;
        maxMana = Mathf.Min(maxMana + 1, 10);
        playerMana = maxMana; // 重置玩家的魔法值
        opponentMaxMana = Mathf.Min(opponentMaxMana + 1, 10); // 增加對手的最大魔法值
        opponentMana = opponentMaxMana; // 重置對手的魔法值
        Debug.Log($"Opponent mana reset. Max: {opponentMaxMana}, Current: {opponentMana}");
        DrawCard(true, 1);
        UIManager.UpdateUI();
    }
}

[System.Serializable]
public class GameMessage
{
    public string type; // 例如 "playCard", "endTurn"
    public string cardId;
    // 可添加更多字段
}