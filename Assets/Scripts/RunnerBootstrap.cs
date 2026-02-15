using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerBootstrap : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunnerConfigSO config;
    [SerializeField] private InputActionAsset inputActions;

    [Header("UI")]
    [SerializeField] private bool showHud = true;

    private GameObject runtimeRoot;
    private RunnerHealth playerHealth;
    private RunnerPlayerController playerController;
    private RunnerObstacleSpawner spawner;
    private RunnerDifficulty difficulty;
    private RunnerEndlessTrack track;
    private RunnerGameManager gameManager;
    private RunnerScore score;
    private RunnerUIController ui;
    private RunnerScreenFader fader;
    private RunnerDamageFeedback damageFeedback;
    private RunnerStartHints startHints;
    private RunnerPlayerFeedback playerFeedback;

    public RunnerConfigSO Config => config;
    public InputActionAsset InputActions => inputActions;
    public RunnerHealth PlayerHealth => playerHealth;
    public RunnerPlayerController PlayerController => playerController;
    public RunnerObstacleSpawner Spawner => spawner;
    public RunnerDifficulty Difficulty => difficulty;
    public RunnerScore Score => score;
    public RunnerUIController UI => ui;
    public RunnerScreenFader Fader => fader;
    public bool ShowHud => showHud;

    public event Action RunRecreated;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError("RunnerBootstrap: RunnerConfigSO is not assigned.", this);
            enabled = false;
            return;
        }

        if (inputActions == null) inputActions = RunnerInputFactory.CreateDefault();

        EnsurePersistentSystems();
        ConfigureLayerCollisions();
        ApplyVisualSettings();

        EnsureGameManager();
        gameManager.Initialize(this);

        CreateOrRecreateRuntime();
        EnsureCameraFollow();
        RunRecreated?.Invoke();
    }

    public void RestartRun()
    {
        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }

    public void RestartRunImmediate()
    {
        StopAllCoroutines();
        CreateOrRecreateRuntime();
        EnsureCameraFollow();
        RunRecreated?.Invoke();
    }

    private IEnumerator RestartRoutine()
    {
        float delay = config != null ? config.deathRestartDelay : 1f;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        RestartRunImmediate();
    }

    private void CreateOrRecreateRuntime()
    {
        if (runtimeRoot != null) Destroy(runtimeRoot);

        runtimeRoot = new GameObject("RunnerRuntime");

        CreatePlayer(runtimeRoot.transform);
        CreateTrack(runtimeRoot.transform);
        CreateSpawnerAndDifficulty(runtimeRoot.transform);
    }

    private void CreatePlayer(Transform parent)
    {
        var playerGo = new GameObject("Player");
        playerGo.transform.SetParent(parent);
        playerGo.transform.position = new Vector3(0f, 0f, 0f);
        SetLayer(playerGo, "Player");

        var playerCapsule = playerGo.AddComponent<CapsuleCollider>();
        playerCapsule.direction = 1; // Y

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(playerGo.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        SetLayer(visual, "Player");
        var visualCol = visual.GetComponent<Collider>();
        if (visualCol != null)
        {
            visualCol.enabled = false;
            Destroy(visualCol);
        }

        FitPlayerColliderToVisual(playerGo.transform, playerCapsule, visual);
        SnapPlayerToGround(playerGo.transform, playerCapsule);

        var rb = playerGo.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var powerups = playerGo.AddComponent<RunnerPowerups>();
        powerups.Initialize(config);

        playerHealth = playerGo.AddComponent<RunnerHealth>();
        playerHealth.Initialize(config.maxHealth, config.damageInvulnerabilitySeconds);
        playerHealth.ConfigureRegen(config.regenEnabled, config.regenDelaySeconds, config.regenIntervalSeconds, config.regenAmount);

        playerController = playerGo.AddComponent<RunnerPlayerController>();
        playerController.Initialize(config, inputActions);
    }

    private static void FitPlayerColliderToVisual(Transform player, CapsuleCollider capsule, GameObject visual)
    {
        if (player == null || capsule == null || visual == null) return;

        var r = visual.GetComponent<Renderer>();
        if (r == null) return;

        // Renderer bounds are in world space; player has identity scale/rot in this template.
        var b = r.bounds;
        var localCenter = b.center - player.position;
        var size = b.size;

        capsule.center = new Vector3(0f, localCenter.y, 0f);
        capsule.height = Mathf.Max(0.2f, size.y);
        capsule.radius = Mathf.Max(0.05f, 0.5f * Mathf.Max(size.x, size.z));

        // Capsule height must be >= diameter.
        float minHeight = capsule.radius * 2f;
        if (capsule.height < minHeight) capsule.height = minHeight;
    }

    private void SnapPlayerToGround(Transform player, CapsuleCollider capsule)
    {
        if (player == null || capsule == null) return;

        // Track top surface is at Y=0 in this project.
        float desiredBottomWorldY = 0.02f;
        float bottomLocalY = capsule.center.y - capsule.height * 0.5f;
        float currentBottomWorldY = player.position.y + bottomLocalY;
        float delta = desiredBottomWorldY - currentBottomWorldY;

        var p = player.position;
        p.y += delta;
        player.position = p;
    }

    private void CreateTrack(Transform parent)
    {
        var trackRoot = new GameObject("Track");
        trackRoot.transform.SetParent(parent);
        SetLayer(trackRoot, "Ground");

        var segments = new List<Transform>(config.trackSegmentCount);
        for (int i = 0; i < config.trackSegmentCount; i++)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"TrackSegment_{i}";
            seg.transform.SetParent(trackRoot.transform);
            seg.transform.localScale = new Vector3(config.trackWidth, config.trackThickness, config.trackSegmentLength);
            seg.transform.position = new Vector3(0f, -config.trackThickness * 0.5f, i * config.trackSegmentLength);
            SetLayer(seg, "Ground");

            var r = seg.GetComponent<Renderer>();
            if (r != null) r.material.color = config.trackColor;

            segments.Add(seg.transform);
        }

        track = trackRoot.AddComponent<RunnerEndlessTrack>();
        float invisibleAhead = Mathf.Max(config.spawnDistanceAhead, config.cameraFarClip + 20f);
        if (config.fogEnabled) invisibleAhead = Mathf.Max(invisibleAhead, config.fogEnd + 10f);
        float minSpawnOffset = invisibleAhead + config.trackSegmentLength;

        track.Initialize(playerController.transform, config.trackSegmentLength, config.trackRecycleBehindPlayerDistance, minSpawnOffset, segments);
    }

    private void CreateSpawnerAndDifficulty(Transform parent)
    {
        var spawnerGo = new GameObject("ObstacleSpawner");
        spawnerGo.transform.SetParent(parent);
        spawner = spawnerGo.AddComponent<RunnerObstacleSpawner>();
        spawner.Initialize(config, playerController.transform);
        if (playerController != null) playerController.SetObstacleSpawner(spawner);

        var diffGo = new GameObject("Difficulty");
        diffGo.transform.SetParent(parent);
        difficulty = diffGo.AddComponent<RunnerDifficulty>();
        difficulty.Initialize(config, playerController, spawner);
    }

    private void EnsureGameManager()
    {
        if (gameManager == null)
        {
            gameManager = GetComponent<RunnerGameManager>();
            if (gameManager == null) gameManager = gameObject.AddComponent<RunnerGameManager>();
        }
    }

    private void EnsurePersistentSystems()
    {
        score = GetComponent<RunnerScore>();
        if (score == null) score = gameObject.AddComponent<RunnerScore>();

        ui = GetComponent<RunnerUIController>();
        if (ui == null) ui = gameObject.AddComponent<RunnerUIController>();

        ui.Initialize(() =>
        {
            if (gameManager != null) gameManager.RequestRestart();
        });
        ui.SetHudVisible(showHud);

        fader = GetComponent<RunnerScreenFader>();
        if (fader == null) fader = gameObject.AddComponent<RunnerScreenFader>();
        if (ui.FadeImage != null) fader.Initialize(ui.FadeImage);

        damageFeedback = GetComponent<RunnerDamageFeedback>();
        if (damageFeedback == null) damageFeedback = gameObject.AddComponent<RunnerDamageFeedback>();
        damageFeedback.Initialize(this);

        startHints = GetComponent<RunnerStartHints>();
        if (startHints == null) startHints = gameObject.AddComponent<RunnerStartHints>();
        startHints.Initialize(this);

        playerFeedback = GetComponent<RunnerPlayerFeedback>();
        if (playerFeedback == null) playerFeedback = gameObject.AddComponent<RunnerPlayerFeedback>();
        playerFeedback.Initialize(this);
    }

    private void ApplyVisualSettings()
    {
        if (config == null) return;

        RenderSettings.fog = config.fogEnabled;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = config.fogColor;
        RenderSettings.fogStartDistance = config.fogStart;
        RenderSettings.fogEndDistance = config.fogEnd;

        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            float far = Mathf.Max(10f, config.cameraFarClip);
            if (config.fogEnabled) far = Mathf.Min(far, config.fogEnd + 10f);
            cam.farClipPlane = far;
        }
    }

    private void EnsureCameraFollow()
    {
        var cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam == null) return;

        var follow = cam.GetComponent<RunnerCameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<RunnerCameraFollow>();
        if (cam.GetComponent<RunnerCameraShake>() == null) cam.gameObject.AddComponent<RunnerCameraShake>();
        follow.Initialize(playerController != null ? playerController.transform : null, config.cameraOffset, config.cameraLookAtOffset, config.cameraSmoothTime);
    }

    private static void ConfigureLayerCollisions()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int pickup = LayerMask.NameToLayer("Pickup");

        Ignore(ground, obstacle);
        Ignore(ground, pickup);
        Ignore(obstacle, obstacle);
        Ignore(obstacle, pickup);
        Ignore(pickup, pickup);
    }

    private static void Ignore(int layerA, int layerB)
    {
        if (layerA < 0 || layerB < 0) return;
        Physics.IgnoreLayerCollision(layerA, layerB, true);
    }

    private static void SetLayer(GameObject go, string layerName)
    {
        if (go == null) return;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return;
        go.layer = layer;
    }
}
