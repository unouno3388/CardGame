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
            if(_uiManager != null) _uiManager.UpdateRoomStatus("正在創建房間...");
        }
        else
        {
            Debug.LogError("RoomActionHandler: Cannot create room. Invalid mode or not connected.");
            if(_uiManager != null) _uiManager.ShowErrorPopup("無法創建房間：模式錯誤或未連接。");
        }
    }

    public void RequestJoinRoom(string targetRoomId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(targetRoomId))
        {
            if(_uiManager != null) _uiManager.UpdateRoomStatus("錯誤：房間ID不能為空！");
            return;
        }
        if (_gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom && _wsManager != null && _wsManager.IsConnected())
        {
            _wsManager.SendJoinRoomRequest(targetRoomId, playerName);
             if(_uiManager != null) _uiManager.UpdateRoomStatus($"正在加入房間 {targetRoomId}...");
        }
        else
        {
            Debug.LogError("RoomActionHandler: Cannot join room. Invalid mode or not connected.");
             if(_uiManager != null) _uiManager.ShowErrorPopup("無法加入房間：模式錯誤或未連接。");
        }
    }

    public void RequestLeaveRoom()
    {
        if (_gameState.IsInRoom && _wsManager != null && _wsManager.IsConnected())
        {
            _wsManager.SendLeaveRoomRequest(_gameState.RoomId);
             if(_uiManager != null) _uiManager.UpdateRoomStatus("正在離開房間...");
        }
        else
        {
             Debug.LogWarning("RoomActionHandler: Cannot leave room. Not in a room or not connected.");
             if(_uiManager != null && _gameState.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom) _uiManager.UpdateRoomStatus("您不在房間內或未連接。");
        }
    }

    public void HandleRoomCreatedResponse(GameMessage message)
    {
        // GameMessage 的 roomId 和 message 欄位是在頂層的
        Debug.Log($"RoomActionHandler: Room created response. RoomID: {message.roomId}, Message: {message.message}");
        _gameState.RoomId = message.roomId;
        _gameState.IsInRoom = true;
        // PlayerId 通常是發送創建請求時就有的，或者伺服器可以在這裡確認或分配
        // _gameState.PlayerId = message.playerId; // 如果伺服器在 roomCreated 時也返回 playerId

        if (_uiManager != null)
        {
            _uiManager.UpdateRoomStatus($"房間創建成功！\n房間ID: {message.roomId}\n等待對手加入...");
            _uiManager.SetRoomPanelActive(true); // 確保房間面板可見
            _uiManager.ToggleRoomJoinCreateButtons(false); // 隱藏創建/加入按鈕
            _uiManager.leaveRoomButton?.gameObject.SetActive(true); // 顯示離開按鈕
        }
    }

    public void HandleRoomJoinedResponse(GameMessage message)
    {
        Debug.Log($"RoomActionHandler: Joined room response. RoomID: {message.roomId}, Message: {message.message}");
        _gameState.RoomId = message.roomId;
        _gameState.IsInRoom = true;
        // _gameState.PlayerId = message.playerId; // 如果伺服器在 roomJoined 時也返回 playerId

        if (_uiManager != null)
        {
            _uiManager.UpdateRoomStatus($"成功加入房間: {message.roomId}\n{message.message}");
            _uiManager.SetRoomPanelActive(true);
            _uiManager.ToggleRoomJoinCreateButtons(false);
            _uiManager.leaveRoomButton?.gameObject.SetActive(true);
        }
        // 完整的房間狀態 (包括對手) 通常會通過 roomUpdate 消息更新
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