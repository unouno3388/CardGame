// GameState.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameState : IGameState
{
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

    public void ResetState(GameManager.GameMode mode)
    {
        CurrentGameMode = mode;
        PlayerHealth = 30; // 根據 GameManager.InitializeGameDefaults 調整
        OpponentHealth = 10;
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
        IsInRoom = false;
        RoomId = null;
        OpponentPlayerId = null;
         Debug.Log($"GameState: Reset complete for mode {mode}. PlayerHealth: {PlayerHealth}");
    }

    public void UpdateFromGameStartServer(ServerGameState initialState, IDataConverter converter, UIManager uiManager)
    {
        PlayerId = initialState.playerId;
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
        Debug.Log($"GameState: Updated from ServerGameState. PlayerHealth: {PlayerHealth}, IsPlayerTurn: {IsPlayerTurn}");
    }

    public void UpdateFromRoomStateServer(ServerRoomState roomState, IDataConverter converter, UIManager uiManager, string localPlayerId)
    {
        RoomId = roomState.roomId;
        IsInRoom = true; // 假設收到 RoomState 就是在房間內
        IsPlayerTurn = (roomState.currentPlayerId == localPlayerId);

        if (roomState.self != null && roomState.self.playerId == localPlayerId)
        {
            PlayerHealth = roomState.self.health;
            PlayerMana = roomState.self.mana;
            MaxMana = roomState.self.maxMana;
            PlayerHand.Clear();
            if (roomState.self.hand != null) // 假設 self 包含完整手牌
            {
                PlayerHand.AddRange(converter.ConvertServerCardsToClientCards(roomState.self.hand, uiManager));
            }
            // PlayerField 的更新通常也包含在 self 裡，如果 ServerPlayerState 有 field 欄位
        }

        if (roomState.opponent != null)
        {
            OpponentPlayerId = roomState.opponent.playerId;
            OpponentHealth = roomState.opponent.health;
            OpponentMana = roomState.opponent.mana;
            OpponentMaxMana = roomState.opponent.maxMana;
            OpponentServerHandCount = roomState.opponent.handCount;
            OpponentField.Clear();
            // 假設 opponent state 包含場地牌列表 (如果 ServerPlayerState 有 field 欄位)
            // if (roomState.opponent.field != null) {
            //     OpponentField.AddRange(converter.ConvertServerCardsToClientCards(roomState.opponent.field, uiManager));
            // }
        }
        else
        {
            OpponentPlayerId = null; // 對手可能尚未加入或已離開
            // 可以根據需要重置對手狀態
        }
        Debug.Log($"GameState: Updated from RoomStateServer. RoomId: {RoomId}, IsPlayerTurn: {IsPlayerTurn}");
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