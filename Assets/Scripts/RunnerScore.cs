using System;
using UnityEngine;

public class RunnerScore : MonoBehaviour
{
    private const string BestScoreKey = "Runner.BestScore";

    [SerializeField] private Transform player;
    [SerializeField] private RunnerConfigSO config;
    [SerializeField] private RunnerHealth health;
    [SerializeField] private float lastPlayerZ;
    [SerializeField] private float scoreAccum;
    [SerializeField] private int score;
    [SerializeField] private int bestScore;
    [SerializeField] private float multiplier = 1f;
    [SerializeField] private float timeWithoutHpDamage;
    [SerializeField] private float nextComboStepAt;

    public int Score => score;
    public int BestScore => bestScore;
    public float Multiplier => multiplier;

    public event Action<int> ScoreChanged;
    public event Action<int> BestScoreChanged;
    public event Action<float> MultiplierChanged;

    private void Awake()
    {
        LoadPersistentData();
    }

    public void Initialize(Transform playerTransform, RunnerConfigSO runnerConfig, RunnerHealth playerHealth)
    {
        if (health != null) health.Damaged -= OnDamaged;
        player = playerTransform;
        config = runnerConfig;
        health = playerHealth;
        if (health != null) health.Damaged += OnDamaged;
        ResetRun();
    }

    public void ResetRun()
    {
        lastPlayerZ = player != null ? player.position.z : 0f;
        scoreAccum = 0f;
        SetMultiplier(1f);
        timeWithoutHpDamage = 0f;
        nextComboStepAt = config != null ? Mathf.Max(0.1f, config.comboStepSeconds) : 6f;
        SetScore(0);
    }

    private void Update()
    {
        if (player == null) return;

        float z = player.position.z;
        float deltaZ = z - lastPlayerZ;
        if (deltaZ > 0f)
        {
            float ppm = config != null ? Mathf.Max(0f, config.basePointsPerMeter) : 1f;
            scoreAccum += deltaZ * ppm * Mathf.Max(1f, multiplier);
        }
        lastPlayerZ = z;

        UpdateCombo(Time.deltaTime);

        int newScore = Mathf.Max(0, Mathf.FloorToInt(scoreAccum));
        if (newScore != score) SetScore(newScore);
    }

    private void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        ScoreChanged?.Invoke(score);
        UpdateBestScore(score);
    }

    private void SetMultiplier(float value)
    {
        float v = Mathf.Max(1f, value);
        if (Mathf.Approximately(v, multiplier)) return;
        multiplier = v;
        MultiplierChanged?.Invoke(multiplier);
    }

    private void UpdateCombo(float dt)
    {
        if (config == null || !config.comboEnabled)
        {
            if (multiplier != 1f) SetMultiplier(1f);
            return;
        }

        float stepSeconds = Mathf.Max(0.1f, config.comboStepSeconds);
        float stepMul = Mathf.Max(0f, config.comboStepMultiplier);
        float maxMul = Mathf.Max(1f, config.comboMaxMultiplier);

        timeWithoutHpDamage += Mathf.Max(0f, dt);
        if (timeWithoutHpDamage < nextComboStepAt) return;

        while (timeWithoutHpDamage >= nextComboStepAt)
        {
            nextComboStepAt += stepSeconds;
            SetMultiplier(Mathf.Min(maxMul, multiplier + stepMul));
            if (multiplier >= maxMul) break;
        }
    }

    private void OnDamaged(int amount, int current, int max)
    {
        timeWithoutHpDamage = 0f;
        nextComboStepAt = config != null ? Mathf.Max(0.1f, config.comboStepSeconds) : 6f;
        SetMultiplier(1f);
    }

    public void AddBonus(int points)
    {
        scoreAccum += Mathf.Max(0, points);
        int newScore = Mathf.Max(0, Mathf.FloorToInt(scoreAccum));
        if (newScore != score) SetScore(newScore);
    }

    public void LoadPersistentData()
    {
        int loaded = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
        if (loaded == bestScore) return;
        bestScore = loaded;
        BestScoreChanged?.Invoke(bestScore);
    }

    private void UpdateBestScore(int candidate)
    {
        int clamped = Mathf.Max(0, candidate);
        if (clamped <= bestScore) return;

        bestScore = clamped;
        PlayerPrefs.SetInt(BestScoreKey, bestScore);
        PlayerPrefs.Save();
        BestScoreChanged?.Invoke(bestScore);
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }
}
