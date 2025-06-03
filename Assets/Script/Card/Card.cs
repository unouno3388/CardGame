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
                GameManager.Instance.CurrentState.OpponentHealth -= damage;
                Debug.Log($"Player's {name} deals {damage} damage to opponent. Opponent health: " +
                $"{GameManager.Instance.CurrentState.OpponentHealth}");
            }
            else
            {
                GameManager.Instance.CurrentState.PlayerHealth -= damage;
                Debug.Log($"AI's {name} deals {damage} damage to player. Player health:" +
                $" {GameManager.Instance.CurrentState.PlayerHealth}");
            }
        }
        else if (effect.Contains("Heal"))
        {
            int heal = value;
            if (isPlayer)
            {
                GameManager.Instance.CurrentState.PlayerHealth += heal;
                Debug.Log($"Player's {name} heals {heal} health. Player health:" +
                $"{GameManager.Instance.CurrentState.PlayerHealth}");
            }
            else
            {
                GameManager.Instance.CurrentState.OpponentHealth += heal;
                Debug.Log($"AI's {name} heals {heal} health. Opponent health:" +
                $" {GameManager.Instance.CurrentState.OpponentHealth}");
            }
        }
    }
    public void ApplyCardEffect(bool isPlayer,IGameState gameState)
    {
        if (effect.Contains("Deal"))
        {
            int damage = attack;
            if (isPlayer)
            {
                gameState.OpponentHealth -= damage;
                Debug.Log($"Player's {name} deals {damage} damage to opponent. Opponent health: " +
                $"{gameState.OpponentHealth}");
            }
            else
            {
                gameState.PlayerHealth -= damage;
                Debug.Log($"AI's {name} deals {damage} damage to player. Player health:"+
                $" {gameState.PlayerHealth}");
            }
        }
        else if (effect.Contains("Heal"))
        {
            int heal = value;
            if (isPlayer)
            {
                gameState.PlayerHealth += heal;
                Debug.Log($"Player's {name} heals {heal} health. Player health:"+
                $"{gameState.PlayerHealth}");
            }
            else
            {
                gameState.OpponentHealth += heal;
                Debug.Log($"AI's {name} heals {heal} health. Opponent health:"+
                $" {gameState.OpponentHealth}");
            }
        }
    }
}