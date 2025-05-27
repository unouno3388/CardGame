using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections; // For IEnumerator

public class UIManager : MonoBehaviour
{
    public Transform playerHandPanel;
    public Transform opponentHandPanel;
    public Transform playerFieldPanel;
    public Transform opponentFieldPanel;
    public Text playerHealthText;
    public Text opponentHealthText;
    public Text playerManaText;
    public Text gameOverText;
    public GameObject cardPrefab;
    public float cardSpacing = 10f;

    // 將 GameObject 列表命名得更清晰，以區分數據和UI物件
    private List<GameObject> playerHandCardObjects = new List<GameObject>();
    private List<GameObject> opponentHandCardObjects = new List<GameObject>();
    private List<GameObject> playerFieldCardObjects = new List<GameObject>();
    private List<GameObject> opponentFieldCardObjects = new List<GameObject>();

    public Transform animationContainer;

    void Awake()
    {
        if (playerHandPanel == null || opponentHandPanel == null ||
            playerFieldPanel == null || opponentFieldPanel == null || cardPrefab == null)
        {
            Debug.LogError("UIManager: One or more UI components are not assigned!");
        }

        EnsureLayoutGroup(playerHandPanel);
        EnsureLayoutGroup(opponentHandPanel);
        EnsureLayoutGroup(playerFieldPanel);
        EnsureLayoutGroup(opponentFieldPanel);
    }

    void EnsureLayoutGroup(Transform panel)
    {
        if (panel == null) return;
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = cardSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    public void UpdateUI()
    {
        if (GameManager.Instance == null) return; // 防禦性程式碼

        if (playerHealthText != null)
            playerHealthText.text = "玩家生命: " + GameManager.Instance.playerHealth;
        if (opponentHealthText != null)
            opponentHealthText.text = "對手生命: " + GameManager.Instance.opponentHealth;
        if (playerManaText != null)
            playerManaText.text = "魔力: " + GameManager.Instance.playerMana + "/" + GameManager.Instance.maxMana;

        UpdateHand(GameManager.Instance.playerHand, playerHandPanel, true, playerHandCardObjects);
        UpdateHand(GameManager.Instance.opponentHand, opponentHandPanel, false, opponentHandCardObjects);
        UpdateField(GameManager.Instance.playerField, playerFieldPanel, true, playerFieldCardObjects);
        UpdateField(GameManager.Instance.opponentField, opponentFieldPanel, false, opponentFieldCardObjects);
    }

    void UpdateHand(List<Card> handData, Transform panel, bool isPlayer, List<GameObject> handCardObjectsList)
    {
        if (panel == null)
        {
            Debug.LogError($"UIManager: Panel is null for UpdateHand (isPlayer: {isPlayer})!");
            return;
        }

        // 1. 銷毀不再存在於手牌數據中的舊UI物件
        List<GameObject> toRemove = new List<GameObject>();
        foreach (var cardObj in handCardObjectsList)
        {
            if (cardObj == null) continue;
            CardUI cardUIComp = cardObj.GetComponent<CardUI>();
            // 如果UI物件沒有對應的卡牌數據，或者其卡牌數據已不在當前手牌數據中，則標記為待刪除
            if (cardUIComp == null || cardUIComp.card == null || !handData.Contains(cardUIComp.card))
            {
                toRemove.Add(cardObj);
            }
        }

        foreach (var cardObjToRemove in toRemove)
        {
            handCardObjectsList.Remove(cardObjToRemove); // 從追蹤列表移除
            DOTween.Kill(cardObjToRemove.transform); // 停止動畫
            Destroy(cardObjToRemove); // 銷毀GameObject
        }

        // 2. 更新現有或創建新的UI物件以匹配手牌數據
        foreach (var cardData in handData)
        {
            if (cardData == null) continue;

            // 檢查是否已存在該卡牌的UI物件
            GameObject existingCardObj = handCardObjectsList.Find(obj => obj != null && obj.GetComponent<CardUI>()?.card == cardData);

            if (existingCardObj == null) // 如果不存在，則創建新的
            {
                GameObject newCardObj = Instantiate(cardPrefab, panel);
                CardUI cardUI = newCardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(cardData, isPlayer, false); // isField 為 false
                    handCardObjectsList.Add(newCardObj); // 添加到追蹤列表

                    RectTransform rect = newCardObj.GetComponent<RectTransform>();
                    if (rect != null) // 增加空檢查
                    {
                        rect.localScale = Vector3.one;
                        rect.sizeDelta = new Vector2(100, 150);
                        rect.anchorMin = new Vector2(0.5f, 0.5f);
                        rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.localPosition = Vector3.zero;
                    }
                }
                else
                {
                    Debug.LogError("UIManager: CardUI component missing on card prefab! Destroying instantiated object.");
                    Destroy(newCardObj);
                }
            }
        }

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = cardSpacing;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
        }
    }

    void UpdateField(List<Card> fieldData, Transform panel, bool isPlayer, List<GameObject> fieldCardObjectsList)
    {
        if (panel == null)
        {
            Debug.LogError($"UIManager: Panel is null for UpdateField (isPlayer: {isPlayer})!");
            return;
        }

        // 1. 銷毀舊的場上卡牌UI
        List<GameObject> toRemove = new List<GameObject>();
        foreach (var cardObj in fieldCardObjectsList)
        {
            if (cardObj == null) continue;
            CardUI cardUIComp = cardObj.GetComponent<CardUI>();
            if (cardUIComp == null || cardUIComp.card == null || !fieldData.Contains(cardUIComp.card))
            {
                toRemove.Add(cardObj);
            }
        }
        foreach (var cardObjToRemove in toRemove)
        {
            fieldCardObjectsList.Remove(cardObjToRemove);
            DOTween.Kill(cardObjToRemove.transform);
            Destroy(cardObjToRemove);
        }

        // 2. 創建新的場上卡牌UI
        foreach (var cardData in fieldData)
        {
            if (cardData == null) continue;
            GameObject existingCardObj = fieldCardObjectsList.Find(obj => obj != null && obj.GetComponent<CardUI>()?.card == cardData);
            if (existingCardObj == null)
            {
                GameObject newCardObj = Instantiate(cardPrefab, panel);
                CardUI cardUI = newCardObj.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.Initialize(cardData, isPlayer, true); // isField 為 true
                    fieldCardObjectsList.Add(newCardObj);
                    RectTransform rect = newCardObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.localScale = Vector3.one;
                        rect.sizeDelta = new Vector2(100, 150);
                        rect.anchorMin = new Vector2(0.5f, 0.5f);
                        rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.localPosition = Vector3.zero;
                    }
                }
                else
                {
                    Debug.LogError("UIManager: CardUI component missing on card prefab! Destroying instantiated object.");
                    Destroy(newCardObj);
                }
            }
        }
        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = cardSpacing;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
        }
    }

    public void PlayCardWithAnimation(Card card, bool isPlayer, GameObject cardObject, Transform sourcePanel, Transform targetPanel)
    {
        if (card == null || cardObject == null || targetPanel == null)
        {
            Debug.LogError("UIManager: 動畫參數錯誤！");
            if (cardObject != null) CardUI.IsAnimationPlaying = false; // 重置動畫狀態
            return;
        }

        // 從 UIManager 的手牌追蹤列表中移除，因為它即將播放動畫並由 CardAnimationManager 管理其生命週期
        bool removed = false;
        if (isPlayer)
        {
            removed = playerHandCardObjects.Remove(cardObject);
        }
        else
        {
            removed = opponentHandCardObjects.Remove(cardObject);
        }
        if (removed)
        {
            // Debug.Log($"UIManager: Removed {cardObject.name} from tracking list for animation.");
        }
        else
        {
            // Debug.LogWarning($"UIManager: Could not remove {cardObject.name} from tracking list. It might have been already removed or not tracked correctly.");
        }

        DOTween.Kill(cardObject.transform, true); // 確保殺死的是 transform 上的動畫，或者 cardObject 本身
        CardUI.IsAnimationPlaying = true;

        CardAnimationManager.Instance.PlayCardAnimation(card, isPlayer, cardObject, targetPanel, () =>
        {
            // 動畫完成後執行
            if (GameManager.Instance != null && card != null) // 增加空檢查
            {
                card.ApplyCardEffect(isPlayer); // 應用卡牌效果
                GameManager.Instance.CheckGameOver(); // 檢查遊戲是否結束
            }
            CardUI.IsAnimationPlaying = false; // 重置靜態標誌
            StartCoroutine(DelayedUpdateUI()); // 觸發UI刷新
        });
    }

    private IEnumerator DelayedUpdateUI()
    {
        yield return new WaitForSeconds(0.1f); // 稍微縮短延遲，但仍需確保動畫回調優先
        if (GameManager.Instance != null && GameManager.Instance.UIAutoUpdate) // 檢查 UIAutoUpdate 標誌
        {
            GameManager.Instance.UIManager.UpdateUI();
        }
    }

    // 新增方法：根據 Card 數據找到對應的對手手牌 UI GameObject
    public GameObject GetOpponentHandCardObject(Card card)
    {
        foreach (var cardObj in opponentHandCardObjects)
        {
            if (cardObj != null)
            {
                CardUI cardUI = cardObj.GetComponent<CardUI>();
                if (cardUI != null && cardUI.card == card && cardObj.activeInHierarchy)
                {
                    return cardObj;
                }
            }
        }
        return null;
    }

    public void ShowGameOver(string message)
    {
        if (gameOverText != null)
        {
            gameOverText.text = message;
            gameOverText.gameObject.SetActive(true);
        }
    }
}