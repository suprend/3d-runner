using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerGameManager : MonoBehaviour
{
    public enum RunState
    {
        Running = 0,
        GameOver = 1,
        Restarting = 2,
    }

    [SerializeField] private RunnerBootstrap bootstrap;

    private RunnerHealth health;
    private RunnerPlayerController player;
    private RunnerObstacleSpawner spawner;
    private RunnerDifficulty difficulty;
    private RunnerScore score;
    private RunnerUIController ui;
    private RunnerScreenFader fader;

    private InputAction restartAction;
    private Coroutine restartCoroutine;
    private bool restarting;

    public RunState State { get; private set; } = RunState.Running;

    public void Initialize(RunnerBootstrap runnerBootstrap)
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        bootstrap = runnerBootstrap;
        if (bootstrap != null) bootstrap.RunRecreated += OnRunRecreated;

        OnRunRecreated();
    }

    public void RequestRestart()
    {
        if (State != RunState.GameOver) return;
        if (restartCoroutine != null) return;
        restartCoroutine = StartCoroutine(RestartRoutine());
    }

    private void OnDestroy()
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        UnbindRuntime();
    }

    private void Update()
    {
        if (State != RunState.GameOver) return;
        if (restartAction != null && restartAction.triggered) RequestRestart();
    }

    private void OnRunRecreated()
    {
        UnbindRuntime();

        if (bootstrap == null) return;

        health = bootstrap.PlayerHealth;
        player = bootstrap.PlayerController;
        spawner = bootstrap.Spawner;
        difficulty = bootstrap.Difficulty;
        score = bootstrap.Score;
        ui = bootstrap.UI;
        fader = bootstrap.Fader;

        if (health != null) health.Died += OnDied;

        if (player != null)
        {
            player.enabled = true;
            player.Freeze(false);
        }
        if (spawner != null) spawner.enabled = true;
        if (difficulty != null) difficulty.enabled = true;

        if (score != null) score.Initialize(player != null ? player.transform : null, bootstrap != null ? bootstrap.Config : null, health);
        if (ui != null)
        {
            ui.Bind(health, difficulty, score);
            ui.HideGameOver();
        }

        BindRestartAction();
        State = restarting ? RunState.Restarting : RunState.Running;
    }

    private void UnbindRuntime()
    {
        if (health != null) health.Died -= OnDied;
        health = null;
        player = null;
        spawner = null;
        difficulty = null;
    }

    private void BindRestartAction()
    {
        restartAction = null;
        if (bootstrap == null || bootstrap.InputActions == null) return;

        var map = bootstrap.InputActions.FindActionMap("Player", false);
        if (map == null) return;
        restartAction = map.FindAction("Restart", false);
        map.Enable();
    }

    private void OnDied()
    {
        if (State != RunState.Running) return;
        EnterGameOver();
    }

    private void EnterGameOver()
    {
        State = RunState.GameOver;

        if (spawner != null) spawner.enabled = false;
        if (difficulty != null) difficulty.enabled = false;

        if (player != null)
        {
            player.enabled = false;
            player.Freeze(true);
        }

        if (ui != null) ui.ShowGameOver(score != null ? score.Score : 0);
    }

    private IEnumerator RestartRoutine()
    {
        restarting = true;
        State = RunState.Restarting;

        float d = bootstrap != null && bootstrap.Config != null ? bootstrap.Config.fadeDuration : 0.3f;

        if (ui != null) ui.HideGameOver();
        if (fader != null) yield return fader.FadeTo(1f, d);

        if (bootstrap != null) bootstrap.RestartRunImmediate();
        yield return null;

        if (fader != null) yield return fader.FadeTo(0f, d);

        restarting = false;
        State = RunState.Running;
        restartCoroutine = null;
    }
}
