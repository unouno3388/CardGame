using UnityEngine;
using NativeWebSocket;
using System.Threading.Tasks;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket ws;

    void Start()
    {
        if (GameManager.Instance.IsOnline)
        {
            Connect();
        }
    }

    public async void Connect()
    {
        ws = new WebSocket("ws://localhost:8080/game");

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
        };

        ws.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed: " + code);
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
            Debug.LogWarning("WebSocket is not connected!");
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
            Debug.LogWarning("WebSocket is not connected!");
        }
    }

    void OnMessageReceived(string message)
    {
        var data = JsonUtility.FromJson<GameMessage>(message);
        if (data.type == "playCard")
        {
            if (int.TryParse(data.cardId, out int cardId))
            {
                Card card = GameManager.Instance.opponentDeck.Find(c => c.id == cardId);
                if (card != null)
                {
                    GameManager.Instance.ReceivePlayCard(card);
                }
                else
                {
                    Debug.LogError($"Received card ID {data.cardId} not found in opponentDeck!");
                }
            }
            else
            {
                Debug.LogError($"Invalid cardId format: {data.cardId}");
            }
        }
        else if (data.type == "endTurn")
        {
            GameManager.Instance.ReceiveEndTurn();
        }
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