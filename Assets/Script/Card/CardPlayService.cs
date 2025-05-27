using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardPlayService : MonoBehaviour
{
    private GameManager gameManager;
    private UIManager uiManager;

    void Awake()
    {
        gameManager = GameManager.Instance;
        uiManager = gameManager.UIManager;
    }

    // 统一的打牌方法
    public void PlayCard(Card card, bool isPlayer, GameObject cardObject) // cardObject 現在可以是 null (對手線上打牌)
    {
        Debug.Log($"PlayCard: {card.name}, isPlayer: {isPlayer}, cardObject: {(cardObject != null ? cardObject.name : "null")}");
        if (card == null)
        {
            Debug.LogError("PlayCard: Card is null!");
            return;
        }

        // 回合檢查
        if (isPlayer && !gameManager.isPlayerTurn)
        {
            Debug.LogWarning("Not player's turn!");
            return;
        }

        // 魔法值檢查
        int currentMana = isPlayer ? gameManager.playerMana : gameManager.opponentMana;
        if (currentMana < card.cost)
        {
            Debug.LogWarning($"Not enough mana for {(isPlayer ? "player" : "opponent")}! Current: {currentMana}, Required: {card.cost}");
            return;
        }

        // 從手牌中移除卡牌並消耗魔法值
        if (isPlayer)
        {
            gameManager.playerHand.Remove(card);
            gameManager.playerMana -= card.cost;
        }
        else
        {
            gameManager.opponentHand.Remove(card);
            gameManager.opponentMana -= card.cost;
        }
        Debug.Log($"{(isPlayer ? "Player" : "Opponent")} played {card.name}. Mana after: {(isPlayer ? gameManager.playerMana : gameManager.opponentMana)}.");

        // 處理動畫和效果
        if (cardObject != null)
        {
            // 確保卡牌對象在傳遞時是激活的
            cardObject.SetActive(true);

            Transform sourcePanel = isPlayer ? uiManager.playerHandPanel : uiManager.opponentHandPanel;
            Transform targetPanel = isPlayer ? uiManager.playerFieldPanel : uiManager.opponentFieldPanel;

            // 將卡牌從手牌UI列表中移除，因為動畫管理器會負責銷毀它
            if (isPlayer)
            {
                // UIManager.playerHandCardObjects.Remove(cardObject); // UIManager 中已處理
            }
            else
            {
                // UIManager.opponentHandCardObjects.Remove(cardObject); // UIManager 中已處理
            }

            uiManager.PlayCardWithAnimation(card, isPlayer, cardObject, sourcePanel, targetPanel);
        }
        else
        {
            // 如果沒有卡牌對象 (例如線上模式收到對手打牌訊息，且沒有對應的UI實例)，直接應用效果
            Debug.Log($"No card object provided for {card.name}. Applying effect directly.");
            card.ApplyCardEffect(isPlayer); // 應用效果
            gameManager.CheckGameOver(); // 檢查遊戲結束
            // 直接更新UI，因為沒有動畫會觸發 DelayedUpdateUI
            gameManager.UIManager.UpdateUI();
        }

        // 線上模式發送打牌訊息 (只在玩家打牌時發送)
        if (isPlayer && gameManager.CurrentGameMode == GameManager.GameMode.OnlineMultiplayer) // Use new GameMode enum
        {
            gameManager.WebSocketManager.SendPlayCard(card);
        }
        // UIManager.UpdateUI() 不在這裡調用，它會在動畫完成後或直接應用效果後被調用
    }
}