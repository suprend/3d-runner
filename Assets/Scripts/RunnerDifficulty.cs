using UnityEngine;

public class RunnerDifficulty : MonoBehaviour
{
    [SerializeField] private RunnerConfigSO config;
    [SerializeField] private RunnerPlayerController player;
    [SerializeField] private RunnerObstacleSpawner spawner;

    private float elapsed;

    public float Elapsed => elapsed;
    public float CurrentSpeed { get; private set; }
    public float CurrentSpawnInterval { get; private set; }

    public void Initialize(RunnerConfigSO runnerConfig, RunnerPlayerController playerController, RunnerObstacleSpawner obstacleSpawner)
    {
        config = runnerConfig;
        player = playerController;
        spawner = obstacleSpawner;
        ResetRun();
    }

    public void ResetRun()
    {
        elapsed = 0f;
        Apply(0f);
    }

    private void Update()
    {
        Apply(Time.deltaTime);
    }

    private void Apply(float deltaTime)
    {
        if (config == null || player == null || spawner == null) return;

        elapsed += deltaTime;

        float startSpeed = Mathf.Max(0f, config.forwardSpeedStart);
        float growth = Mathf.Max(0f, config.speedIncreasePerSecond);
        float t = Mathf.Max(0f, elapsed);

        if (config.useLogarithmicSpeed)
        {
            float logScale = Mathf.Max(0.01f, config.speedLogScale);
            CurrentSpeed = startSpeed + growth * Mathf.Log(1f + logScale * t) / logScale;
        }
        else
        {
            CurrentSpeed = startSpeed + growth * t;
        }

        float hardCap = Mathf.Max(0f, config.maxForwardSpeed);
        if (hardCap > 0f) CurrentSpeed = Mathf.Min(CurrentSpeed, hardCap);

        player.SetForwardSpeed(CurrentSpeed);
        spawner.SetForwardSpeed(CurrentSpeed);

        CurrentSpawnInterval = Mathf.Max(config.spawnIntervalMin,
            config.spawnIntervalStart - config.spawnIntervalDecreasePerSecond * elapsed);
        spawner.SetSpawnInterval(CurrentSpawnInterval);
    }
}
