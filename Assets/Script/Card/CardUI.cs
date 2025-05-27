using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class CardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Card card;
    public Text nameText;
    public Text descriptionText;
    public Text costText;
    public Text attackText;
    public Text valueText;
    public Image cardImage;
    public Sprite cardBackSprite;
    private Vector3 originalScale;
    private bool isPlayerCard;
    private bool isFieldCard;
    private static bool isAnimationPlaying = false;
    public static bool IsAnimationPlaying
    {
        get => isAnimationPlaying;
        set => isAnimationPlaying = value;
    }
    void Awake()
    {
        if (cardImage == null) cardImage = GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("NameText")?.GetComponent<Text>();
        if (descriptionText == null) descriptionText = transform.Find("DescriptionText")?.GetComponent<Text>();
        if (costText == null) costText = transform.Find("CostText")?.GetComponent<Text>();
        if (attackText == null) attackText = transform.Find("AttackText")?.GetComponent<Text>();
        if (valueText == null) valueText = transform.Find("ValueText")?.GetComponent<Text>();
        
        originalScale = transform.localScale;
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
            if (cardImage == null)
            {
                Debug.LogError("CardUI: cardImage is not assigned and no Image component found!");
            }
        }
    }

    public void Initialize(Card cardData, bool isPlayer, bool isField)
    {
        if (cardData == null)
        {
            Debug.LogError("CardUI: Card data is null!");
            return;
        }

        card = cardData;
        isPlayerCard = isPlayer;
        isFieldCard = isField;

        if (isPlayer || isField)
        {
            // 玩家卡牌或場上卡牌顯示正面
            if (nameText != null) nameText.text = card.name;
            if (descriptionText != null) descriptionText.text = card.effect;
            if (costText != null) costText.text = card.cost.ToString();
            if (attackText != null) attackText.text = card.attack.ToString();
            if (valueText != null) valueText.text = card.value.ToString();
            if (cardImage != null) cardImage.sprite = card.sprite;
        }
        else
        {
            // 對手卡牌顯示卡背
            if (nameText != null) nameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (costText != null) costText.text = "";
            if (attackText != null) attackText.text = "";
            if (valueText != null) valueText.text = "";
            if (cardImage != null)
            {
                // 確保有卡背圖片
                if (cardBackSprite != null)
                {
                    cardImage.sprite = cardBackSprite;
                }
                else
                {
                    Debug.LogWarning("CardUI: cardBackSprite is not assigned for opponent card!");
                    // 如果沒有卡背，至少隱藏文字
                    cardImage.sprite = null;
                    cardImage.color = Color.gray; // 使用灰色作為臨時卡背
                }
            }
        }

        RectTransform rect = GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(100, 150);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isPlayerCard || !GameManager.Instance.isPlayerTurn || card == null || isFieldCard || CardUI.IsAnimationPlaying)
        {
            return;
        }

        // 殺死可能存在的殘留動畫
        DOTween.Kill(transform);
        if (cardImage != null)
        {
            DOTween.Kill(cardImage);
        }

        CardUI.IsAnimationPlaying = true;
        GameManager.Instance.CardPlayService.PlayCard(card, true, gameObject);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isPlayerCard && cardImage != null && !isFieldCard)
        {
            transform.DOScale(originalScale * 1.1f, 0.2f);
            cardImage.DOColor(Color.yellow, 0.2f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPlayerCard && cardImage != null && !isFieldCard)
        {
            transform.DOScale(originalScale, 0.2f);
            cardImage.DOColor(Color.white, 0.2f);
        }
    }

    void OnDestroy()
    {
        // 强制终止所有相关动画
        DOTween.Kill(transform, true);  // true表示立即完全终止
        
        if (cardImage != null)
        {
            DOTween.Kill(cardImage, true);
        }
    }
}