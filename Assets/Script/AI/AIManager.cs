using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        // 【重要】確保只在離線模式下執行
        if (gameManager.CurrentGameMode != GameManager.GameMode.OfflineSinglePlayer)
        {
            Debug.LogWarning("AIManager: PlayAITurn called in non-offline mode. Aborting.");
            yield break; // 立即退出協程
        }

        Debug.Log("AI turn started (Offline).");
        yield return new WaitForSeconds(1f); // AI思考時間

        // 嘗試找出可以打出的卡牌 (魔法值足夠且在手牌中)
        List<Card> playableCards = gameManager.opponentHand
            .Where(card => gameManager.opponentMana >= card.cost)
            .ToList();

        if (playableCards.Count > 0)
        {
            Card cardToPlay = playableCards[Random.Range(0, playableCards.Count)];
            GameObject cardObject = gameManager.UIManager.GetOpponentHandCardObject(cardToPlay); // 獲取對手手牌的UI物件

            if (cardObject != null)
            {
                // 調用 CardPlayService 來處理卡牌的打出邏輯 (離線模式)
                gameManager.CardPlayService.PlayCard(cardToPlay, false, cardObject); // isPlayer is false for AI
                Debug.Log("AI played card (Offline): " + cardToPlay.name);
            }
            else
            {
                // 如果找不到 UI 對象，仍然應用卡牌效果 (離線模式)
                Debug.LogWarning($"AIManager: Could not find card object for AI to play {cardToPlay.name}. Applying effect directly (Offline).");
                cardToPlay.ApplyCardEffect(false);
                gameManager.opponentHand.Remove(cardToPlay);
                gameManager.opponentMana -= cardToPlay.cost;
                gameManager.CheckGameOver();
                gameManager.UIManager.UpdateUI();
            }
        }
        else
        {
            Debug.Log("AI has no playable cards (Offline). Ending turn.");
        }

        yield return new WaitForSeconds(1f); // AI行動後的延遲
        gameManager.EndAITurn(); // AI結束回合 (這個方法內部也應該有模式檢查，確保只在離線時更新玩家回合)
        Debug.Log("AI turn ended (Offline).");
    }
}