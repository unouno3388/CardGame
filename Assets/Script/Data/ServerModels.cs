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
    public int maxHealth;
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
