using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI feedText;
    [SerializeField] private TextMeshProUGUI staminaText;

    private void OnEnable()
    {
        if (playerStats == null) return;

        playerStats.OnHealthChanged += UpdateHealth;
        playerStats.OnFeedChanged += UpdateFeed;
        playerStats.OnStaminaChanged += UpdateStamina;
    }

    private void OnDisable()
    {
        if (playerStats == null) return;

        playerStats.OnHealthChanged -= UpdateHealth;
        playerStats.OnFeedChanged -= UpdateFeed;
        playerStats.OnStaminaChanged -= UpdateStamina;
    }

    private void UpdateHealth(float current, float max)
    {
        if (healthText != null) healthText.text = current.ToString("F0");
    }

    private void UpdateFeed(float current, float max)
    {
        if (feedText != null) feedText.text = current.ToString("F0");
    }

    private void UpdateStamina(float current, float max)
    {
        if (staminaText != null) staminaText.text = current.ToString("F0");
    }
}