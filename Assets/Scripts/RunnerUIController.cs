using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunnerUIController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    private RunnerHealth health;
    private RunnerDifficulty difficulty;
    private RunnerScore score;
    private RunnerPowerups powerups;
    private const float HudStatusRefreshInterval = 0.10f;

    private GameObject statusPanel;
    private Text hpText;
    private Image hpBarFill;
    private Image hpShieldBaseFill;
    private Image hpShieldTopFill;
    private Image hpRegenPulse;
    private Coroutine hpRegenPulseCoroutine;
    private Text shieldText;
    private Text boostText;
    private Text statusText;
    private Text scoreText;
    private Text multiplierText;
    private GameObject gameOverPanel;
    private Text gameOverText;
    private Button restartButton;
    private Image fadeImage;
    private Image damageFlashImage;
    private Coroutine damageFlashCoroutine;
    private Text controlsHintText;
    private Coroutine controlsHintFadeCoroutine;

    private float hpFillTarget;
    private float hpFillVelocity;
    private float hpShieldBaseTarget;
    private float hpShieldTopTarget;
    private float hpShieldBaseVelocity;
    private float hpShieldTopVelocity;
    private RectTransform hpBarBgRt;
    private Image hpRegenShine;
    private Coroutine hpRegenShineCoroutine;
    private float nextHudStatusRefreshAt;

    private Action restartRequested;
    private static Sprite whiteSprite;

    public Canvas Canvas => canvas;
    public Image FadeImage => fadeImage;

    public void FlashDamage(float duration, float alpha)
    {
        FlashScreenTint(new Color(1f, 0.12f, 0.12f, 1f), duration, alpha);
    }

    public void FlashShieldBlock(float duration, float alpha)
    {
        FlashScreenTint(new Color(0.20f, 0.58f, 1f, 1f), duration, alpha);
    }

    public void Initialize(Action onRestartRequested)
    {
        restartRequested = onRestartRequested;
        EnsureUI();
        HideGameOver();
    }

    public void SetHudVisible(bool visible)
    {
        if (statusPanel != null) statusPanel.SetActive(visible);
        if (scoreText != null) scoreText.gameObject.SetActive(visible);
    }

    public void Bind(RunnerHealth playerHealth, RunnerDifficulty runnerDifficulty, RunnerScore runnerScore)
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        if (health != null) health.Healed -= OnHealed;
        if (score != null) score.ScoreChanged -= OnScoreChanged;
        if (score != null) score.MultiplierChanged -= OnMultiplierChanged;
        if (powerups != null) powerups.ShieldChargesChanged -= OnShieldChargesChanged;
        if (powerups != null) powerups.BoostsChanged -= OnBoostsChanged;

        health = playerHealth;
        difficulty = runnerDifficulty;
        score = runnerScore;
        powerups = health != null ? health.GetComponent<RunnerPowerups>() : null;

        if (health != null) health.HealthChanged += OnHealthChanged;
        if (health != null) health.Healed += OnHealed;
        if (score != null) score.ScoreChanged += OnScoreChanged;
        if (score != null) score.MultiplierChanged += OnMultiplierChanged;
        if (powerups != null) powerups.ShieldChargesChanged += OnShieldChargesChanged;
        if (powerups != null) powerups.BoostsChanged += OnBoostsChanged;

        RefreshHud();
    }

    public void ShowGameOver(int finalScore)
    {
        EnsureUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = $"GAME OVER\n\nScore: {finalScore}\n\nPress R or click Restart";
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (statusText == null) return;
        if (Time.unscaledTime < nextHudStatusRefreshAt) return;
        RefreshRuntimeStatus();
        nextHudStatusRefreshAt = Time.unscaledTime + HudStatusRefreshInterval;
    }

    private void OnDestroy()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        if (health != null) health.Healed -= OnHealed;
        if (score != null) score.ScoreChanged -= OnScoreChanged;
        if (score != null) score.MultiplierChanged -= OnMultiplierChanged;
        if (powerups != null) powerups.ShieldChargesChanged -= OnShieldChargesChanged;
        if (powerups != null) powerups.BoostsChanged -= OnBoostsChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        RefreshHealthStatus();
    }

    private void OnScoreChanged(int current)
    {
        if (scoreText != null) scoreText.text = $"{current}";
    }

    private void OnMultiplierChanged(float m)
    {
        if (multiplierText != null) multiplierText.text = $"x{m:0.00}";
        PulseMultiplier();
    }

    private void OnHealed(int current, int max)
    {
        RefreshHealthStatus();
        PulseRegenBar();
    }

    private void OnShieldChargesChanged(int charges)
    {
        RefreshHealthStatus();
    }

    private void OnBoostsChanged()
    {
        RefreshBoostStatus();
    }

    private void RefreshHud()
    {
        if (statusText == null) return;

        RefreshHealthStatus();
        RefreshRuntimeStatus();
        RefreshBoostStatus();
        nextHudStatusRefreshAt = Time.unscaledTime + HudStatusRefreshInterval;
        if (scoreText != null) scoreText.text = $"{(score != null ? score.Score : 0)}";
        if (multiplierText != null) multiplierText.text = $"x{(score != null ? score.Multiplier : 1f):0.00}";
    }

    private void RefreshHealthStatus()
    {
        int hp = health != null ? health.CurrentHealth : 0;
        int maxHp = health != null ? health.MaxHealth : 0;
        int shieldCharges = powerups != null ? powerups.ShieldCharges : 0;

        if (hpText != null) hpText.text = $"HP {hp}/{maxHp}";
        if (shieldText != null) shieldText.text = $"SH {shieldCharges}";
        hpFillTarget = maxHp > 0 ? (float)hp / maxHp : 0f;
        RefreshShieldBar(shieldCharges, maxHp);
    }

    private void RefreshRuntimeStatus()
    {
        if (statusText == null) return;
        float t = difficulty != null ? difficulty.Elapsed : 0f;
        float speed = difficulty != null ? difficulty.CurrentSpeed : 0f;
        statusText.text = $"Time: {t:0.0}s   Speed: {speed:0.0}";
        RefreshBoostStatus();
    }

    private void RefreshBoostStatus()
    {
        if (boostText == null) return;
        int wallBreak = powerups != null ? powerups.WallBreakCharges : 0;
        int jumpBoost = powerups != null ? powerups.JumpBoostCharges : 0;
        float cooldown = powerups != null ? powerups.JumpBoostCooldownRemaining : 0f;
        string cooldownLabel = cooldown > 0f ? $"{cooldown:0.0}s" : "ready";
        boostText.text = $"E: WallBreak {wallBreak}   Air Space: JumpBoost {jumpBoost} ({cooldownLabel})";
    }

    private void EnsureUI()
    {
        if (canvas != null) return;

        var canvasGo = new GameObject("RunnerCanvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        statusPanel = CreatePanel(canvas.transform, "StatusPanel", new Color(0.05f, 0.02f, 0.09f, 0.42f));
        var panelRt2 = (RectTransform)statusPanel.transform;
        panelRt2.anchorMin = new Vector2(0f, 1f);
        panelRt2.anchorMax = new Vector2(0f, 1f);
        panelRt2.pivot = new Vector2(0f, 1f);
        panelRt2.anchoredPosition = new Vector2(12f, -12f);
        panelRt2.sizeDelta = new Vector2(300f, 92f);

        hpText = CreateText(statusPanel.transform, "HPText", 14, TextAnchor.UpperLeft);
        var hpTextRt = (RectTransform)hpText.transform;
        hpTextRt.anchorMin = new Vector2(0f, 1f);
        hpTextRt.anchorMax = new Vector2(0f, 1f);
        hpTextRt.pivot = new Vector2(0f, 1f);
        hpTextRt.anchoredPosition = new Vector2(10f, -8f);
        hpTextRt.sizeDelta = new Vector2(120f, 18f);

        shieldText = CreateText(statusPanel.transform, "ShieldText", 14, TextAnchor.UpperRight);
        shieldText.fontStyle = FontStyle.Bold;
        shieldText.color = new Color(0.28f, 0.92f, 1f, 0.98f);
        var shieldTextRt = (RectTransform)shieldText.transform;
        shieldTextRt.anchorMin = new Vector2(0f, 1f);
        shieldTextRt.anchorMax = new Vector2(0f, 1f);
        shieldTextRt.pivot = new Vector2(1f, 1f);
        shieldTextRt.anchoredPosition = new Vector2(290f, -8f);
        shieldTextRt.sizeDelta = new Vector2(84f, 18f);

        var hpBarBg = CreatePanel(statusPanel.transform, "HPBarBG", new Color(1f, 1f, 1f, 0.20f));
        var hpBarBgImg = hpBarBg.GetComponent<Image>();
        hpBarBgImg.sprite = GetWhiteSprite();

        var hpBarBgRt = (RectTransform)hpBarBg.transform;
        hpBarBgRt.anchorMin = new Vector2(0f, 1f);
        hpBarBgRt.anchorMax = new Vector2(0f, 1f);
        hpBarBgRt.pivot = new Vector2(0f, 1f);
        hpBarBgRt.anchoredPosition = new Vector2(10f, -28f);
        hpBarBgRt.sizeDelta = new Vector2(280f, 12f);
        this.hpBarBgRt = hpBarBgRt;
        hpBarBg.AddComponent<RectMask2D>();

        var hpFillGo = CreatePanel(hpBarBg.transform, "HPBarFill", new Color(0.18f, 1f, 0.42f, 0.95f));
        hpBarFill = hpFillGo.GetComponent<Image>();
        hpBarFill.sprite = GetWhiteSprite();
        hpBarFill.type = Image.Type.Filled;
        hpBarFill.fillMethod = Image.FillMethod.Horizontal;
        hpBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpBarFill.fillAmount = 1f;

        var hpFillRt = (RectTransform)hpFillGo.transform;
        hpFillRt.anchorMin = Vector2.zero;
        hpFillRt.anchorMax = Vector2.one;
        hpFillRt.offsetMin = Vector2.zero;
        hpFillRt.offsetMax = Vector2.zero;

        var hpShieldBaseGo = CreatePanel(hpBarBg.transform, "HPShieldBaseFill", new Color(0f, 0.90f, 1f, 0f));
        hpShieldBaseFill = hpShieldBaseGo.GetComponent<Image>();
        hpShieldBaseFill.sprite = GetWhiteSprite();
        hpShieldBaseFill.type = Image.Type.Filled;
        hpShieldBaseFill.fillMethod = Image.FillMethod.Horizontal;
        hpShieldBaseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpShieldBaseFill.fillAmount = 0f;
        var hpShieldBaseRt = (RectTransform)hpShieldBaseGo.transform;
        hpShieldBaseRt.anchorMin = Vector2.zero;
        hpShieldBaseRt.anchorMax = Vector2.one;
        hpShieldBaseRt.offsetMin = Vector2.zero;
        hpShieldBaseRt.offsetMax = Vector2.zero;

        var hpShieldTopGo = CreatePanel(hpBarBg.transform, "HPShieldTopFill", new Color(0f, 0.65f, 1f, 0f));
        hpShieldTopFill = hpShieldTopGo.GetComponent<Image>();
        hpShieldTopFill.sprite = GetWhiteSprite();
        hpShieldTopFill.type = Image.Type.Filled;
        hpShieldTopFill.fillMethod = Image.FillMethod.Horizontal;
        hpShieldTopFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpShieldTopFill.fillAmount = 0f;
        var hpShieldTopRt = (RectTransform)hpShieldTopGo.transform;
        hpShieldTopRt.anchorMin = Vector2.zero;
        hpShieldTopRt.anchorMax = Vector2.one;
        hpShieldTopRt.offsetMin = Vector2.zero;
        hpShieldTopRt.offsetMax = Vector2.zero;

        var hpPulseGo = CreatePanel(hpBarBg.transform, "HPRegenPulse", new Color(0.18f, 1f, 0.42f, 0f));
        hpRegenPulse = hpPulseGo.GetComponent<Image>();
        var hpPulseRt = (RectTransform)hpPulseGo.transform;
        hpPulseRt.anchorMin = Vector2.zero;
        hpPulseRt.anchorMax = Vector2.one;
        hpPulseRt.offsetMin = Vector2.zero;
        hpPulseRt.offsetMax = Vector2.zero;

        var hpShineGo = CreatePanel(hpBarBg.transform, "HPRegenShine", new Color(0.8f, 1f, 0.85f, 0f));
        hpRegenShine = hpShineGo.GetComponent<Image>();
        var hpShineRt = (RectTransform)hpShineGo.transform;
        hpShineRt.anchorMin = new Vector2(0f, 0f);
        hpShineRt.anchorMax = new Vector2(0f, 1f);
        hpShineRt.pivot = new Vector2(0.5f, 0.5f);
        hpShineRt.anchoredPosition = Vector2.zero;
        hpShineRt.sizeDelta = new Vector2(40f, 0f);

        statusText = CreateText(statusPanel.transform, "StatusText", 14, TextAnchor.UpperLeft);
        var statusRt = (RectTransform)statusText.transform;
        statusRt.anchorMin = new Vector2(0f, 1f);
        statusRt.anchorMax = new Vector2(0f, 1f);
        statusRt.pivot = new Vector2(0f, 1f);
        statusRt.anchoredPosition = new Vector2(10f, -46f);
        statusRt.sizeDelta = new Vector2(280f, 20f);

        boostText = CreateText(statusPanel.transform, "BoostText", 13, TextAnchor.UpperLeft);
        boostText.color = new Color(1f, 1f, 1f, 0.92f);
        var boostRt = (RectTransform)boostText.transform;
        boostRt.anchorMin = new Vector2(0f, 1f);
        boostRt.anchorMax = new Vector2(0f, 1f);
        boostRt.pivot = new Vector2(0f, 1f);
        boostRt.anchoredPosition = new Vector2(10f, -64f);
        boostRt.sizeDelta = new Vector2(280f, 18f);

        scoreText = CreateText(canvas.transform, "ScoreText", 32, TextAnchor.UpperCenter);
        scoreText.fontStyle = FontStyle.Bold;
        var scoreRt = (RectTransform)scoreText.transform;
        scoreRt.anchorMin = new Vector2(0.5f, 1f);
        scoreRt.anchorMax = new Vector2(0.5f, 1f);
        scoreRt.pivot = new Vector2(0.5f, 1f);
        scoreRt.anchoredPosition = new Vector2(0f, -12f);
        scoreRt.sizeDelta = new Vector2(240f, 40f);

        multiplierText = CreateText(canvas.transform, "MultiplierText", 18, TextAnchor.UpperCenter);
        multiplierText.fontStyle = FontStyle.Bold;
        var multRt = (RectTransform)multiplierText.transform;
        multRt.anchorMin = new Vector2(0.5f, 1f);
        multRt.anchorMax = new Vector2(0.5f, 1f);
        multRt.pivot = new Vector2(0.5f, 1f);
        multRt.anchoredPosition = new Vector2(0f, -52f);
        multRt.sizeDelta = new Vector2(240f, 26f);

        controlsHintText = CreateText(canvas.transform, "ControlsHintText", 18, TextAnchor.LowerCenter);
        controlsHintText.fontStyle = FontStyle.Bold;
        controlsHintText.color = new Color(1f, 1f, 1f, 0f);
        var hintRt = (RectTransform)controlsHintText.transform;
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 18f);
        hintRt.sizeDelta = new Vector2(900f, 44f);

        gameOverPanel = CreatePanel(canvas.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.65f));
        var panelRt = (RectTransform)gameOverPanel.transform;
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(420f, 260f);

        gameOverText = CreateText(gameOverPanel.transform, "GameOverText", 22, TextAnchor.MiddleCenter);
        var gotRt = (RectTransform)gameOverText.transform;
        gotRt.anchorMin = new Vector2(0f, 0f);
        gotRt.anchorMax = new Vector2(1f, 1f);
        gotRt.offsetMin = new Vector2(16f, 58f);
        gotRt.offsetMax = new Vector2(-16f, -16f);

        restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Restart");
        var btnRt = (RectTransform)restartButton.transform;
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 16f);
        btnRt.sizeDelta = new Vector2(180f, 36f);

        restartButton.onClick.AddListener(() => restartRequested?.Invoke());

        damageFlashImage = CreateDamageFlash(canvas.transform);
        fadeImage = CreateFade(canvas.transform);
    }

    private void LateUpdate()
    {
        if (hpBarFill != null)
        {
            float current = hpBarFill.fillAmount;
            float target = hpFillTarget;
            float smoothTime = target >= current ? 0.14f : 0.08f;
            hpBarFill.fillAmount = Mathf.SmoothDamp(current, target, ref hpFillVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        if (hpShieldBaseFill != null)
        {
            float current = hpShieldBaseFill.fillAmount;
            float target = hpShieldBaseTarget;
            hpShieldBaseFill.fillAmount = Mathf.SmoothDamp(current, target, ref hpShieldBaseVelocity, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        if (hpShieldTopFill != null)
        {
            float current = hpShieldTopFill.fillAmount;
            float target = hpShieldTopTarget;
            hpShieldTopFill.fillAmount = Mathf.SmoothDamp(current, target, ref hpShieldTopVelocity, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }

    private void RefreshShieldBar(int shieldCharges, int maxHp)
    {
        if (hpShieldBaseFill == null || hpShieldTopFill == null) return;

        int charges = Mathf.Max(0, shieldCharges);
        if (charges <= 0 || maxHp <= 0)
        {
            hpShieldBaseTarget = 0f;
            hpShieldTopTarget = 0f;
            return;
        }

        int hpPerLayer = Mathf.Max(1, maxHp);
        int completedLayers = charges / hpPerLayer;
        int remainder = charges % hpPerLayer;

        if (remainder == 0)
        {
            int fullLayerIndex = Mathf.Max(0, completedLayers - 1);
            hpShieldBaseFill.color = ShieldLayerColor(fullLayerIndex, 1f);
            hpShieldBaseTarget = 1f;
            hpShieldTopTarget = 0f;
            return;
        }

        if (completedLayers > 0)
        {
            hpShieldBaseFill.color = ShieldLayerColor(completedLayers - 1, 1f);
            hpShieldBaseTarget = 1f;
        }
        else
        {
            hpShieldBaseTarget = 0f;
        }

        hpShieldTopFill.color = ShieldLayerColor(completedLayers, 1f);
        hpShieldTopTarget = (float)remainder / hpPerLayer;
    }

    private static Color ShieldLayerColor(int layerIndex, float alpha)
    {
        float t = 1f - Mathf.Pow(0.80f, Mathf.Max(0, layerIndex));
        Color bright = new Color(0.18f, 0.64f, 1f, alpha);
        Color dark = new Color(0.04f, 0.19f, 0.66f, alpha);
        return Color.Lerp(bright, dark, t);
    }

    public void ShowControlsHint(string text)
    {
        EnsureUI();
        if (controlsHintText == null) return;

        controlsHintText.text = text ?? string.Empty;
        SetControlsHintAlpha(1f);

        if (controlsHintFadeCoroutine != null)
        {
            StopCoroutine(controlsHintFadeCoroutine);
            controlsHintFadeCoroutine = null;
        }
    }

    public void HideControlsHint(float fadeSeconds)
    {
        EnsureUI();
        if (controlsHintText == null) return;

        if (controlsHintFadeCoroutine != null) StopCoroutine(controlsHintFadeCoroutine);
        controlsHintFadeCoroutine = StartCoroutine(ControlsHintFadeRoutine(Mathf.Max(0f, fadeSeconds)));
    }

    private IEnumerator ControlsHintFadeRoutine(float duration)
    {
        if (duration <= 0f)
        {
            SetControlsHintAlpha(0f);
            controlsHintFadeCoroutine = null;
            yield break;
        }

        float start = controlsHintText.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            SetControlsHintAlpha(a);
            yield return null;
        }

        SetControlsHintAlpha(0f);
        controlsHintFadeCoroutine = null;
    }

    private void SetControlsHintAlpha(float a)
    {
        if (controlsHintText == null) return;
        var c = controlsHintText.color;
        c.a = Mathf.Clamp01(a);
        controlsHintText.color = c;
    }

    private void PulseRegenBar()
    {
        if (hpRegenPulse == null) return;
        if (hpRegenPulseCoroutine != null) StopCoroutine(hpRegenPulseCoroutine);
        hpRegenPulseCoroutine = StartCoroutine(RegenPulseRoutine());

        if (hpRegenShineCoroutine != null) StopCoroutine(hpRegenShineCoroutine);
        hpRegenShineCoroutine = StartCoroutine(RegenShineRoutine());
    }

    private IEnumerator RegenPulseRoutine()
    {
        if (hpRegenPulse == null)
        {
            hpRegenPulseCoroutine = null;
            yield break;
        }

        float d = 0.28f;
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            float a = Mathf.Sin(u * Mathf.PI) * 0.55f;
            var c = hpRegenPulse.color;
            c.a = a;
            hpRegenPulse.color = c;
            yield return null;
        }

        var cc = hpRegenPulse.color;
        cc.a = 0f;
        hpRegenPulse.color = cc;
        hpRegenPulseCoroutine = null;
    }

    private IEnumerator RegenShineRoutine()
    {
        if (hpRegenShine == null || hpBarBgRt == null)
        {
            hpRegenShineCoroutine = null;
            yield break;
        }

        var rt = (RectTransform)hpRegenShine.transform;
        float width = hpBarBgRt.rect.width;
        float shineW = rt.sizeDelta.x;
        float startX = -shineW;
        float endX = width + shineW;

        float d = 0.32f;
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / d);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, u), 0f);

            float a = Mathf.Sin(u * Mathf.PI) * 0.38f;
            var c = hpRegenShine.color;
            c.a = a;
            hpRegenShine.color = c;
            yield return null;
        }

        var cc = hpRegenShine.color;
        cc.a = 0f;
        hpRegenShine.color = cc;
        hpRegenShineCoroutine = null;
    }

    private void PulseMultiplier()
    {
        if (multiplierText == null) return;
        multiplierText.transform.localScale = Vector3.one * 1.15f;
        StartCoroutine(MultiplierScaleDown());
    }

    private IEnumerator MultiplierScaleDown()
    {
        if (multiplierText == null) yield break;
        float d = 0.18f;
        float t = 0f;
        var start = multiplierText.transform.localScale;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            multiplierText.transform.localScale = Vector3.Lerp(start, Vector3.one, Mathf.Clamp01(t / d));
            yield return null;
        }
        multiplierText.transform.localScale = Vector3.one;
    }

    private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        return go;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = new Color(1f, 1f, 1f, 0.9f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var text = CreateText(go.transform, "Text", 18, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.black;
        var rt = (RectTransform)text.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return button;
    }

    private static Image CreateFade(Transform parent)
    {
        var go = new GameObject("Fade");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = false;

        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private static Image CreateDamageFlash(Transform parent)
    {
        var go = new GameObject("DamageFlash");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = new Color(1f, 0f, 0f, 0f);
        img.raycastTarget = false;

        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private void FlashScreenTint(Color tint, float duration, float alpha)
    {
        EnsureUI();
        if (damageFlashImage == null) return;

        if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
        damageFlashCoroutine = StartCoroutine(DamageFlashRoutine(tint, Mathf.Max(0f, duration), Mathf.Clamp01(alpha)));
    }

    private IEnumerator DamageFlashRoutine(Color tint, float duration, float alpha)
    {
        if (damageFlashImage == null) yield break;
        SetDamageColor(tint);

        if (duration <= 0f)
        {
            SetDamageAlpha(0f);
            damageFlashCoroutine = null;
            yield break;
        }

        float half = duration * 0.5f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a;
            if (t <= half) a = Mathf.Lerp(0f, alpha, Mathf.Clamp01(t / half));
            else a = Mathf.Lerp(alpha, 0f, Mathf.Clamp01((t - half) / half));
            SetDamageAlpha(a);
            yield return null;
        }

        SetDamageAlpha(0f);
        damageFlashCoroutine = null;
    }

    private void SetDamageColor(Color color)
    {
        if (damageFlashImage == null) return;
        var c = damageFlashImage.color;
        c.r = color.r;
        c.g = color.g;
        c.b = color.b;
        damageFlashImage.color = c;
    }

    private void SetDamageAlpha(float a)
    {
        if (damageFlashImage == null) return;
        var c = damageFlashImage.color;
        c.a = Mathf.Clamp01(a);
        damageFlashImage.color = c;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();

        var inputSystemUIModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUIModule != null)
        {
            esGo.AddComponent(inputSystemUIModule);
        }
        else
        {
            esGo.AddComponent<StandaloneInputModule>();
        }
    }
}
