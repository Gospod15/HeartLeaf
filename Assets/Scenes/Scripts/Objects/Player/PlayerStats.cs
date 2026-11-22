using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    [Header("Hunger")]
    [SerializeField] private float maxFeed = 100f;
    [SerializeField] private float feedDrainPerSecond = 0.001f;
    [SerializeField] private float starvationDamagePerSecond = 1f;
    public float CurrentFeed { get; private set; }
    public float MaxFeed => maxFeed;
    public bool IsStarving => CurrentFeed <= 0f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenPerSecond = 7f;
    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;

    public bool IsDead => CurrentHealth <= 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnFeedChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action OnDied;

    private bool isRestoredFromSave = false;

    private void Awake()
    {
        if (!isRestoredFromSave)
        {
            CurrentHealth = maxHealth;
            CurrentFeed = maxFeed;
            CurrentStamina = maxStamina;
        }
    }

    private void Start()
    {
        NotifyAll();
    }

    private void Update()
    {
        if (IsDead) return;
        HandleHunger();
    }

    private void HandleHunger()
    {
        if (CurrentFeed > 0f)
        {
            CurrentFeed = Mathf.Max(0f, CurrentFeed - feedDrainPerSecond * Time.deltaTime);
            OnFeedChanged?.Invoke(CurrentFeed, maxFeed);
        }
        else
        {
            TakeDamage(starvationDamagePerSecond * Time.deltaTime);
        }
    }

    public bool TrySpendStamina(float amount)
    {
        if (CurrentStamina < amount) return false;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
        return true;
    }

    public void RegenerateStamina(float multiplier = 1f)
    {
        if (CurrentStamina >= maxStamina) return;

        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * multiplier * Time.deltaTime);
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }

    public void Eat(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentFeed = Mathf.Min(maxFeed, CurrentFeed + amount);
        OnFeedChanged?.Invoke(CurrentFeed, maxFeed);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
        {
            OnDied?.Invoke();
        }
    }

    public void RestoreStats(float health, float feed, float stamina)
    {
        isRestoredFromSave = true;

        float safeHealth = health <= 0f ? maxHealth : health;
        CurrentHealth = Mathf.Clamp(safeHealth, 1f, maxHealth);
        CurrentFeed = Mathf.Clamp(feed, 0f, maxFeed);
        CurrentStamina = Mathf.Clamp(stamina, 0f, maxStamina);

        if (TryGetComponent(out PlayerMovement movement))
        {
            movement.ReviveMovement();
        }

        NotifyAll();
    }

    private void NotifyAll()
    {
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnFeedChanged?.Invoke(CurrentFeed, maxFeed);
        OnStaminaChanged?.Invoke(CurrentStamina, maxStamina);
    }
}