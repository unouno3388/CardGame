// RoomActionHandler.cs
using UnityEngine;
using Newtonsoft.Json; // 用於反序列化 data 部分

public class RoomActionHandler : IRoomActionHandler
{
    private IGameState _gameState;
    private WebSocketManager _wsManager;
    private UIManager _uiManager;
    private GameManager _gameManager; // 用於訪問 PlayerId (如果不由 GameState 直接提供)

    public void InitializeDependencies(IGameState gameState, WebSocketManager wsManager, UIManager uiManager, GameManager gameManager)
    {
        _gameState = gameState;
        _wsManager = wsManager;
        _uiManager = uiManager;
        _gameManager = gameManager;
    }

    public void RequestCreateRoom(string playerName)
    {
        if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && _wsManager != null && _wsManager.IsConnected())
        {
            _wsManager.SendCreateRoomRequest(playerName);
            if (_uiManager != null) _uiManager.UpdateRoomStatus("正在創建房間...");
        }
        else
        {
            Debug.LogError("RoomActionHandler: Cannot create room. Invalid mode or not connected.");
            if (_uiManager != null) _uiManager.ShowErrorPopup("無法創建房間：模式錯誤或未連接。");
        }
    }

    public void RequestJoinRoom(string targetRoomId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(targetRoomId))
        {
            if (_uiManager != null) _uiManager.UpdateRoomStatus("錯誤：房間ID不能為空！");
            return;
        }
        if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && _wsManager != null && _wsManager.IsConnected())
        {
            _wsManager.SendJoinRoomRequest(targetRoomId, playerName);
            if (_uiManager != null) _uiManager.UpdateRoomStatus($"正在加入房間 {targetRoomId}...");
        }
        else
        {
            Debug.LogError("RoomActionHandler: Cannot join room. Invalid mode or not connected.");
            if (_uiManager != null) _uiManager.ShowErrorPopup("無法加入房間：模式錯誤或未連接。");
        }
    }

    public void RequestLeaveRoom()
    {
        if (_gameState.IsInRoom && _wsManager != null && _wsManager.IsConnected())
        {
            _wsManager.SendLeaveRoomRequest(_gameState.RoomId);
            if (_uiManager != null) _uiManager.UpdateRoomStatus("正在離開房間...");
        }
        else
        {
            Debug.LogWarning("RoomActionHandler: Cannot leave room. Not in a room or not connected.");
            if (_uiManager != null && _gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom) _uiManager.UpdateRoomStatus("您不在房間內或未連接。");
        }
    }

    // In RoomActionHandler.cs
    public void HandleRoomCreatedResponse(GameMessage message)
    {
        Debug.Log("RoomID: " + message.roomId);
        Debug.Log("PlayerID: " + message.playerId);
        Debug.Log($"RoomActionHandler: Room created response. RoomID: {message.roomId}, PlayerID: {message.playerId}, Server Message: {message.message}");
        // message.data 來自伺服器 roomCreatedMsg.setData(Map.of("message", "房間創建成功！房間號：" + newRoom.getId()))
        // message.roomId 和 message.playerId 來自 GameMessage 的頂層字段

        _gameState.RoomId = message.roomId; // 設定房間ID
        _gameState.IsInRoom = true;        // 標記玩家在房間內
        if (!string.IsNullOrEmpty(message.playerId)) // 如果伺服器在roomCreated時也返回了playerId，更新它
        {
            _gameState.PlayerId = message.playerId;
        }

        if (_uiManager != null)
        {
            string statusMessage = $"房間創建成功！\n房間ID: {message.roomId}";
            if (message.data != null)
            {
                var dataDict = message.data as Newtonsoft.Json.Linq.JObject;
                if (dataDict != null)
                {
                    // 嘗試從 message.data 中獲取更詳細的 "message"
                    statusMessage = dataDict.Value<string>("message") ?? statusMessage;
                    // 如果伺服器還在 data 中發送了 playerName，也可以在這裡處理
                    // string playerNameFromServer = dataDict.Value<string>("playerName");
                    // if(!string.IsNullOrEmpty(playerNameFromServer)) { /* 更新UI或GameState中的玩家名 */ }
                }
            }
            // 遊戲尚未開始，所以這裡應該顯示房間面板，並提示等待對手
            _uiManager.UpdateRoomStatus(statusMessage + "\n等待對手加入...");
            _uiManager.SetRoomPanelActive(true);         // 確保房間面板是激活的
            _uiManager.ToggleRoomJoinCreateButtons(false); // 隱藏創建/加入按鈕
            if (_uiManager.leaveRoomButton != null) _uiManager.leaveRoomButton.gameObject.SetActive(true); // 顯示離開按鈕
        }
    }

    public void HandleRoomJoinedResponse(GameMessage message)
    {
        Debug.Log($"RoomActionHandler: Joined room response. RoomID: {message.roomId}, PlayerID: {message.playerId}, Server Message: {message.message}");
        // GameMessage message.data 包含 {"playerName":"Player2936","message":"成功加入房間！"}
        // message.roomId 和 message.playerId 來自 GameMessage 的頂層字段

        // 確保 GameState 中的 IsInRoom 和 RoomId 被設置
        _gameState.RoomId = message.roomId; // 來自頂層 GameMessage.roomId
        _gameState.IsInRoom = true;
        // PlayerId 應該由 roomUpdate 中的 self.playerId 來設定更可靠，
        // 但如果這裡的 message.playerId 是準確的，也可以用來輔助確認
        // if (!string.IsNullOrEmpty(message.playerId) && string.IsNullOrEmpty(_gameState.PlayerId)) {
        // _gameState.PlayerId = message.playerId;
        // }


        if (_uiManager != null)
        {
            // 更新狀態文本
            string serverMsgContent = message.message; // 這是 "成功加入房間！"
            if (message.data != null)
            { // 從 message.data 中獲取更詳細的內容
                var dataDict = message.data as Newtonsoft.Json.Linq.JObject; // 假設 message.data 是 JObject
                if (dataDict != null)
                {
                    serverMsgContent = dataDict.Value<string>("message") ?? serverMsgContent;
                }
            }
            _uiManager.UpdateRoomStatus($"成功加入房間: {message.roomId}\n{serverMsgContent}");

            // 只有當遊戲尚未開始時，才考慮激活房間面板。
            // 遊戲是否開始的權威信息來自 roomUpdate。
            // 此處可以不主動切換主面板，讓 roomUpdate 來決定。
            // 或者，如果確定遊戲還未開始，可以激活房間面板：
            if (!_gameState.GameStarted) // 檢查 GameState 中的 GameStarted 狀態
            {
                _uiManager.SetRoomPanelActive(true);
                _uiManager.ToggleRoomJoinCreateButtons(false); // 已加入，隱藏創建/加入
                if (_uiManager.leaveRoomButton != null) _uiManager.leaveRoomButton.gameObject.SetActive(true);
            }
            else
            {
                // 如果遊戲已經開始 (可能是通過之前的 roomUpdate 得知)，則不應再顯示 roomPanel
                // UIManager.ShowGameScreen() 應該已經被 roomUpdate 觸發了
                Debug.Log("RoomActionHandler (Joined): Game has already started, not activating room panel.");
            }
        }
    }

    public void HandleLeftRoomResponse(GameMessage message)
    {
        Debug.Log($"RoomActionHandler: Left room response. Message: {message.message}");
        _gameState.RoomId = null;
        _gameState.IsInRoom = false;
        _gameState.OpponentPlayerId = null; // 清除對手信息

        if (_uiManager != null)
        {
            _uiManager.UpdateRoomStatus("您已離開房間。");
            // 根據是否仍在房間模式決定是否顯示房間面板
            _uiManager.SetRoomPanelActive(_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom);
            _uiManager.ToggleRoomJoinCreateButtons(true); // 顯示創建/加入按鈕
            _uiManager.leaveRoomButton?.gameObject.SetActive(false); // 隱藏離開按鈕
        }
    }

    public void HandleErrorResponse(GameMessage message)
    {
        Debug.LogError("RoomActionHandler: Server error response: " + message.message);
        if (_uiManager != null)
        {
            _uiManager.ShowErrorPopup("伺服器錯誤:\n" + message.message);
            // 根據錯誤類型，可能需要重置UI狀態，例如重新顯示創建/加入按鈕
            if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && !_gameState.IsInRoom)
            {
                _uiManager.ToggleRoomJoinCreateButtons(true);
                _uiManager.UpdateRoomStatus("發生錯誤，請重試。");
            }
        }
    }
}