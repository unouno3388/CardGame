using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AIManager : MonoBehaviour
{
    private GameManager gameManager;

    void Awake()
    {
        gameManager = GetComponent<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("AIManager: GameManager component missing!");
        }
    }

    public IEnumerator PlayAITurn()
    {
        Debug.Log("AI turn started.");
        yield return new WaitForSeconds(1f);

        List<Card> opponentHand = gameManager.opponentHand;
        if (opponentHand.Count > 0)
        {
            Card card = opponentHand[Random.Range(0, opponentHand.Count)];
            
            // 找到對應的卡牌遊戲對象
            GameObject cardObject = null;
            foreach (Transform child in gameManager.UIManager.opponentHandPanel)
            {
                CardUI cardUI = child.GetComponent<CardUI>();
                if (cardUI != null && cardUI.card == card && child.gameObject.activeInHierarchy)
                {
                    cardObject = child.gameObject;
                    break;
                }
            }
            
            if (cardObject != null)
            {
                
                gameManager.CardPlayService.PlayCard(card, false, cardObject);
                Debug.Log("AI played card: " + card.name);
            }
            else
            {
                Debug.LogWarning("Could not find active card object for AI to play");
                card.ApplyCardEffect(false);
                gameManager.CheckGameOver();
            }
        }

        yield return new WaitForSeconds(1f);
        gameManager.EndAITurn();
        Debug.Log("AI turn ended.");
    }
}