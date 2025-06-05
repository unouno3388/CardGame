using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardPlayService : MonoBehaviour
{
    private GameManager gameManager;
    [SerializeField] // 【新增】允許在 Inspector 中設置 UIManager
    private UIManager uiManager;

    void Awake()
    {
        // 【修改】確保 GameManager.Instance 已經可用
        // gameManager = GameManager.Instance;
        // if (gameManager != null) {
        //    uiManager = gameManager.UIManager;
        // } else {
        //    Debug.LogError("CardPlayService: GameManager.Instance is null in Awake!");
        // }
    }

    // 【修改】在 Start 中獲取引用，確保 GameManager 和 UIManager 已初始化
    void Start() {
        gameManager = GameManager.Instance;
        if (gameManager == null) {
            Debug.LogError("CardPlayService: GameManager.Instance is null in Start!");
            return;
        }
        uiManager = gameManager.UIManager;
        if (uiManager == null) {
            Debug.LogError("CardPlayService: GameManager.UIManager is null in Start!");
        }
    }


    // 統一的打牌方法
    // 在離線模式下，此方法由 CardUI.OnPointerClick 調用
    // 在線上模式下，此方法可能不再由本地玩家的 CardUI.OnPointerClick 直接完整調用其所有邏輯
    // 而是，CardUI.OnPointerClick 會發送請求到伺服器。
    // 當伺服器確認出牌並發回 gameStateUpdate 時，GameManager 會更新狀態，
    // UIManager.UpdateUI 會重新渲染手牌和場地。
    // 如果需要為“剛剛被打出的牌”播放特定動畫，可能需要一個新的方法或調整此方法。
    public void PlayCard(Card card, bool isPlayer, GameObject cardObject)
    {
        if (card == null)
        {
            Debug.LogError("PlayCard: Card is null!");
            return;
        }
        if (gameManager == null) { // 【新增】檢查 gameManager
            Debug.LogError("PlayCard: GameManager is null!");
            return;
        }


        // --- 離線模式邏輯 ---
        if (gameManager.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
        {
            // 回合檢查 (離線)
            if (isPlayer && !gameManager.CurrentState.IsPlayerTurn)
            {
                Debug.LogWarning("Not player's turn (Offline)!");
                CardAnimationManager.Instance.SetAnimationPlaying(false); // 重置動畫標誌
                return;
            }
            else if (!isPlayer && gameManager.CurrentState.IsPlayerTurn)
            {
                Debug.LogWarning("Not player's turn (Offline)!");
                CardAnimationManager.Instance.SetAnimationPlaying(false); // 重置動畫標誌
                return;
            }

            // 法力值檢查 (離線)
            int currentMana = isPlayer ? gameManager.CurrentState.PlayerMana : gameManager.CurrentState.OpponentMana;
            if (currentMana < card.cost)
            {
                Debug.LogWarning($"Not enough mana for {(isPlayer ? "player" : "opponent")} (Offline)! Current: {currentMana}, Required: {card.cost}");
                CardAnimationManager.Instance.SetAnimationPlaying(false); // 重置動畫標誌
                return;
            }

            // 從手牌中移除卡牌並消耗法力值 (離線)
            if (isPlayer)
            {
                gameManager.CurrentState.PlayerHand.Remove(card);
                gameManager.CurrentState.PlayerMana -= card.cost;
            }
            else // AI (離線)
            {
                gameManager.CurrentState.OpponentHand.Remove(card);
                gameManager.CurrentState.OpponentMana -= card.cost;
            }
            Debug.Log($"{(isPlayer ? "Player" : "Opponent")} played {card.name} (Offline). Mana after: "+
            $"{(isPlayer ? gameManager.CurrentState.PlayerMana : gameManager.CurrentState.OpponentMana)}.");

            // 處理動畫和效果 (離線)
            if (cardObject != null && uiManager != null) // 確保 cardObject 和 uiManager 有效
            {
                cardObject.SetActive(true);
                Transform sourcePanel = isPlayer ? uiManager.playerHandPanel : uiManager.opponentHandPanel;
                Transform targetPanel = isPlayer ? uiManager.playerFieldPanel : uiManager.opponentFieldPanel;
                // 【重要】線上模式下，卡牌效果應用由伺服器決定，動畫後的 ApplyCardEffect 回調需要調整
                uiManager.PlayCardWithAnimation(card, isPlayer, cardObject, sourcePanel, targetPanel);
            }
            else // 如果沒有卡牌物件 (例如 AI 直接應用效果，或者 UI 管理器不存在)
            {
                Debug.LogWarning($"PlayCard (Offline): cardObject is null for {card.name} or UIManager is missing. Applying effect directly.");
                card.ApplyCardEffect(isPlayer); // 應用效果
                gameManager.CheckGameOver(); // 檢查遊戲結束
                if (uiManager != null) uiManager.UpdateUI(); // 強制更新UI
                else Debug.LogError("UIManager is null, cannot update UI after direct effect application.");
                CardAnimationManager.Instance.SetAnimationPlaying(false); // 重置動畫標誌
            }
        }
        // --- 線上模式下，此 PlayCard 方法的直接調用方式和職責會改變 ---
        // 線上模式的出牌請求已由 CardUI -> WebSocketManager 發送。
        // 此方法可能被重構為 DisplayPlayedCardAnimation，由 GameManager 在收到伺服器訊息後調用。
        else if (gameManager.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI ||
                 gameManager.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            // 【注意】線上模式下，此 PlayCard 的傳統邏輯 (扣費、移手牌、應用效果) 已移至伺服器。
            // 客戶端的 CardUI.OnPointerClick -> WebSocketManager.SendPlayCardRequest
            // 伺服器處理 -> 發送 gameStateUpdate 或 specific action (如 opponentPlayCard)
            // GameManager 接收更新 -> UIManager.UpdateUI() 刷新界面

            // 如果這個 PlayCard 方法仍然被某些流程調用來“顯示”一個已經被伺服器確認的打牌動作（例如，對手出牌），
            // 那麼它的職責主要是播放動畫。
            if (!isPlayer) { // 如果是顯示對手（AI或線上對手）的出牌
                Debug.Log($"Displaying opponent's card play: {card.name}");
                if (cardObject != null && uiManager != null) { // cardObject 可能是 UIManager 臨時為動畫創建的
                    cardObject.SetActive(true);
                    Transform sourcePanel = uiManager.opponentHandPanel; // 假設動畫從對手手牌區開始
                    Transform targetPanel = uiManager.opponentFieldPanel;
                    // 線上模式對手出牌，不應在客戶端 ApplyCardEffect，效果由伺服器狀態更新體現
                    uiManager.PlayCardWithAnimation(card, isPlayer, cardObject, sourcePanel, targetPanel);
                } else {
                     Debug.LogWarning($"PlayCard (Online Opponent): cardObject for {card.name} is null or UIManager missing. Cannot play animation. Card should appear on field via UIUpdate.");
                     // 即使沒有動畫，也確保卡牌數據被加入到場地列表（如果還沒被加入的話），等待 UIManager.UpdateUI() 渲染
                     if (!gameManager.CurrentState.OpponentField.Contains(card)) { // 簡單檢查避免重複添加
                        gameManager.CurrentState.OpponentField.Add(card);
                     }
                     if (uiManager != null) uiManager.UpdateUI(); // 確保UI刷新
                }
            } else {
                // 如果是玩家自己的牌，並且是由伺服器確認後觸發顯示 (例如，播放一個出牌成功的動畫)
                // 這種情況比較少見，通常是手牌直接消失，然後出現在場上，由 UIManager.UpdateUI() 完成。
                // 如果確實需要為玩家的牌在“確認後”播放動畫，則邏輯類似上方 isPlayer == false 的情況，
                // 但 sourcePanel 是 playerHandPanel，targetPanel 是 playerFieldPanel。
                // 而且，此時卡牌數據應該已經從 GameManager.playerHand 移除了（由伺服器狀態更新觸發）。
                Debug.Log($"Displaying local player's confirmed card play: {card.name}");
                 if (cardObject != null && uiManager != null) {
                    // cardObject 此時應該是從手牌UI中被拿出來的那個
                    uiManager.PlayCardWithAnimation(card, isPlayer, cardObject, uiManager.playerHandPanel, uiManager.playerFieldPanel);
                 }
            }
        }
    }
}