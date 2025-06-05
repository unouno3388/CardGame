// CardUI.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))] // 【新增】確保物件上有 CanvasGroup
public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Card card; // 卡牌數據

    // --- UI元素引用 ---
    public Image backgroundImage;
    public Image artworkImage;
    public Text nameText;
    public Text costText;
    public Text descriptionText;
    public Text attackText;
    public Text valueText;
    public GameObject cardDetailsContainer; // 這個容器可以包含所有文字和 artworkImage

    public Sprite cardBackSprite;
    public Sprite defaultCardFrontSprite; // 【移除或保留】defaultCardFrontSprite 的使用已在 Initialize 中調整
    //用於控制卡牌位置與縮放的變數
    private Vector3 originalScale;
    private Vector3 originalPosition;
    private bool isPlayerCard;
    private bool isFieldCard;

    // 【新增】CanvasGroup 引用
    private CanvasGroup canvasGroup;
   
    
    void Awake()
    {
        originalScale = transform.localScale;
        originalPosition = transform.position; // 初始位置加上偏移量
        Debug.Log($"CardUI on {gameObject.name}: Awake called. Original scale: {originalScale}, Original position: {originalPosition}");
        // 【新增】獲取 CanvasGroup 組件
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) // 理論上 RequireComponent 會保證它存在
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            Debug.LogWarning($"CardUI on {gameObject.name}: CanvasGroup was missing and has been added.");
        }


        if (cardDetailsContainer == null)
        {
            Debug.LogWarning($"CardUI on {gameObject.name}: cardDetailsContainer is not assigned. Text visibility might not work as expected for card back.");
        }
    }

    public void Initialize(Card cardData, bool isPlayer, bool isField)
    {
        // ... (Initialize 方法的其他部分保持不變，如上次的修改)
        // 確保在 Initialize 開始時，CanvasGroup 是完全可見的
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // ------------ 您上次的 Initialize 程式碼應放在這裡 -----------
        if (cardData == null)
        {
            Debug.LogError($"CardUI on {gameObject.name}: Card data is null! Hiding card.");
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        this.card = cardData;
        card.sprite = Resources.Load<Sprite>($"cards/{cardData.name}"); // 確保卡牌圖片正確載入
        if (card.sprite == null)
        {
            // 如果 Resources 中沒有找到對應的卡牌圖片，則使用預設圖片
            card.sprite = defaultCardFrontSprite;
            //Debug.LogWarning($"CardUI on {gameObject.name}: Card '{cardData.name}' sprite not found in Resources/CardArt. Please check the file name and path.");
        }
        this.isPlayerCard = isPlayer;
        this.isFieldCard = isField;
        
        bool showFront = isPlayer || isField;
        if (GameManager.Instance.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer && !isPlayer && !isField) {
            showFront = false;
        }

        if (showFront)
        {
            if (cardDetailsContainer != null) cardDetailsContainer.SetActive(true);
            if (artworkImage != null)
            {
                if (cardData.sprite != null)
                {
                    artworkImage.sprite = cardData.sprite;
                    artworkImage.gameObject.SetActive(true);
                }
                else
                {
                    // artworkImage.sprite = defaultCardFrontSprite; // 使用者 CardUI.cs 中有此行
                    // 如果您希望在 sprite 為 null 時使用 defaultCardFrontSprite：
                    if (defaultCardFrontSprite != null) {
                        artworkImage.sprite = defaultCardFrontSprite;
                        artworkImage.gameObject.SetActive(true);
                    } else {
                        artworkImage.gameObject.SetActive(false);
                    }
                    Debug.LogWarning($"CardUI: Card '{cardData.name}' is missing its artwork sprite.");
                }
            }
            // ... (Initialize 中顯示正面的其他文字賦值邏輯保持不變)
            if (backgroundImage != null)
            {
                if (backgroundImage.sprite == cardBackSprite) {
                    backgroundImage.sprite = null;
                }
            }
            if (nameText != null) { nameText.text = cardData.name; nameText.gameObject.SetActive(true); }
            if (costText != null) { costText.text = "Cost"+cardData.cost.ToString(); costText.gameObject.SetActive(true); }
            if (descriptionText != null) { descriptionText.text = cardData.effect; descriptionText.gameObject.SetActive(false); }
            if (attackText != null) {
                if (cardData.attack > 0) { attackText.text = "ATK"+cardData.attack.ToString(); attackText.gameObject.SetActive(true); }
                else { attackText.gameObject.SetActive(false); }
            }
            if (valueText != null) {
                //bool shouldShowValue = cardData.value > 0;
                if (cardData.value > 0) { valueText.text = "Heal"+cardData.value.ToString(); valueText.gameObject.SetActive(true); }
                else { valueText.gameObject.SetActive(false); }
            }
        }
        else
        {
            if (cardDetailsContainer != null) cardDetailsContainer.SetActive(false);
            if (artworkImage != null)
            {
                if (cardBackSprite != null)
                {
                    artworkImage.sprite = cardBackSprite;
                    artworkImage.gameObject.SetActive(true);
                }
                else
                {
                    artworkImage.gameObject.SetActive(false);
                    Debug.LogWarning($"CardUI on {gameObject.name}: cardBackSprite is not assigned!");
                }
            }
            // ... (Initialize 中顯示背面的其他文字隱藏邏輯保持不變)
            else if (backgroundImage != null)
            {
                 if (cardBackSprite != null) { backgroundImage.sprite = cardBackSprite; }
                 else { Debug.LogWarning($"CardUI on {gameObject.name}: cardBackSprite is not assigned and no artworkImage for back!"); }
            }
            if (nameText != null) nameText.gameObject.SetActive(false);
            if (costText != null) costText.gameObject.SetActive(false);
            if (descriptionText != null) descriptionText.gameObject.SetActive(false);
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (valueText != null) valueText.gameObject.SetActive(false);
        }
        // ------------ Initialize 結束 -----------------------------
    }

    // OnPointerClick, OnPointerEnter, OnPointerExit, OnDestroy 方法保持您提供的最新版本
    public void OnPointerClick(PointerEventData eventData)
    {
        // 複製您 CardUI.cs 中最新的 OnPointerClick 內容
        if (isFieldCard || card == null || (CardAnimationManager.Instance != null && CardAnimationManager.Instance.IsAnimationPlaying()))
        {
            return;
        }

        GameManager gm = GameManager.Instance;
        if (!gm.CurrentState.IsPlayerTurn) {
            Debug.Log("Not your turn!");
            return;
        }
        if (!isPlayerCard) {
            Debug.Log("This is not your card to play from hand.");
            return;
        }

        if (gm.CurrentGameMode == GameManager.GameMode.OfflineSinglePlayer)
        {
            if (gm.CurrentState.PlayerMana >= card.cost)
            {
                gm.CardPlayService.PlayCard(card, true, gameObject); //
            }
            else
            {
                Debug.Log("Not enough mana (Offline)!");
                if (gm.UIManager != null) gm.UIManager.ShowErrorPopup("法力不足！");
            }
        }
        else if (gm.CurrentGameMode == GameManager.GameMode.OnlineSinglePlayerAI ||
                 gm.CurrentGameMode == GameManager.GameMode.OnlineMultiplayerRoom)
        {
            if (gm.CurrentState.PlayerMana >= card.cost)
            {
                gm.WebSocketManager.SendPlayCardRequest(card.id.ToString(), gm.CurrentState.RoomId); //
                Debug.Log($"Sent PlayCard request for card: {card.name} (ID: {card.id})");
            }
            else
            {
                Debug.Log("Not enough mana (Online)!");
                if (gm.UIManager != null) gm.UIManager.ShowErrorPopup("法力不足！");
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 複製您 CardUI.cs 中最新的 OnPointerEnter 內容
        if (!isFieldCard && isPlayerCard && artworkImage != null && // artworkImage 是您在 CardUI 中定義的
            (CardAnimationManager.Instance == null || !CardAnimationManager.Instance.IsAnimationPlaying()) &&
            GameManager.Instance != null && GameManager.Instance.CurrentState.IsPlayerTurn)
        {
            Debug.Log("y = "+originalPosition.y + 1f);
            transform.DOScale(originalScale * 1.5f, 0.2f).SetEase(Ease.OutQuad);
            //transform.DOMoveY(originalPosition.y + 2f, 0.5f).SetEase(Ease.OutQuad);
        }
    }
    /// <summary>
    /// 當滑鼠指針離開卡牌時觸發。
    /// 這裡會將卡牌縮放回原始大小。
    /// 如果正在播放動畫，則不執行任何操作。
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerExit(PointerEventData eventData)
    {
        // 複製您 CardUI.cs 中最新的 OnPointerExit 內容
        if (!isFieldCard && isPlayerCard && artworkImage != null && !CardAnimationManager.Instance.IsAnimationPlaying())  // artworkImage 是您在 CardUI 中定義的
        {
            Debug.Log("y = "+originalPosition.y );
            transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);
            //transform.DOMoveY(originalPosition .y - 2f, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    void OnDestroy()
    {
        // 複製您 CardUI.cs 中最新的 OnDestroy 內容
        DOTween.Kill(transform);
    }
}