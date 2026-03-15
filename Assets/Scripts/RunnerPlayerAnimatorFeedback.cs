using System.Collections;
using UnityEngine;

public class RunnerPlayerAnimatorFeedback : MonoBehaviour
{
    private const string AnimatorControllerResource = "RunnerPlayerFeedbackController";

    private static readonly int DamageTrigger = Animator.StringToHash("Damage");
    private static readonly int PickupTrigger = Animator.StringToHash("Pickup");
    private static readonly int ShieldActive = Animator.StringToHash("ShieldActive");

    [SerializeField] private RunnerBootstrap bootstrap;

    private RunnerHealth health;
    private RunnerPowerups powerups;
    private Animator animator;
    private Renderer auraRenderer;
    private Coroutine auraTintCoroutine;

    public void Initialize(RunnerBootstrap runnerBootstrap)
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        Unbind();

        bootstrap = runnerBootstrap;
        if (bootstrap != null) bootstrap.RunRecreated += OnRunRecreated;

        OnRunRecreated();
    }

    private void OnDestroy()
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        Unbind();
    }

    private void OnRunRecreated()
    {
        Unbind();
        if (bootstrap == null) return;

        var player = bootstrap.PlayerController != null ? bootstrap.PlayerController.transform : null;
        if (player == null) return;

        health = bootstrap.PlayerHealth;
        powerups = player.GetComponent<RunnerPowerups>();
        animator = player.GetComponent<Animator>();
        auraRenderer = FindAuraRenderer(player);

        EnsureAnimatorController();
        ApplyBaseAuraTint();
        UpdateShieldState();

        if (health != null) health.Damaged += OnDamaged;
        if (powerups != null) powerups.BonusCollected += OnBonusCollected;
        if (powerups != null) powerups.ShieldChargesChanged += OnShieldChargesChanged;
    }

    private void Unbind()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (powerups != null) powerups.BonusCollected -= OnBonusCollected;
        if (powerups != null) powerups.ShieldChargesChanged -= OnShieldChargesChanged;

        health = null;
        powerups = null;
        animator = null;
        auraRenderer = null;

        if (auraTintCoroutine != null)
        {
            StopCoroutine(auraTintCoroutine);
            auraTintCoroutine = null;
        }
    }

    private void EnsureAnimatorController()
    {
        if (animator == null) return;
        if (animator.runtimeAnimatorController != null) return;

        var controller = Resources.Load<RuntimeAnimatorController>(AnimatorControllerResource);
        if (controller == null) return;

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
    }

    private void OnDamaged(int amount, int current, int max)
    {
        if (animator != null) animator.SetTrigger(DamageTrigger);
        StartAuraTintPulse(new Color(1f, 0.22f, 0.22f, 0.45f), 0.22f);
    }

    private void OnBonusCollected(PickupKind kind)
    {
        if (animator != null) animator.SetTrigger(PickupTrigger);

        Color tint;
        switch (kind)
        {
            case PickupKind.Shield:
                tint = new Color(0.20f, 0.68f, 1f, 0.35f);
                break;
            case PickupKind.WallBreak:
                tint = new Color(1f, 0.70f, 0.18f, 0.35f);
                break;
            case PickupKind.JumpBoost:
                tint = new Color(0.45f, 1f, 0.45f, 0.35f);
                break;
            default:
                tint = new Color(1f, 1f, 1f, 0.3f);
                break;
        }

        StartAuraTintPulse(tint, 0.24f);
    }

    private void OnShieldChargesChanged(int charges)
    {
        UpdateShieldState();
    }

    private void UpdateShieldState()
    {
        bool active = powerups != null && powerups.ShieldCharges > 0;
        if (animator != null) animator.SetBool(ShieldActive, active);
        ApplyBaseAuraTint();
    }

    private void ApplyBaseAuraTint()
    {
        if (auraRenderer == null) return;
        bool shieldActive = powerups != null && powerups.ShieldCharges > 0;
        SetAuraColor(shieldActive ? new Color(0.18f, 0.62f, 1f, 0.24f) : new Color(1f, 1f, 1f, 0f));
    }

    private void StartAuraTintPulse(Color color, float duration)
    {
        if (auraRenderer == null) return;

        if (auraTintCoroutine != null) StopCoroutine(auraTintCoroutine);
        auraTintCoroutine = StartCoroutine(AuraTintPulseRoutine(color, duration));
    }

    private IEnumerator AuraTintPulseRoutine(Color color, float duration)
    {
        SetAuraColor(color);

        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
        }

        ApplyBaseAuraTint();
        auraTintCoroutine = null;
    }

    private void SetAuraColor(Color color)
    {
        if (auraRenderer == null) return;
        auraRenderer.material.color = color;
    }

    private static Renderer FindAuraRenderer(Transform player)
    {
        if (player == null) return null;
        var aura = player.Find("Visual/FeedbackAura");
        if (aura == null) aura = player.Find("FeedbackAura");
        return aura != null ? aura.GetComponent<Renderer>() : null;
    }
}
