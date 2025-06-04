// CardAnimationManager.cs
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // 確保您已正確導入 DOTween
using System.Collections;

public class CardAnimationManager : MonoBehaviour
{
    public static CardAnimationManager Instance;
    private Transform animationCanvasTransform;
    private bool _isAnimationPlaying = false;

    // --- 動畫參數 ---
    [Header("General Animation Settings")]
    public float cardMoveToAnimationLayerDuration = 0.1f; // 卡牌移動到動畫層的準備時間（可選）

    [Header("Enlarge & Hold Animation")]
    public float enlargeScaleFactor = 1.8f;     // 放大倍數 (相對於卡牌原始大小)
    public float enlargeDuration = 0.3f;        // 放大動畫的持續時間
    public Ease enlargeEase = Ease.OutQuad;      // 放大動畫的緩動類型
    public float postEnlargeHoldDuration = 0.8f;  // 放大後停留的時間

    [Header("Move to Target Animation")]
    public float moveToTargetDuration = 0.5f;     // 移動到目標位置的動畫持續時間
    public Ease moveToTargetEase = Ease.OutQuad;   // 移動動畫的緩動類型
    public bool scaleDownDuringMove = false;     // 是否在移動到目標時逐漸縮小回原始大小
    public float scaleDownDuration = 0.3f;      // 如果上面為true，縮小的持續時間

    [Header("Opponent Card Reveal Animation")]
    public float flipToRevealDuration = 0.2f;    // 翻面顯示正面的持續時間
    public float flipToOriginalDuration = 0.2f;  // 轉回來的持續時間
    public float postRevealHoldDuration = 1.5f;  // 翻面並顯示文字後，在漸隱前的停留時間

    [Header("Fade Out Animation")]
    public float fadeOutDuration = 0.5f;          // 卡牌漸變消失的持續時間
    // --- 動畫參數結束 ---


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        SetupAnimationContainer();
    }

    void SetupAnimationContainer()
    {
        if (GameManager.Instance != null && GameManager.Instance.UIManager != null && GameManager.Instance.UIManager.animationContainer != null)
        {
            animationCanvasTransform = GameManager.Instance.UIManager.animationContainer;
        }
        else
        {
            Canvas mainCanvas = FindObjectOfType<Canvas>();
            if (mainCanvas != null)
            {
                GameObject animContainerObj = new GameObject("DynamicAnimationContainer_CAM");
                animContainerObj.transform.SetParent(mainCanvas.transform, false);
                RectTransform rect = animContainerObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                animationCanvasTransform = rect;
                Debug.LogWarning("CardAnimationManager: UIManager.animationContainer not set. Created a dynamic container under Canvas.");
            }
            else
            {
                animationCanvasTransform = transform;
                Debug.LogError("CardAnimationManager: Could not find UIManager.animationContainer or a Canvas to create a dynamic container!");
            }
        }
    }
    public void SetAnimationPlaying(bool isPlaying)
    {
        _isAnimationPlaying = isPlaying;
    }
    public bool IsAnimationPlaying()
    {
        return _isAnimationPlaying;
    }


    public void PlayCardAnimation(Card card, bool isPlayer, GameObject cardObject, Transform targetPanel, System.Action onComplete)
    {
        Debug.Log($"CardAnimationManager: PlayCardAnimation called for card '{card?.name}' (isPlayer: {isPlayer})");
        //Debug.Log($"CardAnimationManager: CardObject Position: {cardObject?.transform.position}, TargetPanel Position: {targetPanel?.position}");
        if (animationCanvasTransform == null)
        {
            Debug.LogWarning("AnimationCanvasTransform not set in CardAnimationManager. Attempting to set it up now.");
            SetupAnimationContainer();
            if (animationCanvasTransform == null)
            {
                Debug.LogError("CardAnimationManager: animationCanvasTransform is still null after setup attempt. Aborting animation.");
                onComplete?.Invoke();
                SetAnimationPlaying(false);
                return;
            }
        }

        if (card == null || cardObject == null || targetPanel == null)
        {
            Debug.LogError("CardAnimationManager: 無效參數！");
            onComplete?.Invoke();
            SetAnimationPlaying(false);
            return;
        }

        SetAnimationPlaying(true);

        RectTransform rect = cardObject.GetComponent<RectTransform>();
        CardUI cardUIComponent = cardObject.GetComponent<CardUI>();
        CanvasGroup cardCanvasGroup = cardObject.GetComponent<CanvasGroup>();

        if (rect == null || cardUIComponent == null || cardUIComponent.artworkImage == null || cardCanvasGroup == null)
        {
            Debug.LogError($"CardAnimationManager: 在GameObject '{cardObject.name}' 上缺少 RectTransform, CardUI, CardUI.artworkImage, 或 CanvasGroup 組件！ Rect: {rect != null}, CardUI: {cardUIComponent != null}, ArtworkImage: {cardUIComponent?.artworkImage != null}, CanvasGroup: {cardCanvasGroup != null}");
            onComplete?.Invoke();
            SetAnimationPlaying(false);
            if (cardObject != null) Destroy(cardObject);
            return;
        }
        Image imageToAnimate = cardUIComponent.artworkImage;


        if (cardObject.transform.parent != animationCanvasTransform)
        {
            cardObject.transform.SetParent(animationCanvasTransform, true);
        }

        bool wasDestroyedDuringAnimation = false;
        Sequence seq = DOTween.Sequence();

        cardCanvasGroup.alpha = 1f;
        Vector3 originalCardScale = rect.localScale; // 卡牌在動畫層的初始（通常是手牌中的）大小
        // 步驟 0 (僅對手出牌): 確保初始是卡背
        if (!isPlayer) {
            // 這裡應該已經將 cardUIComponent 設置為顯示卡背
            // 例如，調用 cardUIComponent.ShowCardBack() 或類似方法
            // 或者在 GameManager.HandleOpponentPlayCard 中創建臨時物件時就初始化為卡背
            if (cardUIComponent.cardDetailsContainer != null) cardUIComponent.cardDetailsContainer.SetActive(false);
            if (cardUIComponent.artworkImage != null && cardUIComponent.cardBackSprite != null) {
                cardUIComponent.artworkImage.sprite = cardUIComponent.cardBackSprite;
                cardUIComponent.artworkImage.gameObject.SetActive(true);
            } else if (cardUIComponent.backgroundImage != null && cardUIComponent.cardBackSprite != null) { // 備用方案
                cardUIComponent.backgroundImage.sprite = cardUIComponent.cardBackSprite;
            }
            Debug.Log($"[AnimManager] Opponent card '{card.name}' confirmed as card back for initial move.");
        }

        // 1. 放大效果
        seq.Append(rect.DOScale(originalCardScale * enlargeScaleFactor, enlargeDuration).SetEase(enlargeEase).OnUpdate(() =>
        {
            if (cardObject == null) wasDestroyedDuringAnimation = true;
        }));

        // 2. 放大後停留
        if (postEnlargeHoldDuration > 0)
        {
            seq.AppendInterval(postEnlargeHoldDuration);
        }

        // 3. 移動到目標位置
        //    在移動的同時，可以選擇是否讓它逐漸縮小回接近原始大小（如果卡牌要留在場上）
        Tweener moveTween = rect.DOMove(targetPanel.position, moveToTargetDuration).SetEase(moveToTargetEase).OnUpdate(() =>
        {
            if (cardObject == null) wasDestroyedDuringAnimation = true;
        });
        seq.Append(moveTween);

        if (scaleDownDuringMove && enlargeScaleFactor > 1.0f) // 只有當確實放大了才需要縮小
        {
            // 讓縮小動畫與移動動畫並行，或者緊隨其後
            // 如果與移動並行，卡牌會在移動過程中縮小
            // seq.Join(rect.DOScale(originalCardScale, scaleDownDuration).SetEase(moveEase)); // 與移動並行縮回原大小
            // 如果在移動後縮小：
            // seq.Append(rect.DOScale(originalCardScale, scaleDownDuration).SetEase(Ease.InQuad));
        }


        // 4. 對手卡牌翻面動畫 和 額外顯示停留
        if (!isPlayer && card.sprite != null)
        {
            // 卡牌已經移動到目標位置附近，並保持著放大（或正在縮小）的狀態
            seq.Append(rect.DORotate(new Vector3(0, 90, 0), flipToRevealDuration)
                .OnUpdate(() => { if (cardObject == null) wasDestroyedDuringAnimation = true; })
                .OnComplete(() =>
                {
                    if (!wasDestroyedDuringAnimation && imageToAnimate != null && card != null)
                    {
                        imageToAnimate.sprite = card.sprite;
                        if (cardUIComponent.cardDetailsContainer != null)
                        {
                            cardUIComponent.cardDetailsContainer.SetActive(true);
                            Debug.Log($"CardAnimationManager: Activated cardDetailsContainer for {card.name}");
                        }
                        // 確保文字內容正確，Initialize應該已經做過，但以防萬一
                        cardUIComponent.Initialize(card, isPlayer, true); // isField 應為 true 因為它到了場上
                    }
                }));
            seq.Append(rect.DORotate(Vector3.zero, flipToOriginalDuration));
            if (postRevealHoldDuration > 0)
            {
                seq.AppendInterval(postRevealHoldDuration);
            }
        }
        else if (isPlayer && cardUIComponent.cardDetailsContainer != null) // 玩家出牌，確保細節顯示
        {
            if (!cardUIComponent.cardDetailsContainer.activeSelf)
            {
                cardUIComponent.cardDetailsContainer.SetActive(true);
            }
        }

        // 5. 卡牌整體淡出 (用於法術牌或效果結束後消失的牌)
        //    如果卡牌是單位牌，要留在場上，則不應該執行這個淡出，而是動畫結束，由UIManager.UpdateField處理
        //    這裡假設PlayCardAnimation是用於那些“播放完效果就消失”的卡牌。
        //    如果您的遊戲中，所有打出的牌（包括單位）都有一個這樣的“消失”動畫，然後再由UpdateField重新創建在場上，那也可以。
        //    但通常單位牌是移動到場上並停留。
        //    【重要】您需要根據您的遊戲邏輯決定是否所有牌都執行此淡出。
        //    如果這張牌是要永久放到場上的（非消失型法術），則應移除下面的AppendFade，並在OnComplete中處理其最終狀態。

        // 如果是放到場上且不消失的牌，在淡出前應恢復其正常大小
        if (scaleDownDuringMove == false && enlargeScaleFactor > 1.0f) // 如果移動時沒縮小，且之前放大了
        {
            // 可以選擇在淡出前，或者如果牌不淡出，就直接設置為最終大小
            // seq.Append(rect.DOScale(originalCardScale, 0.1f)); // 快速恢復
        }

        seq.Append(cardCanvasGroup.DOFade(0, fadeOutDuration).OnUpdate(() =>
        {
            if (cardObject == null) wasDestroyedDuringAnimation = true;
        }));


        seq.OnComplete(() =>
        {
            onComplete?.Invoke(); //Onkill 會重複在執行一次
            if (!wasDestroyedDuringAnimation && cardObject != null)
            {
                StartCoroutine(DestroyAfterFrame(cardObject));
            }
            else
            {
                Debug.LogWarning($"Card {(cardObject != null ? cardObject.name : "OBJECT_DESTROYED")} 在動畫結束時已無效或被外部銷毀。");
            }
            SetAnimationPlaying(false);
        });

        seq.OnKill(() =>
        {
            if (!wasDestroyedDuringAnimation)
            {
                onComplete?.Invoke();
            }
            SetAnimationPlaying(false);
            if (!wasDestroyedDuringAnimation && cardObject != null)
            {
                // Destroy(cardObject);
            }
        });
    }

    private IEnumerator DestroyAfterFrame(GameObject obj)
    {
        yield return null;
        if (obj != null)
        {
            Destroy(obj);
        }
    }
}