// DataConverter.cs
using System.Collections.Generic;
using UnityEngine; // Sprite 可能需要

public class DataConverter : IDataConverter
{
    public List<Card> ConvertServerCardsToClientCards(List<ServerCard> serverCards, UIManager uiManager)
    {
        if (serverCards == null)
        {
            Debug.LogWarning("[DataConverter] ConvertServerCardsToClientCards: Input serverCards list is null. Returning empty list.");
            return new List<Card>();
        }
        List<Card> clientCards = new List<Card>();
        Debug.Log($"[DataConverter] ConvertServerCardsToClientCards: Attempting to convert {serverCards.Count} server cards.");

        foreach (var sc in serverCards)
        {
            if (sc == null)
            {
                Debug.LogWarning("[DataConverter] ConvertServerCardsToClientCards: Encountered a null ServerCard in the input list. Skipping it.");
                continue;
            }
            // 日誌點1：打印正在處理的 ServerCard 的基本信息
            Debug.Log($"[DataConverter] ConvertServerCardsToClientCards: Processing ServerCard - ID: '{sc.id}', Name: '{sc.name}'");

            Card clientCard = ConvertServerCardToClientCard(sc, uiManager);
            if (clientCard != null)
            {
                clientCards.Add(clientCard);
            }
            else
            {
                // ConvertServerCardToClientCard 內部應該已經打印了為什麼返回 null 的詳細原因
                Debug.LogWarning($"[DataConverter] ConvertServerCardsToClientCards: ConvertServerCardToClientCard returned null for ServerCard Name: '{sc.name}', ID: '{sc.id}'. This card will not be added.");
            }
        }
        Debug.Log($"[DataConverter] ConvertServerCardsToClientCards: Successfully converted and added {clientCards.Count} client cards out of {serverCards.Count} server cards received.");
        return clientCards;
    }

    public Card ConvertServerCardToClientCard(ServerCard serverCard, UIManager uiManager)
    {
        if (serverCard == null)
        {
            Debug.LogError("[DataConverter] ConvertServerCardToClientCard: Received a null serverCard object. Returning null.");
            return null;
        }
        // 日誌點2：打印傳入的 ServerCard 的所有詳細屬性
        Debug.Log($"[DataConverter] ConvertServerCardToClientCard: Input ServerCard Details - ID: '{serverCard.id}', Name: '{serverCard.name}', Cost: {serverCard.cost}, Attack: {serverCard.attack}, Value: {serverCard.value}, Effect: '{serverCard.effect}', CardType: '{serverCard.cardType}'");

        // 檢查關鍵的 ID 和 Name 是否為空，這可能導致後續問題
        if (string.IsNullOrEmpty(serverCard.id))
        {
            Debug.LogError($"[DataConverter] ConvertServerCardToClientCard: ServerCard '{serverCard.name ?? "UNKNOWN NAME"}' has a NULL or EMPTY ID. This is critical and will cause issues. Returning null.");
            return null; // 如果沒有ID，卡牌在客戶端很難被正確識別和管理
        }
        if (string.IsNullOrEmpty(serverCard.name)) {
            Debug.LogWarning($"[DataConverter] ConvertServerCardToClientCard: ServerCard with ID '{serverCard.id}' has a NULL or EMPTY Name.");
            // 根據您的遊戲邏輯，名稱為空可能不致命，但最好有值
        }

        Sprite cardSprite = null;
        if (uiManager != null) // UIManager 負責 Sprite 的獲取
        {
            // 日誌點3：記錄調用 GetCardSpriteByName 之前和之後
            // Debug.Log($"[DataConverter] ConvertServerCardToClientCard: Calling UIManager.GetCardSpriteByName for card name: '{serverCard.name}'");
            cardSprite = uiManager.GetCardSpriteByName(serverCard.name);
            if (cardSprite == null) {
                // Debug.LogWarning($"[DataConverter] ConvertServerCardToClientCard: UIManager.GetCardSpriteByName returned null for card name: '{serverCard.name}'.");
            } else {
                // Debug.Log($"[DataConverter] ConvertServerCardToClientCard: Successfully got sprite '{cardSprite.name}' for card '{serverCard.name}'.");
            }
        }
        else
        {
            // 如果沒有 UIManager，嘗試直接從 Resources 加載 (備用方案)
            // cardSprite = Resources.Load<Sprite>($"cards/{serverCard.name}");
            Debug.LogWarning($"[DataConverter] ConvertServerCardToClientCard: UIManager is null. Cannot get sprite for card '{serverCard.name}'. Sprite will be null.");
            //Debug.LogWarning($"DataConverter: UIManager is null. Cannot get sprite for card '{serverCard.name}' via UIManager. Consider direct Resource loading if needed.");
        }


        Card clientCard = new Card
        {
            id = serverCard.id,
            name = serverCard.name,
            cost = serverCard.cost,
            attack = serverCard.attack,
            value = serverCard.value,
            effect = serverCard.effect,
            cardType = serverCard.cardType,
            sprite = cardSprite // sprite 可以為 null
        };

        // 日誌點4：打印創建的 Client Card 的基本信息
        Debug.Log($"[DataConverter] ConvertServerCardToClientCard: Successfully created Client Card - ID: '{clientCard.id}', Name: '{clientCard.name}', Sprite is null: {clientCard.sprite == null}");
        return clientCard;
    }
}