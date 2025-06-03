// AIManager.cs (修改後)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AIManager : MonoBehaviour
{
    private GameManager gameManager; // GameManager 的引用仍然需要，以訪問 CurrentState 和其他服務
    private IGameState gameState;    // 直接獲取 IGameState 的引用，更清晰

    void Awake()
    {
        // 嘗試獲取 GameManager 的單例實例
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("AIManager: GameManager.Instance is null! AIManager might not function correctly.");
            // 根據您的遊戲設計，這裡可能需要禁用此組件或拋出更嚴重的錯誤
            enabled = false; // 例如，禁用 AIManager
            return;
        }

        // 從 GameManager 獲取 IGameState 實例
        // 這依賴於 GameManager.Awake() 已經執行並初始化了 CurrentState
        // 如果 AIManager 的 Awake 可能比 GameManager 的 Awake 先執行，
        // 則 gameState 的初始化最好移到 Start() 方法中，或者確保 GameManager 的執行順序更高。
        // 為簡單起見，這裡假設 GameManager.CurrentState 在 AIManager.Awake 時已可用。
        gameState = gameManager.CurrentState;
        if (gameState == null)
        {
            Debug.LogError("AIManager: GameManager.CurrentState (IGameState) is null in Awake! This might happen if AIManager's Awake runs before GameManager's. Consider moving gameState initialization to Start or ensuring GameManager executes first.");
            // 也可以在 Start() 中嘗試再次獲取
        }
    }

    void Start()
    {
        // 如果在 Awake 中 gameState 可能為 null，可以在 Start 中再次嘗試獲取或確認
        if (gameState == null && gameManager != null) // 確保 gameManager 不是 null
        {
            gameState = gameManager.CurrentState;
            if (gameState == null)
            {
                Debug.LogError("AIManager: GameManager.CurrentState (IGameState) is still null in Start! AI will not function.");
                enabled = false; // 禁用 AI
            }
        }
    }

    public IEnumerator PlayAITurn()
    {
        // 在協程開始時再次檢查 gameState，確保它已成功初始化
        if (gameState == null)
        {
            Debug.LogError("AIManager: gameState is null at the start of PlayAITurn. Aborting AI turn.");
            // 確保AI回合正確結束，即使它什麼也沒做
            if (gameManager != null && gameManager.CurrentState != null && gameManager.CurrentState.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
            {
                 gameManager.EndAITurn(); // 確保遊戲流程可以繼續
            }
            yield break;
        }

        // 【重要】確保只在離線模式下執行
        if (gameState.CurrentGameMode != GameManager.GameMode.OfflineSinglePlayer)
        {
            Debug.LogWarning("AIManager: PlayAITurn called in non-offline mode. Aborting.");
            yield break; // 立即退出協程
        }

        Debug.Log("AI turn started (Offline).");
        yield return new WaitForSeconds(1f); // AI思考時間

        // 通過 gameState 獲取對手手牌和法力值
        List<Card> playableCards = gameState.OpponentHand
            .Where(card => card != null && gameState.OpponentMana >= card.cost) // 【新增】檢查 card != null
            .ToList();

        if (playableCards.Count > 0)
        {
            Card cardToPlay = playableCards[Random.Range(0, playableCards.Count)];
            GameObject cardObject = null;

            // 嘗試從 UIManager 獲取卡牌的 GameObject (用於動畫)
            if (gameManager.UIManager != null)
            {
                cardObject = gameManager.UIManager.GetOpponentHandCardObject(cardToPlay);
            }

            // 優先使用 CardPlayService (如果可用)
            if (gameManager.CardPlayService != null)
            {
                Debug.Log($"AIManager: Attempting to play card '{cardToPlay.name}' via CardPlayService (Offline).");
                // CardPlayService.PlayCard (離線部分) 應負責:
                // 1. 從 gameState.OpponentHand 移除卡牌
                // 2. 扣除 gameState.OpponentMana
                // 3. 觸發動畫 (如果 cardObject 和 UIManager 有效)
                // 4. 調用 card.ApplyCardEffect(false, gameState)
                // 5. 調用 gameManager.CheckGameOver()
                // 6. 更新 UI (通常在動畫結束後)
                gameManager.CardPlayService.PlayCard(cardToPlay, false, cardObject);
            }
            else // CardPlayService 不可用時的備用邏輯
            {
                Debug.LogWarning($"AIManager: CardPlayService is unavailable. Playing card '{cardToPlay.name}' with direct logic (Offline).");
                if (cardObject != null && gameManager.UIManager != null)
                {
                    // 如果有卡牌物件和 UI 管理器，但沒有 CardPlayService，
                    // 仍然可以嘗試播放動畫，並在動畫結束後應用效果。
                    // 這部分邏輯可以模仿 CardPlayService 的行為，或者簡化處理。
                    // 為了簡單起見，這裡直接應用效果，並假設 UIManager 會在之後更新。
                    gameManager.UIManager.PlayCardWithAnimation(cardToPlay, false, cardObject, gameManager.UIManager.opponentHandPanel, gameManager.UIManager.opponentFieldPanel);
                    // 注意：PlayCardWithAnimation 的回調中會應用效果和檢查遊戲結束 (離線模式)
                }
                else
                {
                    // 如果連卡牌物件都沒有，直接應用效果
                    Debug.LogWarning($"AIManager: No card object for '{cardToPlay.name}'. Applying effect directly.");
                    cardToPlay.ApplyCardEffect(false, gameState); // 傳遞 gameState

                    // 手動更新狀態 (因為沒有通過 CardPlayService)
                    gameState.OpponentHand.Remove(cardToPlay);
                    gameState.OpponentMana -= cardToPlay.cost;

                    gameManager.CheckGameOver(); // 遊戲結束檢查
                    if (gameManager.UIManager != null) gameManager.UIManager.UpdateUI(); // 更新UI
                }
            }
            Debug.Log($"AI played card (Offline): {cardToPlay.name}. Opponent Mana: {gameState.OpponentMana}");
        }
        else
        {
            Debug.Log("AI has no playable cards (Offline).");
        }

        yield return new WaitForSeconds(1f); // AI行動後的延遲

        // AI結束回合 (GameManager.EndAITurn 內部應使用 gameState 來更新玩家回合狀態和資源)
        if (gameManager != null)
        {
            gameManager.EndAITurn();
        }
        Debug.Log("AI turn ended (Offline).");
    }
}