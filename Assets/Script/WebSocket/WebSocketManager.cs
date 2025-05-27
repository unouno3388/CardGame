using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket ws;
    private const string ServerUrl = "ws://localhost:8080/game"; // 將URL設為常量

    // Connect 方法現在是 public，由 GameManager 調用
    public async void Connect()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            Debug.Log("WebSocket is already connected.");
            return;
        }

        ws = new WebSocket(ServerUrl);

        ws.OnOpen += () =>
        {
            Debug.Log("WebSocket connected!");
        };

        ws.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            OnMessageReceived(message);
        };

        ws.OnError += (error) =>
        {
            Debug.LogError("WebSocket error: " + error);
            // 可以添加重連邏輯或錯誤提示
        };

        ws.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed: " + code);
            // 可以處理斷線後的行為
        };

        try
        {
            await ws.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError("WebSocket connection failed: " + e.Message);
        }
    }

    public async void SendPlayCard(Card card)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            GameMessage message = new GameMessage { type = "playCard", cardId = card.id.ToString() };
            string json = JsonUtility.ToJson(message);
            await ws.SendText(json);
            Debug.Log($"Sent playCard: {json}");
        }
        else
        {
            Debug.LogWarning("WebSocket is not connected. Cannot send playCard message.");
        }
    }

    public async void SendEndTurn()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            GameMessage message = new GameMessage { type = "endTurn", cardId = "" };
            string json = JsonUtility.ToJson(message);
            await ws.SendText(json);
            Debug.Log("Sent endTurn");
        }
        else
        {
            Debug.LogWarning("WebSocket is not connected. Cannot send endTurn message.");
        }
    }

    void OnMessageReceived(string message)
    {
        // 在主線程中處理接收到的消息，避免線程問題
        MainThreadDispatcher.Instance.Enqueue(() =>
        {
            var data = JsonUtility.FromJson<GameMessage>(message);
            if (data.type == "playCard")
            {
                if (int.TryParse(data.cardId, out int cardId))
                {
                    // 找到對應的卡牌數據
                    // 這裡假設對手的牌庫是同步的，或者我們可以從伺服器獲得完整的卡牌數據
                    // 為了簡化，我們從 GameManager 的 opponentDeck 中查找
                    Card card = GameManager.Instance.opponentDeck.Find(c => c.id == cardId);
                    if (card != null)
                    {
                        GameManager.Instance.ReceivePlayCard(card); // 將處理交給 GameManager
                    }
                    else
                    {
                        Debug.LogError($"Received card ID {data.cardId} not found in opponentDeck!");
                        // 更嚴謹的處理應是請求伺服器同步數據或宣告錯誤
                    }
                }
                else
                {
                    Debug.LogError($"Invalid cardId format: {data.cardId}");
                }
            }
            else if (data.type == "endTurn")
            {
                GameManager.Instance.ReceiveEndTurn(); // 將處理交給 GameManager
            }
        });
    }

    void Update()
    {
        if (ws != null)
        {
            ws.DispatchMessageQueue();
        }
    }

    async void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.Close();
        }
    }
}

[System.Serializable]
public class GameMessage
{
    public string type; // 例如 "playCard", "endTurn"
    public string cardId;
    // 可添加更多字段
}