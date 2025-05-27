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
    
    public void PlayCard(Card card, bool isPlayer, GameObject cardObject = null)
    {
        Debug.Log($"PlayCard: {card.name}, isPlayer: {isPlayer}, cardObject: {cardObject?.name}");
        if (card == null)
        {
            Debug.LogError("PlayCard: Card is null!");
            return;
        }

        if (isPlayer && !gameManager.isPlayerTurn)
        {
            Debug.LogWarning("Not player's turn!");
            return;
        }

        // 檢查魔法值是否足夠
        if (isPlayer)
        {
            if (gameManager.playerMana < card.cost)
            {
                Debug.LogWarning($"Not enough mana for player! Current: {gameManager.playerMana}, Required: {card.cost}");
                return;
            }
        }
        else
        {
            if (gameManager.opponentMana < card.cost)
            {
                Debug.LogWarning($"Not enough mana for opponent! Current: {gameManager.opponentMana}, Required: {card.cost}");
                return;
            }
        }

        if (isPlayer)
        {
            if (cardObject == null)
            {
                Debug.LogWarning("玩家卡牌对象为空，直接应用效果");
                card.ApplyCardEffect(true);
                gameManager.CheckGameOver();
                return;
            }
            gameManager.playerHand.Remove(card);
            gameManager.playerMana -= card.cost;
            
            if (gameManager.IsOnline)
            {
                gameManager.WebSocketManager.SendPlayCard(card);
            }
            
            if (cardObject != null)
            {
                // 確保卡牌對象在傳遞時是激活的
                cardObject.SetActive(true);


                uiManager.PlayCardWithAnimation(card, true, cardObject, 
                    gameManager.UIManager.playerHandPanel, 
                    gameManager.UIManager.playerFieldPanel);
            }
            else
            {
                card.ApplyCardEffect(true);
                gameManager.CheckGameOver();
                // 不要在這裡調用 UpdateUI()
            }
        }
        else
        {
            Debug.Log($"CardObject: {cardObject?.name}");
            if (cardObject == null)
            {
                Debug.Log("未找到卡牌对象，直接应用效果");
                card.ApplyCardEffect(false);
                gameManager.CheckGameOver();
                return;
            }
            gameManager.opponentHand.Remove(card);
            gameManager.opponentMana -= card.cost;
            
            if (cardObject != null)
            {
                // 確保找到正確的卡牌對象

                if (cardObject.activeInHierarchy)
                {
                    uiManager.PlayCardWithAnimation(card, false, cardObject,
                        uiManager.opponentHandPanel,
                        uiManager.opponentFieldPanel);
                    
                }
            }
            else
            {
                card.ApplyCardEffect(false);
                gameManager.CheckGameOver();
                // 不要在這裡調用 UpdateUI()
            }
        }
        //GameManager.Instance.UIManager.UpdateUI();
        //uiManager.UpdateUI();
    }
}