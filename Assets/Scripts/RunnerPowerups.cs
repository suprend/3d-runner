using UnityEngine;

public class RunnerPowerups : MonoBehaviour
{
    [SerializeField] private int shieldCharges;
    [SerializeField] private int wallBreakCharges;
    [SerializeField] private int jumpBoostCharges;
    [SerializeField] private float jumpBoostCooldownUntil = -999f;

    private Renderer visualRenderer;
    private Color baseColor = Color.white;
    private bool hasBaseColor;
    private RunnerHealth health;

    public bool HasShield => shieldCharges > 0;
    public int ShieldCharges => Mathf.Max(0, shieldCharges);
    public int WallBreakCharges => Mathf.Max(0, wallBreakCharges);
    public int JumpBoostCharges => Mathf.Max(0, jumpBoostCharges);
    public float JumpBoostCooldownRemaining => Mathf.Max(0f, jumpBoostCooldownUntil - Time.time);
    public event System.Action<int> ShieldChargesChanged;
    public event System.Action BoostsChanged;

    public void Initialize(RunnerConfigSO runnerConfig)
    {
        shieldCharges = 0;
        wallBreakCharges = 0;
        jumpBoostCharges = 0;
        jumpBoostCooldownUntil = -999f;

        CacheVisualRenderer();
        ApplyTint();
    }

    public void AddShield(int charges = 1)
    {
        shieldCharges = Mathf.Max(0, shieldCharges) + Mathf.Max(1, charges);
        ShieldChargesChanged?.Invoke(ShieldCharges);
        BoostsChanged?.Invoke();
        ApplyTint();
    }

    public bool TryConsumeShield()
    {
        if (shieldCharges <= 0) return false;
        shieldCharges--;
        ShieldChargesChanged?.Invoke(ShieldCharges);
        BoostsChanged?.Invoke();
        ApplyTint();
        return true;
    }

    public void AddWallBreak(int charges = 1)
    {
        wallBreakCharges = Mathf.Max(0, wallBreakCharges) + Mathf.Max(1, charges);
        BoostsChanged?.Invoke();
    }

    public bool TryConsumeWallBreak()
    {
        if (wallBreakCharges <= 0) return false;
        wallBreakCharges--;
        BoostsChanged?.Invoke();
        return true;
    }

    public void AddJumpBoost(int charges = 1)
    {
        jumpBoostCharges = Mathf.Max(0, jumpBoostCharges) + Mathf.Max(1, charges);
        BoostsChanged?.Invoke();
    }

    public bool TryConsumeJumpBoost(float cooldownSeconds)
    {
        if (jumpBoostCharges <= 0) return false;
        if (Time.time < jumpBoostCooldownUntil) return false;
        jumpBoostCharges--;
        jumpBoostCooldownUntil = Time.time + Mathf.Max(0f, cooldownSeconds);
        BoostsChanged?.Invoke();
        return true;
    }

    private void CacheVisualRenderer()
    {
        if (health == null) health = GetComponent<RunnerHealth>();
        if (visualRenderer != null) return;
        var visual = transform.Find("Visual");
        if (visual != null) visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>(true);

        if (visualRenderer != null && !hasBaseColor)
        {
            baseColor = visualRenderer.material.color;
            hasBaseColor = true;
        }
    }

    private void ApplyTint()
    {
        CacheVisualRenderer();
        if (visualRenderer == null || !hasBaseColor) return;

        Color c = baseColor;

        if (HasShield)
        {
            int hpPerLayer = Mathf.Max(1, health != null ? health.MaxHealth : 3);
            int charges = Mathf.Max(0, shieldCharges);
            int completedLayers = charges / hpPerLayer;
            int remainder = charges % hpPerLayer;

            float progress = remainder > 0 ? (float)remainder / hpPerLayer : 1f;
            int layerIndex = remainder > 0 ? completedLayers : Mathf.Max(0, completedLayers - 1);

            float darknessT = 1f - Mathf.Pow(0.80f, layerIndex);
            Color brightBlue = new Color(0.20f, 0.68f, 1f, 1f);
            Color deepBlue = new Color(0.05f, 0.22f, 0.70f, 1f);
            Color targetBlue = Color.Lerp(brightBlue, deepBlue, darknessT);

            float intensity = Mathf.Clamp01(0.20f + 0.75f * progress);
            c = Color.Lerp(c, targetBlue, intensity);
        }

        visualRenderer.material.color = c;
    }
}
