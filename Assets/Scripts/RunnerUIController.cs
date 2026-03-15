using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RunnerUIController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    private RunnerConfigSO config;
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
    private Text bestText;

    private GameObject mainMenuPanel;
    private Text mainMenuTitleText;
    private Text mainMenuDeveloperText;
    private Text mainMenuBestText;
    private Button startButton;
    private Button quitButton;

    private GameObject pausePanel;
    private Text pauseTitleText;
    private Text pauseSummaryText;
    private Button resumeButton;
    private Button pauseMenuButton;

    private GameObject gameOverPanel;
    private Text gameOverText;
    private Button restartButton;
    private Button gameOverMenuButton;

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

    private Action startRequested;
    private Action restartRequested;
    private Action resumeRequested;
    private Action menuRequested;
    private Action quitRequested;
    private static Sprite whiteSprite;

    private int shownPauseScore;
    private int shownGameOverScore;
    private int cachedBestScore;

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

    public void Initialize(
        Action onStartRequested,
        Action onRestartRequested,
        Action onResumeRequested,
        Action onMenuRequested,
        Action onQuitRequested)
    {
        startRequested = onStartRequested;
        restartRequested = onRestartRequested;
        resumeRequested = onResumeRequested;
        menuRequested = onMenuRequested;
        quitRequested = onQuitRequested;

        EnsureUI();
        HideMainMenu();
        HidePauseMenu();
        HideGameOver();
    }

    public void ConfigurePresentation(RunnerConfigSO runnerConfig)
    {
        config = runnerConfig;
        EnsureUI();
        RefreshPresentationTexts();
    }

    public void SetHudVisible(bool visible)
    {
        if (statusPanel != null) statusPanel.SetActive(visible);
        if (scoreText != null) scoreText.gameObject.SetActive(visible);
        if (multiplierText != null) multiplierText.gameObject.SetActive(visible);
        if (bestText != null) bestText.gameObject.SetActive(visible);
    }

    public void Bind(RunnerHealth playerHealth, RunnerDifficulty runnerDifficulty, RunnerScore runnerScore, RunnerConfigSO runnerConfig)
    {
        UnbindEvents();

        config = runnerConfig;
        health = playerHealth;
        difficulty = runnerDifficulty;
        score = runnerScore;
        powerups = health != null ? health.GetComponent<RunnerPowerups>() : null;

        if (health != null) health.HealthChanged += OnHealthChanged;
        if (health != null) health.Healed += OnHealed;
        if (score != null) score.ScoreChanged += OnScoreChanged;
        if (score != null) score.BestScoreChanged += OnBestScoreChanged;
        if (score != null) score.MultiplierChanged += OnMultiplierChanged;
        if (powerups != null) powerups.ShieldChargesChanged += OnShieldChargesChanged;
        if (powerups != null) powerups.BoostsChanged += OnBoostsChanged;

        RefreshPresentationTexts();
        RefreshHud();
    }

    public void ShowMainMenu(int bestScoreValue)
    {
        EnsureUI();
        cachedBestScore = Mathf.Max(0, bestScoreValue);
        RefreshPresentationTexts();
        UpdateMainMenuBestText();
        HidePauseMenu();
        HideGameOver();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        SetHudVisible(false);
        SetControlsHintAlpha(0f);
        SelectButton(startButton);
    }

    public void HideMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    public void ShowPauseMenu(int currentScore, int bestScoreValue)
    {
        EnsureUI();
        shownPauseScore = Mathf.Max(0, currentScore);
        cachedBestScore = Mathf.Max(0, bestScoreValue);
        UpdatePauseSummaryText();
        if (pausePanel != null) pausePanel.SetActive(true);
        SelectButton(resumeButton);
    }

    public void HidePauseMenu()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void ShowGameOver(int finalScore, int bestScoreValue)
    {
        EnsureUI();
        shownGameOverScore = Mathf.Max(0, finalScore);
        cachedBestScore = Mathf.Max(0, bestScoreValue);
        UpdateGameOverText();
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        SelectButton(restartButton);
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

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void UnbindEvents()
    {
        if (health != null) health.HealthChanged -= OnHealthChanged;
        if (health != null) health.Healed -= OnHealed;
        if (score != null) score.ScoreChanged -= OnScoreChanged;
        if (score != null) score.BestScoreChanged -= OnBestScoreChanged;
        if (score != null) score.MultiplierChanged -= OnMultiplierChanged;
        if (powerups != null) powerups.ShieldChargesChanged -= OnShieldChargesChanged;
        if (powerups != null) powerups.BoostsChanged -= OnBoostsChanged;

        health = null;
        difficulty = null;
        score = null;
        powerups = null;
    }

    private void OnHealthChanged(int current, int max)
    {
        RefreshHealthStatus();
    }

    private void OnScoreChanged(int current)
    {
        if (scoreText != null) scoreText.text = $"{current}";
        if (pausePanel != null && pausePanel.activeSelf)
        {
            shownPauseScore = Mathf.Max(0, current);
            UpdatePauseSummaryText();
        }
    }

    private void OnBestScoreChanged(int current)
    {
        cachedBestScore = Mathf.Max(0, current);
        if (bestText != null) bestText.text = $"BEST {cachedBestScore}";
        UpdateMainMenuBestText();
        if (pausePanel != null && pausePanel.activeSelf) UpdatePauseSummaryText();
        if (gameOverPanel != null && gameOverPanel.activeSelf) UpdateGameOverText();
    }

    private void OnMultiplierChanged(float multiplier)
    {
        if (multiplierText != null) multiplierText.text = $"x{multiplier:0.00}";
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

        cachedBestScore = score != null ? score.BestScore : cachedBestScore;
        if (bestText != null) bestText.text = $"BEST {cachedBestScore}";
        RefreshPresentationTexts();
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
        float elapsed = difficulty != null ? difficulty.Elapsed : 0f;
        float speed = difficulty != null ? difficulty.CurrentSpeed : 0f;
        statusText.text = $"Time: {elapsed:0.0}s   Speed: {speed:0.0}";
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

    private void RefreshPresentationTexts()
    {
        if (mainMenuTitleText != null)
        {
            mainMenuTitleText.text = config != null && !string.IsNullOrWhiteSpace(config.gameTitle)
                ? config.gameTitle
                : "3D Runner";
        }

        if (mainMenuDeveloperText != null)
        {
            string name = config != null && !string.IsNullOrWhiteSpace(config.developerName)
                ? config.developerName
                : "Developer";
            mainMenuDeveloperText.text = $"by {name}";
        }

        if (pauseTitleText != null)
        {
            pauseTitleText.text = "PAUSED";
        }

        if (bestText != null)
        {
            bestText.text = $"BEST {cachedBestScore}";
        }

        UpdateMainMenuBestText();
    }

    private void UpdateMainMenuBestText()
    {
        if (mainMenuBestText == null) return;
        mainMenuBestText.text = $"Best Score: {cachedBestScore}";
    }

    private void UpdatePauseSummaryText()
    {
        if (pauseSummaryText == null) return;
        pauseSummaryText.text = $"Score: {shownPauseScore}\nBest: {cachedBestScore}";
    }

    private void UpdateGameOverText()
    {
        if (gameOverText == null) return;
        gameOverText.text = $"GAME OVER\n\nScore: {shownGameOverScore}\nBest: {cachedBestScore}";
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
        var panelRt = (RectTransform)statusPanel.transform;
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(12f, -12f);
        panelRt.sizeDelta = new Vector2(300f, 92f);

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
        var hpBarBgImage = hpBarBg.GetComponent<Image>();
        hpBarBgImage.sprite = GetWhiteSprite();

        var hpBarBgRect = (RectTransform)hpBarBg.transform;
        hpBarBgRect.anchorMin = new Vector2(0f, 1f);
        hpBarBgRect.anchorMax = new Vector2(0f, 1f);
        hpBarBgRect.pivot = new Vector2(0f, 1f);
        hpBarBgRect.anchoredPosition = new Vector2(10f, -28f);
        hpBarBgRect.sizeDelta = new Vector2(280f, 12f);
        hpBarBg.AddComponent<RectMask2D>();
        hpBarBgRt = hpBarBgRect;

        var hpFillGo = CreatePanel(hpBarBg.transform, "HPBarFill", new Color(0.18f, 1f, 0.42f, 0.95f));
        hpBarFill = hpFillGo.GetComponent<Image>();
        hpBarFill.sprite = GetWhiteSprite();
        hpBarFill.type = Image.Type.Filled;
        hpBarFill.fillMethod = Image.FillMethod.Horizontal;
        hpBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpBarFill.fillAmount = 1f;
        StretchToParent(hpFillGo.transform);

        var hpShieldBaseGo = CreatePanel(hpBarBg.transform, "HPShieldBaseFill", new Color(0f, 0.90f, 1f, 0f));
        hpShieldBaseFill = hpShieldBaseGo.GetComponent<Image>();
        hpShieldBaseFill.sprite = GetWhiteSprite();
        hpShieldBaseFill.type = Image.Type.Filled;
        hpShieldBaseFill.fillMethod = Image.FillMethod.Horizontal;
        hpShieldBaseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpShieldBaseFill.fillAmount = 0f;
        StretchToParent(hpShieldBaseGo.transform);

        var hpShieldTopGo = CreatePanel(hpBarBg.transform, "HPShieldTopFill", new Color(0f, 0.65f, 1f, 0f));
        hpShieldTopFill = hpShieldTopGo.GetComponent<Image>();
        hpShieldTopFill.sprite = GetWhiteSprite();
        hpShieldTopFill.type = Image.Type.Filled;
        hpShieldTopFill.fillMethod = Image.FillMethod.Horizontal;
        hpShieldTopFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hpShieldTopFill.fillAmount = 0f;
        StretchToParent(hpShieldTopGo.transform);

        var hpPulseGo = CreatePanel(hpBarBg.transform, "HPRegenPulse", new Color(0.18f, 1f, 0.42f, 0f));
        hpRegenPulse = hpPulseGo.GetComponent<Image>();
        StretchToParent(hpPulseGo.transform);

        var hpShineGo = CreatePanel(hpBarBg.transform, "HPRegenShine", new Color(0.8f, 1f, 0.85f, 0f));
        hpRegenShine = hpShineGo.GetComponent<Image>();
        var hpShineRt = (RectTransform)hpRegenShine.transform;
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
        var multiplierRt = (RectTransform)multiplierText.transform;
        multiplierRt.anchorMin = new Vector2(0.5f, 1f);
        multiplierRt.anchorMax = new Vector2(0.5f, 1f);
        multiplierRt.pivot = new Vector2(0.5f, 1f);
        multiplierRt.anchoredPosition = new Vector2(0f, -52f);
        multiplierRt.sizeDelta = new Vector2(240f, 26f);

        bestText = CreateText(canvas.transform, "BestText", 18, TextAnchor.UpperRight);
        bestText.fontStyle = FontStyle.Bold;
        var bestRt = (RectTransform)bestText.transform;
        bestRt.anchorMin = new Vector2(1f, 1f);
        bestRt.anchorMax = new Vector2(1f, 1f);
        bestRt.pivot = new Vector2(1f, 1f);
        bestRt.anchoredPosition = new Vector2(-16f, -16f);
        bestRt.sizeDelta = new Vector2(220f, 26f);

        controlsHintText = CreateText(canvas.transform, "ControlsHintText", 18, TextAnchor.LowerCenter);
        controlsHintText.fontStyle = FontStyle.Bold;
        controlsHintText.color = new Color(1f, 1f, 1f, 0f);
        var hintRt = (RectTransform)controlsHintText.transform;
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 18f);
        hintRt.sizeDelta = new Vector2(900f, 44f);

        CreateMainMenu();
        CreatePauseMenu();
        CreateGameOverMenu();

        damageFlashImage = CreateDamageFlash(canvas.transform);
        fadeImage = CreateFade(canvas.transform);
        RefreshPresentationTexts();
    }

    private void CreateMainMenu()
    {
        mainMenuPanel = CreatePanel(canvas.transform, "MainMenuPanel", new Color(0.02f, 0.02f, 0.06f, 0.92f));
        StretchToParent(mainMenuPanel.transform);

        mainMenuTitleText = CreateText(mainMenuPanel.transform, "Title", 44, TextAnchor.MiddleCenter);
        mainMenuTitleText.fontStyle = FontStyle.Bold;
        var titleRt = (RectTransform)mainMenuTitleText.transform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 140f);
        titleRt.sizeDelta = new Vector2(700f, 60f);

        mainMenuDeveloperText = CreateText(mainMenuPanel.transform, "Developer", 20, TextAnchor.MiddleCenter);
        var developerRt = (RectTransform)mainMenuDeveloperText.transform;
        developerRt.anchorMin = new Vector2(0.5f, 0.5f);
        developerRt.anchorMax = new Vector2(0.5f, 0.5f);
        developerRt.pivot = new Vector2(0.5f, 0.5f);
        developerRt.anchoredPosition = new Vector2(0f, 92f);
        developerRt.sizeDelta = new Vector2(500f, 30f);

        mainMenuBestText = CreateText(mainMenuPanel.transform, "BestScore", 26, TextAnchor.MiddleCenter);
        mainMenuBestText.fontStyle = FontStyle.Bold;
        var menuBestRt = (RectTransform)mainMenuBestText.transform;
        menuBestRt.anchorMin = new Vector2(0.5f, 0.5f);
        menuBestRt.anchorMax = new Vector2(0.5f, 0.5f);
        menuBestRt.pivot = new Vector2(0.5f, 0.5f);
        menuBestRt.anchoredPosition = new Vector2(0f, 22f);
        menuBestRt.sizeDelta = new Vector2(400f, 36f);

        startButton = CreateButton(mainMenuPanel.transform, "StartButton", "Start Game");
        var startRt = (RectTransform)startButton.transform;
        startRt.anchorMin = new Vector2(0.5f, 0.5f);
        startRt.anchorMax = new Vector2(0.5f, 0.5f);
        startRt.pivot = new Vector2(0.5f, 0.5f);
        startRt.anchoredPosition = new Vector2(0f, -52f);
        startRt.sizeDelta = new Vector2(220f, 42f);
        startButton.onClick.AddListener(() => startRequested?.Invoke());

        quitButton = CreateButton(mainMenuPanel.transform, "QuitButton", "Quit");
        var quitRt = (RectTransform)quitButton.transform;
        quitRt.anchorMin = new Vector2(0.5f, 0.5f);
        quitRt.anchorMax = new Vector2(0.5f, 0.5f);
        quitRt.pivot = new Vector2(0.5f, 0.5f);
        quitRt.anchoredPosition = new Vector2(0f, -104f);
        quitRt.sizeDelta = new Vector2(220f, 42f);
        quitButton.onClick.AddListener(() => quitRequested?.Invoke());
    }

    private void CreatePauseMenu()
    {
        pausePanel = CreatePanel(canvas.transform, "PausePanel", new Color(0f, 0f, 0f, 0.72f));
        StretchToParent(pausePanel.transform);

        pauseTitleText = CreateText(pausePanel.transform, "PauseTitle", 34, TextAnchor.MiddleCenter);
        pauseTitleText.fontStyle = FontStyle.Bold;
        var titleRt = (RectTransform)pauseTitleText.transform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 120f);
        titleRt.sizeDelta = new Vector2(320f, 42f);

        pauseSummaryText = CreateText(pausePanel.transform, "PauseSummary", 24, TextAnchor.MiddleCenter);
        var summaryRt = (RectTransform)pauseSummaryText.transform;
        summaryRt.anchorMin = new Vector2(0.5f, 0.5f);
        summaryRt.anchorMax = new Vector2(0.5f, 0.5f);
        summaryRt.pivot = new Vector2(0.5f, 0.5f);
        summaryRt.anchoredPosition = new Vector2(0f, 38f);
        summaryRt.sizeDelta = new Vector2(340f, 72f);

        resumeButton = CreateButton(pausePanel.transform, "ResumeButton", "Continue");
        var resumeRt = (RectTransform)resumeButton.transform;
        resumeRt.anchorMin = new Vector2(0.5f, 0.5f);
        resumeRt.anchorMax = new Vector2(0.5f, 0.5f);
        resumeRt.pivot = new Vector2(0.5f, 0.5f);
        resumeRt.anchoredPosition = new Vector2(0f, -44f);
        resumeRt.sizeDelta = new Vector2(220f, 42f);
        resumeButton.onClick.AddListener(() => resumeRequested?.Invoke());

        pauseMenuButton = CreateButton(pausePanel.transform, "PauseMenuButton", "Back To Menu");
        var pauseMenuRt = (RectTransform)pauseMenuButton.transform;
        pauseMenuRt.anchorMin = new Vector2(0.5f, 0.5f);
        pauseMenuRt.anchorMax = new Vector2(0.5f, 0.5f);
        pauseMenuRt.pivot = new Vector2(0.5f, 0.5f);
        pauseMenuRt.anchoredPosition = new Vector2(0f, -96f);
        pauseMenuRt.sizeDelta = new Vector2(220f, 42f);
        pauseMenuButton.onClick.AddListener(() => menuRequested?.Invoke());

        pausePanel.SetActive(false);
    }

    private void CreateGameOverMenu()
    {
        gameOverPanel = CreatePanel(canvas.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.78f));
        StretchToParent(gameOverPanel.transform);

        gameOverText = CreateText(gameOverPanel.transform, "GameOverText", 28, TextAnchor.MiddleCenter);
        gameOverText.fontStyle = FontStyle.Bold;
        var gameOverRt = (RectTransform)gameOverText.transform;
        gameOverRt.anchorMin = new Vector2(0.5f, 0.5f);
        gameOverRt.anchorMax = new Vector2(0.5f, 0.5f);
        gameOverRt.pivot = new Vector2(0.5f, 0.5f);
        gameOverRt.anchoredPosition = new Vector2(0f, 70f);
        gameOverRt.sizeDelta = new Vector2(420f, 140f);

        restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Restart");
        var restartRt = (RectTransform)restartButton.transform;
        restartRt.anchorMin = new Vector2(0.5f, 0.5f);
        restartRt.anchorMax = new Vector2(0.5f, 0.5f);
        restartRt.pivot = new Vector2(0.5f, 0.5f);
        restartRt.anchoredPosition = new Vector2(0f, -36f);
        restartRt.sizeDelta = new Vector2(220f, 42f);
        restartButton.onClick.AddListener(() => restartRequested?.Invoke());

        gameOverMenuButton = CreateButton(gameOverPanel.transform, "MenuButton", "Back To Menu");
        var menuRt = (RectTransform)gameOverMenuButton.transform;
        menuRt.anchorMin = new Vector2(0.5f, 0.5f);
        menuRt.anchorMax = new Vector2(0.5f, 0.5f);
        menuRt.pivot = new Vector2(0.5f, 0.5f);
        menuRt.anchoredPosition = new Vector2(0f, -88f);
        menuRt.sizeDelta = new Vector2(220f, 42f);
        gameOverMenuButton.onClick.AddListener(() => menuRequested?.Invoke());

        gameOverPanel.SetActive(false);
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
            float alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            SetControlsHintAlpha(alpha);
            yield return null;
        }

        SetControlsHintAlpha(0f);
        controlsHintFadeCoroutine = null;
    }

    private void SetControlsHintAlpha(float alpha)
    {
        if (controlsHintText == null) return;
        var color = controlsHintText.color;
        color.a = Mathf.Clamp01(alpha);
        controlsHintText.color = color;
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

        float duration = 0.28f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float alpha = Mathf.Sin(u * Mathf.PI) * 0.55f;
            var color = hpRegenPulse.color;
            color.a = alpha;
            hpRegenPulse.color = color;
            yield return null;
        }

        var final = hpRegenPulse.color;
        final.a = 0f;
        hpRegenPulse.color = final;
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
        float shineWidth = rt.sizeDelta.x;
        float startX = -shineWidth;
        float endX = width + shineWidth;

        float duration = 0.32f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, u), 0f);

            float alpha = Mathf.Sin(u * Mathf.PI) * 0.38f;
            var color = hpRegenShine.color;
            color.a = alpha;
            hpRegenShine.color = color;
            yield return null;
        }

        var final = hpRegenShine.color;
        final.a = 0f;
        hpRegenShine.color = final;
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
        float duration = 0.18f;
        float t = 0f;
        var start = multiplierText.transform.localScale;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            multiplierText.transform.localScale = Vector3.Lerp(start, Vector3.one, Mathf.Clamp01(t / duration));
            yield return null;
        }

        multiplierText.transform.localScale = Vector3.one;
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
            float currentAlpha;
            if (t <= half) currentAlpha = Mathf.Lerp(0f, alpha, Mathf.Clamp01(t / half));
            else currentAlpha = Mathf.Lerp(alpha, 0f, Mathf.Clamp01((t - half) / half));
            SetDamageAlpha(currentAlpha);
            yield return null;
        }

        SetDamageAlpha(0f);
        damageFlashCoroutine = null;
    }

    private void SetDamageColor(Color color)
    {
        if (damageFlashImage == null) return;
        var current = damageFlashImage.color;
        current.r = color.r;
        current.g = color.g;
        current.b = color.b;
        damageFlashImage.color = current;
    }

    private void SetDamageAlpha(float alpha)
    {
        if (damageFlashImage == null) return;
        var current = damageFlashImage.color;
        current.a = Mathf.Clamp01(alpha);
        damageFlashImage.color = current;
    }

    private static void StretchToParent(Transform transform)
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        var image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(1f, 1f, 1f, 0.9f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText(go.transform, "Text", 18, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.black;
        StretchToParent(text.transform);

        return button;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    private static Image CreateFade(Transform parent)
    {
        var go = new GameObject("Fade");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;
        StretchToParent(image.transform);
        return image;
    }

    private static Image CreateDamageFlash(Transform parent)
    {
        var go = new GameObject("DamageFlash");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(1f, 0f, 0f, 0f);
        image.raycastTarget = false;
        StretchToParent(image.transform);
        return image;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        var inputSystemUIModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemUIModule != null)
        {
            go.AddComponent(inputSystemUIModule);
        }
        else
        {
            go.AddComponent<StandaloneInputModule>();
        }
    }

    private static void SelectButton(Button button)
    {
        if (button == null) return;
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
