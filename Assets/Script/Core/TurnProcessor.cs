// TurnProcessor.cs
using UnityEngine;

public class TurnProcessor : ITurnProcessor
{
    private GameManager _gameManager;
    private IGameState _gameState;
    private AIManager _aiManager;
    private WebSocketManager _wsManager;
    private UIManager _uiManager;

    public void InitializeDependencies(GameManager gameManager, IGameState gameState, AIManager aiManager, WebSocketManager wsManager, UIManager uiManager)
    {
        _gameManager = gameManager;
        _gameState = gameState;
        _aiManager = aiManager;
        _wsManager = wsManager;
        _uiManager = uiManager;
    }

    public void EndPlayerTurn()
    {
        // 在線上模式，回合結束權由伺服器控制，但玩家可以發送請求
        if (!_gameState.IsPlayerTurn &&
            _gameState.CurrentGameMode != GameManager.GameMode.OnlineSinglePlayerAI &&
            _gameState.CurrentGameMode != GameManager.GameMode.OnlineMultiplayerRoom)
        {
            Debug.LogWarning("TurnProcessor: Not player's turn to end or waiting for server response!");
            return;
        }

        Debug.Log("TurnProcessor: Player ends turn button pressed.");
        if (_gameState.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
        {
            _gameState.IsPlayerTurn = false;
            _gameState.OpponentMaxMana = Mathf.Min(_gameState.OpponentMaxMana + 1, 10);
            _gameState.OpponentMana = _gameState.OpponentMaxMana;

            // 讓 DeckOperator 處理抽牌
            _gameManager.DeckOperator?.DrawCardLocal(false, 1); // AI 抽牌

            if (_uiManager != null) _uiManager.UpdateUI();
            if (_aiManager != null) _gameManager.StartCoroutine(_aiManager.PlayAITurn()); // GameManager 啟動協程
        }
        else if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI ||
                 _gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            if (_wsManager != null && _wsManager.IsConnected())
            {
                _wsManager.SendEndTurnRequest(_gameState.RoomId);
                Debug.Log("TurnProcessor: Sent EndTurn request to server.");
            }
            else
            {
                Debug.LogWarning("TurnProcessor: WebSocketManager not available or not connected. Cannot send EndTurn request.");
            }
            // 客戶端不應立即改變 _gameState.IsPlayerTurn，等待伺服器確認
        }
    }

    public void EndAITurnOffline()
    {
        if (_gameState.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
        {
            Debug.Log("TurnProcessor: AI ends turn (offline).");
            _gameState.IsPlayerTurn = true;
            //_gameState.MaxMana = Mathf.Min(_gameState.MaxMana + 1, 10);
            _gameState.PlayerMana += 2;
            _gameState.PlayerMana = Mathf.Min(_gameState.PlayerMana, _gameState.MaxMana); // 確保不超過最大法力
            _gameManager.DeckOperator?.DrawCardLocal(true, 1); // 玩家抽牌

            if (_uiManager != null) _uiManager.UpdateUI();
        }
        else
        {
            Debug.LogWarning("TurnProcessor: EndAITurnOffline called in non-offline mode.");
        }
    }
}