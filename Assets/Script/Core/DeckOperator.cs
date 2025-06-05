// DeckOperator.cs
using System.Collections.Generic;
using UnityEngine;

public class DeckOperator : IDeckOperator
{
    private IGameState _gameState;
    private UIManager _uiManager; // 可能用於抽牌動畫觸發或UI更新
    private IGameOverHandler _gameOverHandler; // 用於檢查牌庫抽乾導致的遊戲結束

    public void InitializeDependencies(IGameState gameState, UIManager uiManager, IGameOverHandler gameOverHandler)
    {
        _gameState = gameState;
        _uiManager = uiManager;
        _gameOverHandler = gameOverHandler;
    }

    public void DrawCardLocal(bool isPlayer, int count)
    {
        List<Card> deck = isPlayer ? _gameState.PlayerDeck : _gameState.OpponentDeck;
        // List<Card> hand = isPlayer ? _gameState.PlayerHand : _gameState.OpponentHand; // GameState 內部管理 Hand

        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                Debug.LogWarning($"DeckOperator: {(isPlayer ? "Player" : "Opponent")}'s deck is empty! Cannot draw.");
                _gameOverHandler?.CheckOfflineGameOver(); // 牌庫抽乾可能導致遊戲結束 (離線模式)
                return;
            }

            Card cardToDraw = isPlayer ? _gameState.GetCardFromPlayerDeck() : _gameState.GetCardFromOpponentDeck();
            if (cardToDraw != null)
            {
                if (isPlayer) _gameState.AddCardToPlayerHand(cardToDraw);
                else _gameState.OpponentHand.Add(cardToDraw); // 離線 AI 的手牌直接加入
                // Debug.Log($"DeckOperator: Drew card {cardToDraw.name} to {(isPlayer ? "player" : "opponent")}.");
            }
        }
        // UI 更新應由 GameManager 或 TurnProcessor 在適當時機觸發
    }

    public List<Card> GenerateRandomDeck(int count)
    {
        List<Card> deck = new List<Card>();
        string[] cardNames = { "Fireball", "Ice Blast", "Thunder Strike", "Heal Wave", "Shadow Bolt", "Light Heal", "Flame Slash", "Frost Shield" };
        string[] effects = { "Deal", "Heal" };
        string[] cardTypes = { "Spell", /*"Minion" */}; // 假設有這些類型

        for (int i = 0; i < count; i++)
        {
            string name = cardNames[Random.Range(0, cardNames.Length)];
            string effectBase = effects[Random.Range(0, effects.Length)];
            int cost = Random.Range(1, 6);
            int attack = (effectBase == "Deal" || cardTypes[Random.Range(0, cardTypes.Length)] == "Minion") ? Random.Range(1, 6) : 0;
            int value = (effectBase == "Heal" || (cardTypes[Random.Range(0, cardTypes.Length)] == "Minion" && Random.value > 0.5f)) ? Random.Range(1, 6) : 0; // 假設隨機 Minion 也可能有 value (如生命值)
            string cardType = cardTypes[Random.Range(0, cardTypes.Length)];
            string fullEffect = $"{effectBase} { (effectBase == "Deal" ? attack : value) } {(effectBase == "Deal" ? "damage" : "health")}";
            if (cardType == "Minion") fullEffect = $"Summon a {attack}/{value} minion.";


            deck.Add(new Card
            {
                id = System.Guid.NewGuid().ToString(), // 使用 GUID 確保唯一性
                name = $"{name} #{i + 1}",
                cost = cost,
                attack = attack,
                value = value, // 對於 Minion 可能是生命值
                effect = fullEffect,
                cardType = cardType,
                // sprite = null // Sprite 由 DataConverter 或 UIManager 處理
            });
        }
        return deck;
    }
}