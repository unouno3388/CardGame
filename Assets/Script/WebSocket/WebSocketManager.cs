using UnityEngine;
using NativeWebSocket; // 確保您已經匯入了 NativeWebSocket 套件
using System; // 【新增】為了 Action
using System.Collections.Generic; // 【新增】為了 Dictionary
using Newtonsoft.Json; // 【新增】或者使用 Unity 的 JsonUtility，但 Newtonsoft 更強大，需要匯入對應的 DLL 或透過 Package Manager 安裝
// 如果使用 JsonUtility，則 GameMessage 和其嵌套類別需要是 [System.Serializable] 且欄位都是 public

public class WebSocketManager : MonoBehaviour
{
    private WebSocket ws;
    // private const string ServerUrl = "ws://localhost:8080/game"; // 【移除】不再使用單一固定 URL

    public Action OnConnected; // 【新增】連接成功事件
    public Action<string> OnDisconnected; // 【新增】斷開連接事件 (帶原因)
    public Action<GameMessage> OnMessageReceivedEvent; // 【新增】收到訊息事件

    private bool isConnecting = false; // 【新增】防止重複連接
    private bool intentionallyClosed = false; // 【新增】標識是否為刻意關閉

    // 【新增】連接到指定的伺服器 URL
    public async void ConnectToServer(string serverUrl)
    {
        if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting) || isConnecting)
        {
            Debug.LogWarning("WebSocket is already connected or connecting.");
            return;
        }

        isConnecting = true;
        intentionallyClosed = false;
        Debug.Log($"Attempting to connect to: {serverUrl}");

        ws = new WebSocket(serverUrl);

        ws.OnOpen += () =>
        {
            isConnecting = false;
            Debug.Log("WebSocket connected!");
            MainThreadDispatcher.Instance.Enqueue(() => // 【重要】確保在主線程調用 Unity API 或事件
            {
                GameManager.Instance.OnWebSocketConnected(); // 通知 GameManager
                OnConnected?.Invoke();
            });
        };

        ws.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            // Debug.Log("Raw Message received: " + message);
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                HandleMessageFromServer(message);
            });
        };

        ws.OnError += (error) =>
        {
            isConnecting = false;
            Debug.LogError("WebSocket error: " + error);
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                GameManager.Instance.OnWebSocketDisconnected(error); // 通知 GameManager
                OnDisconnected?.Invoke(error);
            });
        };

        ws.OnClose += (code) =>
        {
            isConnecting = false;
            Debug.Log("WebSocket closed with code: " + code);
            string reason = ResolveCloseCode(code);
            if (intentionallyClosed) reason = "Intentionally closed by client.";

            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (!intentionallyClosed) // 只有在非刻意關閉時才通知 GameManager 斷線
                {
                    GameManager.Instance.OnWebSocketDisconnected(reason);
                    OnDisconnected?.Invoke(reason);
                }
            });
        };

        try
        {
            await ws.Connect();
        }
        catch (Exception e)
        {
            isConnecting = false;
            Debug.LogError("WebSocket connection failed: " + e.Message);
            MainThreadDispatcher.Instance.Enqueue(() =>
            {
                GameManager.Instance.OnWebSocketDisconnected("Connection failed: " + e.Message);
                OnDisconnected?.Invoke("Connection failed: " + e.Message);
            });
        }
    }

    private string ResolveCloseCode(WebSocketCloseCode closeCode)
    {
        switch (closeCode)
        {
            case WebSocketCloseCode.Normal: return "Normal closure";
            case WebSocketCloseCode.Away: return "Going away";
            case WebSocketCloseCode.ProtocolError: return "Protocol error";
            case WebSocketCloseCode.UnsupportedData: return "Unsupported data";
            case WebSocketCloseCode.Abnormal: return "Abnormal closure";
            //case WebSocketCloseCode.InvalidFramePayloadData: return "Invalid frame payload data";
            case WebSocketCloseCode.PolicyViolation: return "Policy violation";
            //case WebSocketCloseCode.MessageTooBig: return "Message too big";
            //case WebSocketCloseCode.MandatoryExt: return "Mandatory extension";
            //case WebSocketCloseCode.InternalServerError: return "Internal server error";
            //case WebSocketCloseCode.TLSHandshake: return "TLS handshake error";
            default: return "Unknown closure reason: " + closeCode.ToString();
        }
    }


    // 【新增】處理從伺服器收到的原始訊息字串
    private void HandleMessageFromServer(string jsonMessage)
    {
        Debug.Log("Handling message from server: " + jsonMessage);
        try
        {
            // 使用 Newtonsoft.Json 反序列化 GameMessage 本身
            GameMessage baseMessage = JsonConvert.DeserializeObject<GameMessage>(jsonMessage);

            if (baseMessage == null || string.IsNullOrEmpty(baseMessage.type))
            {
                Debug.LogError("Failed to parse base GameMessage or type is missing.");
                return;
            }

            // 根據 type 進一步處理 data 部分
            // GameManager 中定義的 ServerGameState, ServerCard 等類別需要與後端 JSON 結構匹配
            switch (baseMessage.type)
            {
                case "gameStart": // AI 模式的開始
                    ServerGameState gameStartState = JsonConvert.DeserializeObject<ServerGameState>(baseMessage.data.ToString());
                    GameManager.Instance.HandleGameStartFromServer(gameStartState);
                    break;
                case "gameStateUpdate": // AI 模式或房間模式的通用狀態更新
                    ServerGameState gameState = JsonConvert.DeserializeObject<ServerGameState>(baseMessage.data.ToString());
                    GameManager.Instance.HandleGameStateUpdateFromServer(gameState);
                    break;
                case "roomUpdate": // 房間模式的狀態更新 (可能包含遊戲開始、玩家加入等)
                    ServerRoomState roomState = JsonConvert.DeserializeObject<ServerRoomState>(baseMessage.data.ToString());
                    GameManager.Instance.HandleRoomUpdateFromServer(roomState);
                    break;
                case "aiAction": // AI 的行動
                    // aiAction 的 data 結構可能包含 actionType ("playCard", "endTurn") 和卡牌信息
                    // 例如: {"actionType": "playCard", "card": {"id":"...", "name":"..."}}
                    var aiActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(baseMessage.data.ToString());
                    if (aiActionData.TryGetValue("actionType", out object aiActionType) && aiActionType.ToString() == "playCard")
                    {
                        if (aiActionData.TryGetValue("card", out object cardObj))
                        {
                            ServerCard aiCard = JsonConvert.DeserializeObject<ServerCard>(cardObj.ToString());
                            GameManager.Instance.HandleAIPlayCard(aiCard);
                        }
                    }
                    // 如果是 "endTurn"，可能不需要額外處理卡牌，gameStateUpdate 會處理後續
                    break;
                case "opponentPlayCard": // 房間模式中，對手出牌的單獨通知 (如果後端這樣設計)
                    var opponentPlayData = JsonConvert.DeserializeObject<Dictionary<string, object>>(baseMessage.data.ToString());
                    if (opponentPlayData.TryGetValue("card", out object cardData) &&
                        opponentPlayData.TryGetValue("playerName", out object playerNameObj))
                    {
                        ServerCard playedCard = JsonConvert.DeserializeObject<ServerCard>(cardData.ToString());
                        GameManager.Instance.HandleOpponentPlayCard(playedCard, playerNameObj.ToString());
                    }
                    break;
                case "roomCreated":
                    Debug.Log($"Room created: {baseMessage.roomId}, Message: {baseMessage.message}");
                    GameManager.Instance.RoomId = baseMessage.roomId; // GameManager 也記錄一下
                    GameManager.Instance.IsInRoom = true; // 標識已在房間內
                     // UIManager 可以更新UI顯示房間ID，並隱藏創建/加入按鈕，顯示等待訊息或離開按鈕
                    if(GameManager.Instance.UIManager != null) {
                        GameManager.Instance.UIManager.UpdateRoomStatus($"房間創建成功！\n房間ID: {baseMessage.roomId}\n等待對手加入...");
                        GameManager.Instance.UIManager.SetRoomPanelActive(true); // 確保房間面板可見以顯示狀態和離開按鈕
                        GameManager.Instance.UIManager.ToggleRoomJoinCreateButtons(false); // 隱藏創建和加入按鈕
                    }
                    break;
                case "roomJoined":
                    Debug.Log($"Joined room: {baseMessage.roomId}, Message: {baseMessage.message}");
                    GameManager.Instance.RoomId = baseMessage.roomId;
                    GameManager.Instance.IsInRoom = true;
                    // UIManager 更新UI
                     if(GameManager.Instance.UIManager != null) {
                        GameManager.Instance.UIManager.UpdateRoomStatus($"成功加入房間: {baseMessage.roomId}");
                        // 伺服器的 roomUpdate 應該會緊接著發送完整的房間狀態
                    }
                    break;
                case "leftRoom": // 【新增】處理離開房間的確認
                    Debug.Log($"Left room: {baseMessage.message}");
                    GameManager.Instance.RoomId = null;
                    GameManager.Instance.IsInRoom = false;
                    if(GameManager.Instance.UIManager != null) {
                        GameManager.Instance.UIManager.UpdateRoomStatus("您已離開房間。");
                        GameManager.Instance.UIManager.SetRoomPanelActive(GameManager.Instance.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom); // 如果還在房間模式，顯示房間面板
                        GameManager.Instance.UIManager.ToggleRoomJoinCreateButtons(true); // 重新顯示創建和加入按鈕
                    }
                    break;
                case "error":
                    Debug.LogError("Server error: " + baseMessage.message);
                    if (GameManager.Instance.UIManager != null)
                    {
                        // 可以顯示一個通用的錯誤提示UI
                        GameManager.Instance.UIManager.ShowErrorPopup(baseMessage.message);
                    }
                    break;
                // 【新增或確認】處理伺服器確認玩家行動的訊息
                case "playerAction": // 假設伺服器用這個 type 來確認玩家出牌成功
                    var playerActionData = JsonConvert.DeserializeObject<Dictionary<string, object>>(baseMessage.data.ToString());
                    if (playerActionData.TryGetValue("action", out object playerActionType) && playerActionType.ToString() == "playCard" &&
                        playerActionData.TryGetValue("success", out object successObj) && (bool)successObj == true)
                    {
                        if (playerActionData.TryGetValue("cardId", out object cardIdObj))
                        {
                            string playedCardId = cardIdObj.ToString();
                            Debug.Log($"WebSocketManager: Received playerAction confirmation for cardId: {playedCardId}");
                            GameManager.Instance.HandlePlayerCardPlayConfirmed(playedCardId);
                        }
                    }
                    // 你可能還需要處理 actionType 為 "endTurn" 等其他玩家行動
                    break;
                default:
                    Debug.LogWarning("Received unhandled message type: " + baseMessage.type);
                    break;
            }
            OnMessageReceivedEvent?.Invoke(baseMessage); // 觸發通用事件
        }
        catch (Exception e)
        {
            Debug.LogError("Error handling message from server: " + e.Message + "\nOriginal message: " + jsonMessage);
        }
    }


    // 【修改】發送卡牌請求 (用於線上模式)
    public async void SendPlayCardRequest(string cardId, string roomId) // roomId 在AI模式下可能不需要，後端判斷
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            GameMessage message = new GameMessage
            {
                type = "playCard",
                cardId = cardId,
                roomId = roomId, // 如果是房間模式，帶上 roomId
                playerId = GameManager.Instance.PlayerId // 帶上自己的 PlayerId
            };
            string json = JsonConvert.SerializeObject(message);
            await ws.SendText(json);
            Debug.Log($"Sent playCard request: {json}");
        }
        else
        {
            Debug.LogWarning("WebSocket is not connected. Cannot send playCard request.");
        }
    }

    // 【修改】發送結束回合請求 (用於線上模式)
    public async void SendEndTurnRequest(string roomId) // roomId 在AI模式下可能不需要
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            GameMessage message = new GameMessage
            {
                type = "endTurn",
                roomId = roomId, // 如果是房間模式
                playerId = GameManager.Instance.PlayerId
            };
            string json = JsonConvert.SerializeObject(message);
            await ws.SendText(json);
            Debug.Log($"Sent endTurn request: {json}");
        }
        else
        {
            Debug.LogWarning("WebSocket is not connected. Cannot send endTurn request.");
        }
    }

    // 【新增】發送創建房間請求
    public async void SendCreateRoomRequest(string playerName) {
        if (ws != null && ws.State == WebSocketState.Open) {
            GameMessage message = new GameMessage {
                type = "createRoom",
                playerId = playerName // 或者 GameManager.Instance.PlayerId (如果PlayerId代表名稱或唯一標識)
            };
            string json = JsonConvert.SerializeObject(message);
            await ws.SendText(json);
            Debug.Log($"Sent createRoom request: {json}");
        } else {
            Debug.LogWarning("WebSocket is not connected. Cannot send createRoom request.");
        }
    }

    // 【新增】發送加入房間請求
    public async void SendJoinRoomRequest(string targetRoomId, string playerName) {
        if (ws != null && ws.State == WebSocketState.Open) {
            GameMessage message = new GameMessage {
                type = "joinRoom",
                roomId = targetRoomId,
                playerId = playerName // 或者 GameManager.Instance.PlayerId
            };
            string json = JsonConvert.SerializeObject(message);
            await ws.SendText(json);
            Debug.Log($"Sent joinRoom request: {json}");
        } else {
            Debug.LogWarning("WebSocket is not connected. Cannot send joinRoom request.");
        }
    }
     // 【新增】發送離開房間請求
    public async void SendLeaveRoomRequest(string currentRoomId) {
        if (ws != null && ws.State == WebSocketState.Open) {
            GameMessage message = new GameMessage {
                type = "leaveRoom",
                roomId = currentRoomId,
                playerId = GameManager.Instance.PlayerId
            };
            string json = JsonConvert.SerializeObject(message);
            await ws.SendText(json);
            Debug.Log($"Sent leaveRoom request: {json}");
        } else {
            Debug.LogWarning("WebSocket is not connected. Cannot send leaveRoom request.");
        }
    }

    public bool IsConnected() {
        return ws != null && ws.State == WebSocketState.Open;
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR // DispatchMessageQueue 在 WebGL 上是自動的
        if (ws != null)
        {
            ws.DispatchMessageQueue();
        }
        #endif
    }

    async void OnDestroy() // 【修改】使用 OnApplicationQuit 可能更合適，取決於物件生命週期
    {
        await CloseConnection();
    }

    async void OnApplicationQuit() // 【新增】確保應用程式退出時關閉連接
    {
       await CloseConnection();
    }

    public async System.Threading.Tasks.Task CloseConnection() // 【新增】公共的關閉連接方法
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            intentionallyClosed = true; // 標記為刻意關閉
            Debug.Log("Closing WebSocket connection intentionally.");
            await ws.Close();
            ws = null; // 清理引用
        }
    }
}

// 【新增】C# 版本的 GameMessage (與後端 Java GameMessage 對應)
// 確保這個類別與 GameManager 中定義的 ServerGameState, ServerCard 等輔助類別一起使用
// 通常這些輔助類別可以放在一個單獨的 Models.cs 文件中，或者直接在 GameManager/WebSocketManager 中定義
// 為了簡潔，這裡不再重複定義 ServerGameState, ServerCard, ServerRoomState, ServerPlayerState
// 假設它們已經在 GameManager.cs 的末尾定義好了，並且 WebSocketManager 可以訪問它們。

[System.Serializable] // 如果使用 JsonUtility，則需要這個，並且欄位為 public
public class GameMessage
{
    public string type;
    public string cardId;
    public string roomId;
    public string playerId; // 也可以用來傳遞 playerName
    public string message; // 用於錯誤或一般文字訊息
    // 使用 object 或 Dictionary<string, object> 來接收彈性的 data
    // Newtonsoft.Json 可以很好地處理 object 到具體類型的反序列化 (如 JObject 轉 Dictionary 或特定類)
    public object data;
}