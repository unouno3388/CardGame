// CardAnimationManager.cs
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class CardAnimationManager : MonoBehaviour
{
    public static CardAnimationManager Instance;
    private Transform animationCanvasTransform;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

         if (GameManager.Instance != null && GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.animationContainer != null)
        {
            animationCanvasTransform = GameManager.Instance.UIManager.animationContainer;
        }
        else
        {
            // Fallback: 如果找不到指定的容器，可以考慮在 Canvas 根目錄下動態創建一個
            // 或者直接使用 Canvas 的 RectTransform (但不推薦，因為 Canvas 可能有其他全局設置)
            // 或者使用 UIManager 自身的 transform (如果它在 Canvas 下且沒有 LayoutGroup)
            Canvas mainCanvas = FindObjectOfType<Canvas>(); // 找到場景中的主 Canvas
            if (mainCanvas != null) {
                GameObject animContainerObj = new GameObject("DynamicAnimationContainer");
                animContainerObj.transform.SetParent(mainCanvas.transform, false); // false: 不保持世界位置，而是根據父級重置
                RectTransform rect = animContainerObj.AddComponent<RectTransform>();
                // 可以根據需要設置 rect 的 anchor 和 pivot，通常全屏覆蓋
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                animationCanvasTransform = rect;
                Debug.LogWarning("CardAnimationManager: UIManager.animationContainer not set. Created a dynamic container under Canvas.");
            } else {
                animationCanvasTransform = transform; // 最差情況，用自己
                Debug.LogError("CardAnimationManager: Could not find UIManager.animationContainer or a Canvas to create a dynamic container!");
            }
        }
    }

    public void PlayCardAnimation(Card card, bool isPlayer, GameObject cardObject, Transform targetPanel, System.Action onComplete)
    {
        if (card == null || cardObject == null || targetPanel == null)
        {
            Debug.LogError("CardAnimationManager: 無效參數！");
            onComplete?.Invoke();
            return;
        }

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        // --- 修改開始 ---
        CardUI cardUIComponent = cardObject.GetComponent<CardUI>();

        if (rect == null || cardUIComponent == null || cardUIComponent.cardImage == null)
        {
            Debug.LogError($"CardAnimationManager: 在GameObject '{cardObject.name}' 上缺少 RectTransform 或 CardUI/Image 組件！ Rect: {rect != null}, CardUI: {cardUIComponent != null}, CardUI.Image: {cardUIComponent?.cardImage != null}");
            onComplete?.Invoke();
            // 考慮是否在這裡也銷毀 cardObject，如果它確實處於無效狀態
            // if (cardObject != null) Destroy(cardObject); // 可選，但可能與onComplete?.Invoke()衝突
            return;
        }
        Image image = cardUIComponent.cardImage; // 使用 CardUI 組件引用的 Image
                                                 // --- 修改結束 ---

        // --- 新增：改變父級以脫離原面板的佈局影響 ---
        // 記錄原始父級，以防需要（但對於打出後銷毀的牌，通常不需要還原）
        // Transform originalParent = cardObject.transform.parent;
        if (cardObject.transform.parent != animationCanvasTransform) // 避免不必要的SetParent
        {
            cardObject.transform.SetParent(animationCanvasTransform, true); // true: worldPositionStays
            // Debug.Log($"Card '{cardObject.name}' parent set to animationCanvasTransform: {animationCanvasTransform.name}");
        }
        // --- 新增結束 ---
        // CardUI cardUI = cardObject.GetComponent<CardUI>(); // 這行現在是 cardUIComponent

        bool wasDestroyed = false;
        Sequence seq = DOTween.Sequence();

        seq.Append(rect.DOScale(1.2f, 0.2f).OnUpdate(() => {
            if (cardObject == null) wasDestroyed = true;
        }));

        seq.Append(rect.DOMove(targetPanel.position, 0.5f).SetEase(Ease.OutQuad).OnUpdate(() => {
            if (cardObject == null) wasDestroyed = true;
        }));

        if (!isPlayer)
        {
            seq.Append(rect.DORotate(new Vector3(0, 90, 0), 0.2f)
                .OnUpdate(() => { if (cardObject == null) wasDestroyed = true; })
                .OnComplete(() =>
                {
                    if (!wasDestroyed && image != null && card != null) // 確保 card 也非空
                    {
                        image.sprite = card.sprite;
                    }
                }));
            seq.Append(rect.DORotate(Vector3.zero, 0.2f));
            if (image != null) // 增加對 image 的空檢查
            {
                seq.Append(image.DOFade(0, 0.5f));
            }
        }

        seq.OnComplete(() =>
        {
            if (!wasDestroyed && cardObject != null)
            {
                //onComplete?.Invoke();
                StartCoroutine(DestroyAfterFrame(cardObject)); // 動畫管理器負責銷毀
            }
            else
            {
                Debug.LogWarning($"Card {(cardObject != null ? cardObject.name : "null")} 已被外部銷毀或初始無效，終止動畫 OnComplete。");
                onComplete?.Invoke(); // 即使卡牌被銷毀，也執行回調以更新遊戲狀態
            }
        });

        // 以防萬一，如果序列被 Kill，也嘗試調用 onComplete
        seq.OnKill(() => {
            if (!wasDestroyed) // 避免重複調用（如果 OnComplete 已經因為銷毀而調用了）
            {
                onComplete?.Invoke();
            }
        });
    }

    private IEnumerator DestroyAfterFrame(GameObject obj)
    {
        yield return null; // 等待一幀，確保所有事件處理完畢
        if (obj != null)
        {
            // Debug.Log($"CardAnimationManager: Destroying {obj.name} after animation.");
            Destroy(obj);
        }
    }
}