using System.Collections;
using UnityEngine;

public class RunnerDamageFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private RunnerBootstrap bootstrap;

    private RunnerHealth health;
    private RunnerUIController ui;
    private RunnerCameraShake cameraShake;
    private Renderer[] playerRenderers;
    private Coroutine playerFlashCoroutine;
    private MaterialPropertyBlock propertyBlock;

    public void Initialize(RunnerBootstrap runnerBootstrap)
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        UnbindRuntime();

        bootstrap = runnerBootstrap;
        if (bootstrap != null) bootstrap.RunRecreated += OnRunRecreated;

        OnRunRecreated();
    }

    private void OnDestroy()
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        UnbindRuntime();
    }

    private void OnRunRecreated()
    {
        UnbindRuntime();

        if (bootstrap == null) return;

        health = bootstrap.PlayerHealth;
        ui = bootstrap.UI;

        if (health != null) health.Damaged += OnDamaged;
        if (health != null) health.DamageBlocked += OnDamageBlocked;

        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cameraShake = cam.GetComponent<RunnerCameraShake>();
            if (cameraShake == null) cameraShake = cam.gameObject.AddComponent<RunnerCameraShake>();
        }

        var player = bootstrap.PlayerController != null ? bootstrap.PlayerController.transform : null;
        playerRenderers = player != null ? player.GetComponentsInChildren<Renderer>(true) : null;
    }

    private void UnbindRuntime()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (health != null) health.DamageBlocked -= OnDamageBlocked;
        health = null;
        ui = null;
        cameraShake = null;
        playerRenderers = null;
    }

    private void OnDamaged(int amount, int current, int max)
    {
        if (bootstrap == null || bootstrap.Config == null) return;
        var cfg = bootstrap.Config;

        if (ui != null) ui.FlashDamage(cfg.damageScreenFlashSeconds, cfg.damageScreenFlashAlpha);
        if (cameraShake != null) cameraShake.Kick(cfg.damageShakeAmplitude, cfg.damageShakeSeconds);

        float blinkDuration = Mathf.Max(cfg.damageFlashSeconds, cfg.damageInvulnerabilitySeconds);
        if (health != null) health.ExtendInvulnerability(blinkDuration);

        if (playerFlashCoroutine != null)
        {
            StopCoroutine(playerFlashCoroutine);
            playerFlashCoroutine = null;
            RestorePlayerVisibility();
            ClearPlayerFlash();
        }
        playerFlashCoroutine = StartCoroutine(PlayerFlashRoutine(blinkDuration));
    }

    private void OnDamageBlocked()
    {
        if (bootstrap == null || bootstrap.Config == null) return;
        var cfg = bootstrap.Config;

        float d = Mathf.Max(0.06f, cfg.damageScreenFlashSeconds * 0.85f);
        float a = Mathf.Clamp01(cfg.damageScreenFlashAlpha * 0.85f);
        if (ui != null) ui.FlashShieldBlock(d, a);
    }

    private IEnumerator PlayerFlashRoutine(float duration)
    {
        if (playerRenderers == null || playerRenderers.Length == 0)
        {
            playerFlashCoroutine = null;
            yield break;
        }

        if (duration <= 0f)
        {
            RestorePlayerVisibility();
            ClearPlayerFlash();
            playerFlashCoroutine = null;
            yield break;
        }

        CacheOriginalVisibility();

        float interval = Mathf.Clamp(duration / 10f, 0.04f, 0.09f);
        float endsAt = Time.unscaledTime + duration;
        bool visible = true;
        Color flashColor = new Color(1f, 0.25f, 0.25f, 1f);

        while (Time.unscaledTime < endsAt)
        {
            visible = !visible;
            SetPlayerVisible(visible);
            if (visible) SetPlayerFlashColor(flashColor);
            else ClearPlayerFlash();

            yield return new WaitForSecondsRealtime(interval);
        }

        RestorePlayerVisibility();
        ClearPlayerFlash();
        playerFlashCoroutine = null;
    }

    private bool[] originalRendererEnabled;

    private void CacheOriginalVisibility()
    {
        if (playerRenderers == null) return;
        if (originalRendererEnabled == null || originalRendererEnabled.Length != playerRenderers.Length)
        {
            originalRendererEnabled = new bool[playerRenderers.Length];
        }

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            var r = playerRenderers[i];
            originalRendererEnabled[i] = r != null && r.enabled;
        }
    }

    private void RestorePlayerVisibility()
    {
        if (playerRenderers == null || originalRendererEnabled == null) return;
        int n = Mathf.Min(playerRenderers.Length, originalRendererEnabled.Length);
        for (int i = 0; i < n; i++)
        {
            var r = playerRenderers[i];
            if (r == null) continue;
            r.enabled = originalRendererEnabled[i];
        }
    }

    private void SetPlayerVisible(bool visible)
    {
        if (playerRenderers == null || originalRendererEnabled == null) return;
        int n = Mathf.Min(playerRenderers.Length, originalRendererEnabled.Length);
        for (int i = 0; i < n; i++)
        {
            var r = playerRenderers[i];
            if (r == null) continue;
            r.enabled = originalRendererEnabled[i] && visible;
        }
    }

    private void SetPlayerFlashColor(Color color)
    {
        if (playerRenderers == null || playerRenderers.Length == 0) return;

        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);

        for (int i = 0; i < playerRenderers.Length; i++)
        {
            var r = playerRenderers[i];
            if (r == null) continue;
            r.SetPropertyBlock(propertyBlock);
        }
    }

    private void ClearPlayerFlash()
    {
        if (playerRenderers == null || playerRenderers.Length == 0) return;
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            var r = playerRenderers[i];
            if (r == null) continue;
            r.SetPropertyBlock(null);
        }
    }
}
