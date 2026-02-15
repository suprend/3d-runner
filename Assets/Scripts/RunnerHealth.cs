using System;
using UnityEngine;

public class RunnerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth = 3;
    [SerializeField] private float baseInvulnerabilitySeconds = 0.35f;
    [SerializeField] private float invulnerableUntil = -999f;
    [SerializeField] private float lastHpDamageTime = -999f;

    [Header("Regen")]
    [SerializeField] private bool regenEnabled;
    [SerializeField] private float regenDelaySeconds = 8f;
    [SerializeField] private float regenIntervalSeconds = 6f;
    [SerializeField] private int regenAmount = 1;
    [SerializeField] private float lastRegenTime = -999f;

    private RunnerPowerups powerups;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    public event Action Died;
    public event Action<int, int> HealthChanged;
    public event Action<int, int, int> Damaged;
    public event Action<int, int> Healed;
    public event Action DamageBlocked;

    public void Initialize(int max)
    {
        Initialize(max, baseInvulnerabilitySeconds);
    }

    public void Initialize(int max, float invulnerability)
    {
        maxHealth = Mathf.Max(1, max);
        currentHealth = maxHealth;
        baseInvulnerabilitySeconds = Mathf.Max(0f, invulnerability);
        invulnerableUntil = -999f;
        lastHpDamageTime = -999f;
        lastRegenTime = -999f;
        if (powerups == null) powerups = GetComponent<RunnerPowerups>();
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetInvulnerabilitySeconds(float seconds)
    {
        baseInvulnerabilitySeconds = Mathf.Max(0f, seconds);
    }

    public void ExtendInvulnerability(float seconds)
    {
        if (seconds <= 0f) return;
        float until = Time.time + seconds;
        if (until > invulnerableUntil) invulnerableUntil = until;
    }

    public void ConfigureRegen(bool enabled, float delaySeconds, float intervalSeconds, int amount)
    {
        regenEnabled = enabled;
        regenDelaySeconds = Mathf.Max(0f, delaySeconds);
        regenIntervalSeconds = Mathf.Max(0.1f, intervalSeconds);
        regenAmount = Mathf.Max(1, amount);
    }

    private void Update()
    {
        if (!regenEnabled) return;
        if (IsDead) return;
        if (currentHealth >= maxHealth) return;

        float t = Time.time;
        if (t - lastHpDamageTime < regenDelaySeconds) return;
        if (t - lastRegenTime < regenIntervalSeconds) return;

        lastRegenTime = t;
        Heal(regenAmount);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (amount <= 0) return;
        if (Time.time < invulnerableUntil) return;

        if (powerups == null) powerups = GetComponent<RunnerPowerups>();
        if (powerups != null && powerups.TryConsumeShield())
        {
            invulnerableUntil = Time.time + baseInvulnerabilitySeconds;
            DamageBlocked?.Invoke();
            return;
        }

        invulnerableUntil = Time.time + baseInvulnerabilitySeconds;
        lastHpDamageTime = Time.time;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Damaged?.Invoke(amount, currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0) Died?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        if (amount <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (currentHealth == before) return;

        Healed?.Invoke(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
