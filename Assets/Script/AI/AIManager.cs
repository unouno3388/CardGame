using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Added for Linq usage

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
        yield return new WaitForSeconds(1f); // AI思考時間

        // 嘗試找出可以打出的卡牌 (魔法值足夠且在手牌中)
        List<Card> playableCards = gameManager.opponentHand
            .Where(card => gameManager.opponentMana >= card.cost)
            .ToList();

        if (playableCards.Count > 0)
        {
            // 簡單的 AI 策略：隨機選擇一張可打出的卡牌
            Card cardToPlay = playableCards[Random.Range(0, playableCards.Count)];

            // 找到對應的卡牌遊戲對象 (這是 UIManager 的職責，但 AI 需要提供它)
            // 在這裡需要一個機制來獲取對應的 CardUI GameObject
            // UIManager 應該提供一個方法來根據 Card 數據找到其在手牌中的 UI GameObject
            GameObject cardObject = gameManager.UIManager.GetOpponentHandCardObject(cardToPlay);

            if (cardObject != null)
            {
                // 調用 CardPlayService 來處理卡牌的打出邏輯
                gameManager.CardPlayService.PlayCard(cardToPlay, false, cardObject);
                Debug.Log("AI played card: " + cardToPlay.name);
            }
            else
            {
                Debug.LogWarning($"AIManager: Could not find active card object for AI to play {cardToPlay.name}. Applying effect directly.");
                // 如果找不到 UI 對象，仍然應用卡牌效果，但不播放動畫
                cardToPlay.ApplyCardEffect(false);
                gameManager.opponentHand.Remove(cardToPlay); // 從手牌數據中移除
                gameManager.opponentMana -= cardToPlay.cost; // 減少魔法值
                gameManager.CheckGameOver(); // 檢查遊戲是否結束
                gameManager.UIManager.UpdateUI(); // 強制更新UI，因為沒有動畫會自動觸發
            }
        }
        else
        {
            Debug.Log("AI has no playable cards. Ending turn.");
        }

        yield return new WaitForSeconds(1f); // AI行動後的延遲
        gameManager.EndAITurn(); // AI結束回合
        Debug.Log("AI turn ended.");
    }
}