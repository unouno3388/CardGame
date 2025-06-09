// GameOverHandler.cs
using UnityEngine;
using System.Collections; // For Coroutine
using System.Threading.Tasks; // For Task.Run in original GameManager

public class GameOverHandler : IGameOverHandler
{
    private GameManager _gameManager;
    private MenuManager _menuManager;
    private UIManager _uiManager;
    private CardAnimationManager _cardAnimationManager;
    private IGameState _gameState;

    private bool _isGameOverSequenceRunning = false;
    private string _pendingGameOverMessage = "";
    private bool _pendingPlayerWon = false;
    private Coroutine _gameOverDisplayCoroutine = null;

    public bool IsGameOverSequenceRunning => _isGameOverSequenceRunning;
    const string Win_Message = "You Win!";
    const string Lose_Message = "You Lose!";
    public void InitializeDependencies(GameManager gameManager, MenuManager menuManager, UIManager uiManager, CardAnimationManager cardAnimationManager, IGameState gameState)
    {
        _gameManager = gameManager;
        _menuManager = menuManager;
        _uiManager = uiManager;
        _cardAnimationManager = cardAnimationManager;
        _gameState = gameState;
    }

    public void ResetGameOverState()
    {
        _isGameOverSequenceRunning = false;
        if (_gameOverDisplayCoroutine != null)
        {
            _gameManager.StopCoroutine(_gameOverDisplayCoroutine);
            _gameOverDisplayCoroutine = null;
        }
        _pendingGameOverMessage = "";
        _pendingPlayerWon = false;
        Time.timeScale = 1; // 確保時間恢復
    }

    public void CheckOfflineGameOver()
    {
        if (_gameState.CurrentGameMode != GameManager.GameMode.OfflineSinglePlayer || _isGameOverSequenceRunning)
        {
            return;
        }
        Debug.Log("GameOverHandler: Checking Offline Game Over.");
        bool gameEnded = false;
        bool playerWon = false;
        string message = "";

        if (_gameState.PlayerHealth <= 0)
        {
            playerWon = false;
            message = Lose_Message;
            gameEnded = true;
        }
        else if (_gameState.OpponentHealth <= 0)
        {
            playerWon = true;
            message = Win_Message;
            gameEnded = true;
        }
        // 可以加入牌庫抽乾的判斷
        else if (_gameState.PlayerDeck.Count == 0 && _gameState.PlayerHand.Count == 0) // 假設抽完牌且手牌用完算輸
        {
            // playerWon = false;
            // message = "牌庫耗盡，你輸了!";
            // gameEnded = true;
        }


        if (gameEnded)
        {
            _isGameOverSequenceRunning = true;
            _pendingGameOverMessage = message;
            _pendingPlayerWon = playerWon;
            if (_gameOverDisplayCoroutine != null) _gameManager.StopCoroutine(_gameOverDisplayCoroutine);
            _gameOverDisplayCoroutine = _gameManager.StartCoroutine(ShowGameOverScreenAfterAnimationsInternal());
        }
    }

    public void ProcessGameOverUpdate(ServerGameState updatedState, string localPlayerId)
    {
        if (!updatedState.gameOver || _isGameOverSequenceRunning) return;

        _isGameOverSequenceRunning = true;
        _pendingPlayerWon = false; // 預設
        _pendingGameOverMessage = "遊戲結束!"; // 預設

        if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI)
        {
            _pendingPlayerWon = (updatedState.winner == "Player"); // 後端定義 "Player" 為玩家贏
            _pendingGameOverMessage = _pendingPlayerWon ? Win_Message : Lose_Message;
            if (string.IsNullOrEmpty(updatedState.winner) && updatedState.gameStarted)
            {
                 _pendingGameOverMessage = "遊戲結束 (AI模式)";
            }
        }
        else if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            // 在房間模式，GameManager 會傳入自己的 PlayerId (localPlayerId)
            _pendingPlayerWon = (updatedState.winner == localPlayerId);
            _pendingGameOverMessage = _pendingPlayerWon ? Win_Message : Lose_Message;
            if (string.IsNullOrEmpty(updatedState.winner) && updatedState.gameStarted)
            {
                _pendingGameOverMessage = "遊戲結束 (房間模式)";
            }
        }
        // 其他模式的邏輯...

        Debug.Log($"GameOverHandler: Processed ServerGameState. Winner: {updatedState.winner}. Message: {_pendingGameOverMessage}");
        if (_gameOverDisplayCoroutine != null) _gameManager.StopCoroutine(_gameOverDisplayCoroutine);
        _gameOverDisplayCoroutine = _gameManager.StartCoroutine(ShowGameOverScreenAfterAnimationsInternal());
    }

    public void ProcessRoomGameOverUpdate(ServerRoomState roomState, string localPlayerId)
    {
        if (!roomState.gameOver || _isGameOverSequenceRunning) return;

        _isGameOverSequenceRunning = true;
        _pendingPlayerWon = (roomState.winnerId == localPlayerId);
        _pendingGameOverMessage = _pendingPlayerWon ? Win_Message : Lose_Message;
        if (string.IsNullOrEmpty(roomState.winnerId) && roomState.gameStarted)
        {
            _pendingGameOverMessage = "遊戲結束 (房間模式)";
        }

        Debug.Log($"GameOverHandler: Processed ServerRoomState. WinnerID: {roomState.winnerId}. Message: {_pendingGameOverMessage}");
        if (_gameOverDisplayCoroutine != null) _gameManager.StopCoroutine(_gameOverDisplayCoroutine);
        _gameOverDisplayCoroutine = _gameManager.StartCoroutine(ShowGameOverScreenAfterAnimationsInternal());
    }


    public Coroutine ShowGameOverScreenAfterAnimations(string message, bool playerWon)
    {
        // 這個公開方法主要是為了相容 GameManager 之前直接調用協程的模式
        // 實際上，內部的 _pendingGameOverMessage 和 _pendingPlayerWon 應該已經被設定好了
        // 但如果外部強制指定，也可以用
        _pendingGameOverMessage = message;
        _pendingPlayerWon = playerWon;
        if (!_isGameOverSequenceRunning) _isGameOverSequenceRunning = true; // 確保標記被設置

        if (_gameOverDisplayCoroutine != null) _gameManager.StopCoroutine(_gameOverDisplayCoroutine);
        _gameOverDisplayCoroutine = _gameManager.StartCoroutine(ShowGameOverScreenAfterAnimationsInternal());
        return _gameOverDisplayCoroutine;
    }

    private IEnumerator ShowGameOverScreenAfterAnimationsInternal()
    {
        Debug.Log("GameOverHandler: Coroutine ShowGameOverScreenAfterAnimationsInternal started.");
        yield return new WaitForSeconds(0.2f); // 短暫延遲讓UI動畫開始

        if (_cardAnimationManager != null)
        {
            float waitStartTime = Time.realtimeSinceStartup;
            float maxWaitTime = 7f; // 增加等待時間
            while (_cardAnimationManager.IsAnimationPlaying())
            {
                if (Time.realtimeSinceStartup - waitStartTime > maxWaitTime)
                {
                    Debug.LogWarning("GameOverHandler: Wait for card animations timed out.");
                    break;
                }
                Debug.Log("GameOverHandler: Waiting for card animations to complete...");
                yield return null;
            }
            Debug.Log("GameOverHandler: Card animations complete or timed out.");
        }

        if (!_isGameOverSequenceRunning) // 再次檢查，以防狀態改變
        {
            Debug.Log("GameOverHandler: Game over state was reset during animation wait. Not showing GameOver screen.");
            yield break;
        }

        Debug.Log($"GameOverHandler: Preparing to show GameOver screen: '{_pendingGameOverMessage}', PlayerWon: {_pendingPlayerWon}");
        if (_menuManager != null && _menuManager.gameOverManager != null)
        {
            _menuManager.gameOverManager.ShowGameOverPanel(_pendingGameOverMessage, _pendingPlayerWon);
        }
        else if (_uiManager != null) // 備用方案
        {
            _uiManager.ShowGameOver(_pendingGameOverMessage);
            Debug.LogWarning("GameOverHandler: Used UIManager.ShowGameOver as MenuManager or its GameOverManager is unavailable.");
        }
        else
        {
            Debug.LogError("GameOverHandler: Both MenuManager and UIManager are unavailable. Cannot show GameOver screen.");
        }

        Time.timeScale = 0;
        Debug.Log("GameOverHandler: Game paused.");

        if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI ||
            _gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            if (_gameManager.WebSocketManager != null && _gameManager.WebSocketManager.IsConnected())
            {
                Debug.Log("GameOverHandler: Closing WebSocket connection...");
                // GameManager 中的 wsManager.CloseConnection() 是 async Task
                // 在協程中用 Task.Run 啟動它
                Task.Run(async () => {
                    if (_gameManager.WebSocketManager != null) // 再次檢查以防萬一
                    {
                        await _gameManager.WebSocketManager.CloseConnection();
                        Debug.Log("GameOverHandler: WebSocket connection closed via Task.Run.");
                    }
                });
            }
        }
        // 協程結束後，GameManager 中的 _isGameOverSequenceRunning 應該由 GameManager 自己管理或通過回調重置
        // 或者，我們可以在這裡重置它，如果這個協程全權負責這個標記
        // _isGameOverSequenceRunning = false; // 移到 GameManager 的 HandleGameStateUpdateFromServer/HandleRoomUpdateFromServer 的 finally 或 else if
        // _gameOverDisplayCoroutine = null;
    }
}