using UnityEngine;

[System.Serializable]
public class Card
{
    public string id;
    public string name;
    public int cost;
    public int attack;
    public int value;
    public string effect;
    public Sprite sprite;
    public string cardType; // 【新增】例如 "Minion", "Spell"

    public void ApplyCardEffect(bool isPlayer)
    {
        if (effect.Contains("Deal"))
        {
            int damage = attack;
            if (isPlayer)
            {
                GameManager.Instance.opponentHealth -= damage;
                Debug.Log($"Player's {name} deals {damage} damage to opponent. Opponent health: {GameManager.Instance.opponentHealth}");
            }
            else
            {
                GameManager.Instance.playerHealth -= damage;
                Debug.Log($"AI's {name} deals {damage} damage to player. Player health: {GameManager.Instance.playerHealth}");
            }
        }
        else if (effect.Contains("Heal"))
        {
            int heal = value;
            if (isPlayer)
            {
                GameManager.Instance.playerHealth += heal;
                Debug.Log($"Player's {name} heals {heal} health. Player health: {GameManager.Instance.playerHealth}");
            }
            else
            {
                GameManager.Instance.opponentHealth += heal;
                Debug.Log($"AI's {name} heals {heal} health. Opponent health: {GameManager.Instance.opponentHealth}");
            }
        }
    }
}