// DataConverter.cs
using System.Collections.Generic;
using UnityEngine; // Sprite 可能需要

public class DataConverter : IDataConverter
{
    public List<Card> ConvertServerCardsToClientCards(List<ServerCard> serverCards, UIManager uiManager)
    {
        if (serverCards == null) return new List<Card>();
        List<Card> clientCards = new List<Card>();
        foreach (var sc in serverCards)
        {
            Card clientCard = ConvertServerCardToClientCard(sc, uiManager);
            if (clientCard != null)
            {
                clientCards.Add(clientCard);
            }
        }
        return clientCards;
    }

    public Card ConvertServerCardToClientCard(ServerCard serverCard, UIManager uiManager)
    {
        if (serverCard == null) return null;

        Sprite cardSprite = null;
        if (uiManager != null) // UIManager 負責 Sprite 的獲取
        {
            cardSprite = uiManager.GetCardSpriteByName(serverCard.name);
        }
        else
        {
            // 如果沒有 UIManager，嘗試直接從 Resources 加載 (備用方案)
            // cardSprite = Resources.Load<Sprite>($"cards/{serverCard.name}");
            Debug.LogWarning($"DataConverter: UIManager is null. Cannot get sprite for card '{serverCard.name}' via UIManager. Consider direct Resource loading if needed.");
        }


        return new Card
        {
            id = serverCard.id,
            name = serverCard.name,
            cost = serverCard.cost,
            attack = serverCard.attack,
            value = serverCard.value,
            effect = serverCard.effect,
            cardType = serverCard.cardType,
            sprite = cardSprite
        };
    }
}