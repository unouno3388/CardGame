using System.Collections.Generic;
// --- 輔助類別定義 (與原 GameManager 相同) ---
// 建議將這些移到單獨的 ServerModels.cs 檔案
[System.Serializable]
public class ServerGameState //
{
    public string playerId;
    public int maxHealth;
    public int playerHealth;
    public int playerMana;
    public int playerMaxMana;
    public List<ServerCard> playerHand;
    public int aiHealth;
    public int aiMana;
    public int aiMaxMana;
    public int? aiHandCount;
    public List<ServerCard> aiField;
    public bool isPlayerTurn;
    public bool gameOver;
    public string winner;
    public ServerPlayerState opponentState;
    public bool gameStarted;
    // 新增欄位以匹配 RoomState 中的 self/opponent 結構 (如果 gameStateUpdate 也用它們)
    public List<ServerCard> playerField; // 玩家的場地牌
}

[System.Serializable]
public class ServerPlayerState //
{
    public string playerId;
    public string playerName; // 伺服器 PlayerGameState.toMapForClient 中已加入
    public int health;
    public int mana;
    public int maxMana;
    
    public List<ServerCard> hand; // 通常只有自己的手牌有完整列表，對手可能是空的或null
    public int handCount;
    public int deckSize;      // 牌庫剩餘數量
    public List<ServerCard> field; // 該玩家的場地牌
}

[System.Serializable]
public class ServerCard //
{
    public string id;
    public string name;
    public int cost;
    public int attack;
    public int value;
    public string effect;
    public string cardType;
}

[System.Serializable]
public class ServerRoomState //
{
    public string roomId;
    public Dictionary<string, string> players; // <playerId, playerName>
    public bool gameStarted;
    public bool gameOver;
    public string winnerId;
    public string currentPlayerId; // 當前回合的玩家ID
    public string message;
    public ServerPlayerState self;    // 當前客戶端玩家的詳細狀態
    public ServerPlayerState opponent; // 對手的公開狀態
}
/*
// 【新增】用於接收伺服器遊戲狀態的輔助類 (需要與後端 GameMessage 中的 data 結構對應)
// 這些結構需要根據你後端實際發送的 JSON 結構來定義
[System.Serializable]
public class ServerGameState
{
    public string playerId; // 當前玩家的ID (在AI模式中) 或房間中某個玩家的ID
    public int playerHealth;
    public int playerMana;
    public int playerMaxMana;
    public List<ServerCard> playerHand;

    // AI 模式特有
    public int aiHealth;
    public int aiMana;
    public int aiMaxMana;
    public int? aiHandCount; // AI手牌數量 (可選)
    public List<ServerCard> aiField; // 【新增】接收AI場地牌
    // 通用
    public bool isPlayerTurn;
    public bool gameOver;
    public string winner; // "Player", "AI", 或 playerId

    // 房間模式下，這個頂層結構可能代表 "self" 的狀態，然後有一個嵌套的 "opponentState"
    public ServerPlayerState opponentState; // 用於房間模式中對手的公開狀態
    public bool gameStarted; // 房間模式中，標識遊戲是否已在房間內開始
}

[System.Serializable]
public class ServerPlayerState
{ // 用於房間模式中表示一個玩家的公開狀態
    public string playerId;
    public int health;
    public int mana;
    public int maxMana;
    public int handCount; // 對手手牌數量
    public List<ServerCard> hand; // 通常只有自己的手牌才有完整列表
}


[System.Serializable]
public class ServerCard
{ // 與後端 ServerCard 對應
    public string id;
    public string name;
    public int cost;
    public int attack;
    public int value;
    public string effect;
    public string cardType; // 【新增】
}

[System.Serializable]
public class ServerRoomState
{ // 用於接收房間更新
    public string roomId;
    public Dictionary<string, string> players; // <playerId, playerName>
    public bool gameStarted;
    public bool gameOver;
    public string winnerId;
    public string currentPlayerId;
    public string message; // 伺服器發來的額外訊息

    public ServerPlayerState self; // 玩家自己的詳細狀態
    public ServerPlayerState opponent; // 對手的公開狀態
}*/