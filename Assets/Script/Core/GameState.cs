// GameState.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameState : IGameState
{
    public int MaxHealth { get; set; } // 假設最大生命值為 30
    public int PlayerHealth { get; set; }
    public int OpponentHealth { get; set; }
    public int PlayerMana { get; set; }
    public int MaxMana { get; set; }
    public int OpponentMana { get; set; }
    public int OpponentMaxMana { get; set; }
    public bool IsPlayerTurn { get; set; }
    public int OpponentServerHandCount { get; set; }
    public List<Card> PlayerHand { get; private set; } = new List<Card>();
    public List<Card> OpponentHand { get; private set; } = new List<Card>();
    public List<Card> PlayerField { get; private set; } = new List<Card>();
    public List<Card> OpponentField { get; private set; } = new List<Card>();
    public List<Card> PlayerDeck { get; private set; } = new List<Card>();
    public List<Card> OpponentDeck { get; private set; } = new List<Card>();
    public string PlayerId { get; set; }
    public string RoomId { get; set; }
    public bool IsInRoom { get; set; }
    public string OpponentPlayerId { get; set; }
    public GameManager.GameMode CurrentGameMode { get; set; }
    public bool GameStarted { get; set; }
    public void ResetState(GameManager.GameMode mode)
    {
        CurrentGameMode = mode;
        PlayerHealth = 30; // 根據 GameManager.InitializeGameDefaults 調整
        OpponentHealth = 1;
        PlayerMana = 10;
        MaxMana = 10;
        OpponentMana = 1;
        OpponentMaxMana = 1;
        IsPlayerTurn = true; // 初始回合歸屬可能由伺服器決定或根據模式設定

        PlayerHand.Clear();
        OpponentHand.Clear();
        PlayerField.Clear();
        OpponentField.Clear();
        PlayerDeck.Clear();
        OpponentDeck.Clear();
        OpponentServerHandCount = 0;

        // PlayerId 在連接成功後設定
        // RoomId, IsInRoom, OpponentPlayerId 在房間相關操作後設定
        GameStarted = false;
        IsInRoom = false;
        RoomId = null;
        OpponentPlayerId = null;
         Debug.Log($"GameState: Reset complete for mode {mode}. PlayerHealth: {PlayerHealth}");
    }

    public void UpdateFromGameStartServer(ServerGameState initialState, IDataConverter converter, UIManager uiManager)
    {
        PlayerId = initialState.playerId;
        MaxHealth = initialState.maxHealth; // 假設 ServerGameState 有 maxHealth 欄位
        PlayerHealth = initialState.playerHealth;
        OpponentHealth = initialState.aiHealth; // AI 模式
        PlayerMana = initialState.playerMana;
        MaxMana = initialState.playerMaxMana;
        OpponentMana = initialState.aiMana;
        OpponentMaxMana = initialState.aiMaxMana;
        IsPlayerTurn = initialState.isPlayerTurn;

        PlayerHand.Clear();
        if (initialState.playerHand != null)
        {
            PlayerHand.AddRange(converter.ConvertServerCardsToClientCards(initialState.playerHand, uiManager));
        }

        OpponentServerHandCount = initialState.aiHandCount ?? 0;
        OpponentField.Clear();
        if (initialState.aiField != null)
        {
            OpponentField.AddRange(converter.ConvertServerCardsToClientCards(initialState.aiField, uiManager));
        }
        
        if (initialState != null) // 確保 initialState 不是 null
        {
            GameStarted = initialState.gameStarted; // 假設 ServerGameState 有 gameStarted 欄位
        }
        Debug.Log($"GameState: Updated from GameStartServer. PlayerId: {PlayerId}, IsPlayerTurn: {IsPlayerTurn}");
    }

    public void UpdateFromServer(ServerGameState updatedState, IDataConverter converter, UIManager uiManager)
    {
        // 更新玩家狀態
        PlayerHealth = updatedState.playerHealth;
        PlayerMana = updatedState.playerMana;
        MaxMana = updatedState.playerMaxMana;

        PlayerHand.Clear();
        if (updatedState.playerHand != null)
        {
            PlayerHand.AddRange(converter.ConvertServerCardsToClientCards(updatedState.playerHand, uiManager));
        }

        // 更新對手狀態 (基於遊戲模式)
        if (CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI)
        {
            OpponentHealth = updatedState.aiHealth;
            OpponentMana = updatedState.aiMana;
            OpponentMaxMana = updatedState.aiMaxMana;
            OpponentServerHandCount = updatedState.aiHandCount ?? 0;

            OpponentField.Clear();
            if (updatedState.aiField != null)
            {
                OpponentField.AddRange(converter.ConvertServerCardsToClientCards(updatedState.aiField, uiManager));
            }
        }
        else if (CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            if (updatedState.opponentState != null)
            {
                OpponentHealth = updatedState.opponentState.health;
                OpponentMana = updatedState.opponentState.mana;
                OpponentMaxMana = updatedState.opponentState.maxMana;
                OpponentServerHandCount = updatedState.opponentState.handCount; // 假設 handCount 是數量
                // 對手場地卡牌，如果 gameStateUpdate 包含的話
                // 通常 RoomUpdate 會更詳細地包含對手場地
            }
        }

        IsPlayerTurn = updatedState.isPlayerTurn;
        if (updatedState != null)
        {
            GameStarted = updatedState.gameStarted; // 假設 ServerGameState 有 gameStarted 欄位
        }
        Debug.Log($"GameState: Updated from ServerGameState. PlayerHealth: {PlayerHealth}, IsPlayerTurn: {IsPlayerTurn}");
    }

    // In GameState.cs
    public void UpdateFromRoomStateServer(ServerRoomState roomState, IDataConverter converter, UIManager uiManager, string localPlayerIdArgument) // 重命名參數以區分
    {
        Debug.Log($"[PlayerB-GS] UpdateFromRoomStateServer: localPlayerIdArg='{localPlayerIdArgument}', roomState.self.id='{roomState.self?.playerId}', roomState.gameStarted={roomState.gameStarted}");

        if (roomState == null) {
            Debug.LogError("[PlayerB-GS] roomState is null. Aborting update.");
            return;
        }

        RoomId = roomState.roomId;
        IsInRoom = true;
        GameStarted = roomState.gameStarted;
        CurrentGameMode = GameManager.GameMode.OnlineMultiplayerRoom;

        // 更新 self (本地玩家) 的狀態
        if (roomState.self != null)
        {
            // 將本地 PlayerId 更新為伺服器告知的 self.playerId
            // 這是最權威的ID
            if (!string.IsNullOrEmpty(roomState.self.playerId))
            {
                this.PlayerId = roomState.self.playerId;
                Debug.Log($"[PlayerB-GS] Updated/Set this.PlayerId to: '{this.PlayerId}' from roomState.self.playerId.");
            }
            else
            {
                Debug.LogWarning("[PlayerB-GS] roomState.self.playerId is null or empty. Cannot definitively update local PlayerId for self.");
                // 如果 localPlayerIdArgument 有值且 this.PlayerId 仍為空，可以考慮使用它作為備選，但伺服器的 self.playerId 優先
                if (string.IsNullOrEmpty(this.PlayerId) && !string.IsNullOrEmpty(localPlayerIdArgument)) {
                    this.PlayerId = localPlayerIdArgument;
                    Debug.Log($"[PlayerB-GS] Using localPlayerIdArgument ('{localPlayerIdArgument}') as fallback for this.PlayerId.");
                }
            }

            // 現在 this.PlayerId 應該是正確的了，或者至少是目前最佳的猜測。
            // 直接使用 roomState.self 的數據來更新本地玩家狀態，因為 "self" 就是指這個客戶端。
            PlayerHealth = roomState.self.health;
            PlayerMana = roomState.self.mana;
            MaxMana = roomState.self.maxMana;
            if (roomState.self.maxHealth > 0) // 確保 maxHealth 有效
            {
                MaxHealth = roomState.self.maxHealth;
                Debug.LogWarning($"[PlayerB-GS] Updated MaxHealth to {MaxHealth} from roomState.self.maxHealth for PlayerId: {this.PlayerId}.");
            }
            else
            {
                Debug.LogWarning($"[PlayerB-GS] roomState.self.maxHealth is invalid ({roomState.self.maxHealth}). Using default MaxHealth of 30.");
                MaxHealth = roomState.self.health; // 或者其他預設值
            }
            PlayerHand.Clear();
            if (roomState.self.hand != null)
            {
                Debug.Log($"[PlayerB-GS] Self hand from server has {roomState.self.hand.Count} cards before conversion for PlayerId: {this.PlayerId}.");
                List<Card> clientCards = converter.ConvertServerCardsToClientCards(roomState.self.hand, uiManager);
                PlayerHand.AddRange(clientCards);
                Debug.Log($"[PlayerB-GS] Converted {clientCards.Count} client cards for self. Current PlayerHand count: {PlayerHand.Count} for PlayerId: {this.PlayerId}.");
            }
            else
            {
                Debug.LogWarning($"[PlayerB-GS] roomState.self.hand is null for PlayerId: {this.PlayerId}.");
            }

            PlayerField.Clear();
            if (roomState.self.field != null)
            {
                Debug.Log($"[PlayerB-GS] Self field from server has {roomState.self.field.Count} cards before conversion for PlayerId: {this.PlayerId}.");
                List<Card> clientFieldCards = converter.ConvertServerCardsToClientCards(roomState.self.field, uiManager);
                PlayerField.AddRange(clientFieldCards);
                Debug.Log($"[PlayerB-GS] Converted {clientFieldCards.Count} client field cards for self. Current PlayerField count: {PlayerField.Count} for PlayerId: {this.PlayerId}.");
            }
            else
            {
                Debug.LogWarning($"[PlayerB-GS] roomState.self.field is null for PlayerId: {this.PlayerId}.");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerB-GS] roomState.self is null. Cannot update self's state.");
            // 如果 self 為 null，可能需要重置本地玩家的一些狀態或標記錯誤
            PlayerHand.Clear();
            PlayerField.Clear();
        }

        // 在 PlayerId 被賦值後，再判斷回合歸屬
        IsPlayerTurn = (!string.IsNullOrEmpty(roomState.currentPlayerId) && !string.IsNullOrEmpty(this.PlayerId) && roomState.currentPlayerId == this.PlayerId);

        // 更新 opponent (對手) 的狀態
        if (roomState.opponent != null)
        {
            OpponentPlayerId = roomState.opponent.playerId;
            OpponentHealth = roomState.opponent.health;
            OpponentMana = roomState.opponent.mana;
            OpponentMaxMana = roomState.opponent.maxMana;
            OpponentServerHandCount = roomState.opponent.handCount;

            OpponentHand.Clear(); // 對手手牌列表在客戶端通常只用於顯示卡背

            OpponentField.Clear();
            if (roomState.opponent.field != null)
            {
                Debug.Log($"[PlayerB-GS] Opponent field from server has {roomState.opponent.field.Count} cards before conversion for OpponentId: {OpponentPlayerId}.");
                List<Card> clientOpponentFieldCards = converter.ConvertServerCardsToClientCards(roomState.opponent.field, uiManager);
                OpponentField.AddRange(clientOpponentFieldCards);
                Debug.Log($"[PlayerB-GS] Converted {clientOpponentFieldCards.Count} client field cards for opponent. Current OpponentField count: {OpponentField.Count} for OpponentId: {OpponentPlayerId}.");
            }
            else
            {
                Debug.LogWarning($"[PlayerB-GS] roomState.opponent.field is null for OpponentId: {OpponentPlayerId}.");
            }
        }
        else
        {
            OpponentPlayerId = null;
            OpponentHealth = 0; OpponentMana = 0; OpponentMaxMana = 0;
            OpponentServerHandCount = 0;
            OpponentField.Clear(); OpponentHand.Clear();
            Debug.LogWarning("[PlayerB-GS] roomState.opponent is null. Opponent state reset.");
        }

        Debug.Log($"GameState: Updated from RoomStateServer. RoomId: {RoomId}, IsPlayerTurn: {IsPlayerTurn}, PlayerHand Count: {PlayerHand.Count}, OpponentHand Count: {OpponentServerHandCount}");
        Debug.Log($"[PlayerB-GS] After update: this.GameStarted={this.GameStarted}, this.PlayerId='{this.PlayerId}', PlayerHand.Count={PlayerHand.Count}");
    }

    public void AddCardToPlayerHand(Card card) { PlayerHand.Add(card); }
    public void RemoveCardFromPlayerHand(string cardId) { PlayerHand.RemoveAll(c => c.id == cardId); }
    public void AddCardToPlayerDeck(Card card) { PlayerDeck.Add(card); }
    public void AddCardToOpponentDeck(Card card) { OpponentDeck.Add(card); }
    public void AddCardToPlayerField(Card card) { PlayerField.Add(card); }
    public void AddCardToOpponentField(Card card) { OpponentField.Add(card); }

    public Card GetCardFromPlayerDeck()
    {
        if (PlayerDeck.Count == 0) return null;
        Card card = PlayerDeck[Random.Range(0, PlayerDeck.Count)];
        PlayerDeck.Remove(card);
        return card;
    }

    public Card GetCardFromOpponentDeck()
    {
        if (OpponentDeck.Count == 0) return null;
        Card card = OpponentDeck[Random.Range(0, OpponentDeck.Count)];
        OpponentDeck.Remove(card);
        return card;
    }
}