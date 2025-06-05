// Interfaces.cs
using System.Collections.Generic;
using UnityEngine; // 為了 Coroutine

/// <summary>
/// 定義遊戲狀態的屬性和基本操作。
/// </summary>
public interface IGameState
{
    // 屬性
    int MaxHealth { get; set; }
    int PlayerHealth { get; set; }
    int OpponentHealth { get; set; }
    int PlayerMana { get; set; }
    int MaxMana { get; set; }
    int OpponentMana { get; set; }
    int OpponentMaxMana { get; set; }
    bool IsPlayerTurn { get; set; }
    int OpponentServerHandCount { get; set; }
    List<Card> PlayerHand { get; }
    List<Card> OpponentHand { get; } // 離線模式下是實際卡牌，線上 AI 模式下可能是卡背的佔位符
    List<Card> PlayerField { get; }
    List<Card> OpponentField { get; }
    List<Card> PlayerDeck { get; }
    List<Card> OpponentDeck { get; }
    string PlayerId { get; set; }
    string RoomId { get; set; }
    bool IsInRoom { get; set; }
    string OpponentPlayerId { get; set; }
    GameManager.GameMode CurrentGameMode { get; set; }
     bool GameStarted { get; set; }
    // 方法
    void ResetState(GameManager.GameMode mode);
    void UpdateFromGameStartServer(ServerGameState initialState, IDataConverter converter, UIManager uiManager);
    void UpdateFromServer(ServerGameState updatedState, IDataConverter converter, UIManager uiManager);
    void UpdateFromRoomStateServer(ServerRoomState roomState, IDataConverter converter, UIManager uiManager, string localPlayerId);
    void AddCardToPlayerHand(Card card);
    void RemoveCardFromPlayerHand(string cardId);
    void AddCardToPlayerDeck(Card card);
    void AddCardToOpponentDeck(Card card);
    void AddCardToPlayerField(Card card);
    void AddCardToOpponentField(Card card);
    Card GetCardFromPlayerDeck();
    Card GetCardFromOpponentDeck();
}

/// <summary>
/// 定義數據轉換操作。
/// </summary>
public interface IDataConverter
{
    List<Card> ConvertServerCardsToClientCards(List<ServerCard> serverCards, UIManager uiManager);
    Card ConvertServerCardToClientCard(ServerCard serverCard, UIManager uiManager);
}

/// <summary>
/// 定義遊戲結束處理邏輯。
/// </summary>
public interface IGameOverHandler
{
    bool IsGameOverSequenceRunning { get; }
    void InitializeDependencies(GameManager gameManager, MenuManager menuManager, UIManager uiManager, CardAnimationManager cardAnimationManager, IGameState gameState);
    void CheckOfflineGameOver(); // 檢查離線模式下的遊戲結束條件
    void ProcessGameOverUpdate(ServerGameState updatedState, string localPlayerId); // 處理來自 ServerGameState 的遊戲結束更新
    void ProcessRoomGameOverUpdate(ServerRoomState roomState, string localPlayerId); // 處理來自 ServerRoomState 的遊戲結束更新
    Coroutine ShowGameOverScreenAfterAnimations(string message, bool playerWon); // 在動畫播放完畢後顯示遊戲結束畫面
    void ResetGameOverState(); // 重置遊戲結束狀態
}

/// <summary>
/// 定義回合處理邏輯。
/// </summary>
public interface ITurnProcessor
{
    void InitializeDependencies(GameManager gameManager, IGameState gameState, AIManager aiManager, WebSocketManager wsManager, UIManager uiManager);
    void EndPlayerTurn(); // 結束玩家回合
    void EndAITurnOffline(); // 結束離線 AI 回合
}

/// <summary>
/// 定義牌組操作。
/// </summary>
public interface IDeckOperator
{
    void InitializeDependencies(IGameState gameState, UIManager uiManager, IGameOverHandler gameOverHandler);
    void DrawCardLocal(bool isPlayer, int count); // 本地抽牌 (主要用於離線或視覺表現)
    List<Card> GenerateRandomDeck(int count); // 生成隨機牌組 (離線用)
}

/// <summary>
/// 定義房間相關操作的處理。
/// </summary>
public interface IRoomActionHandler
{
    void InitializeDependencies(IGameState gameState, WebSocketManager wsManager, UIManager uiManager, GameManager gameManager);
    void RequestCreateRoom(string playerName);
    void RequestJoinRoom(string targetRoomId, string playerName);
    void RequestLeaveRoom();
    void HandleRoomCreatedResponse(GameMessage message); // 注意：GameMessage 來自 WebSocketManager
    void HandleRoomJoinedResponse(GameMessage message);
    void HandleLeftRoomResponse(GameMessage message);
    void HandleErrorResponse(GameMessage message);
}