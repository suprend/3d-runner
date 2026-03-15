using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerGameManager : MonoBehaviour
{
    public enum RunState
    {
        MainMenu = 0,
        Running = 1,
        Paused = 2,
        GameOver = 3,
        Restarting = 4,
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
    private InputAction pauseAction;
    private Coroutine transitionCoroutine;
    private bool initialMenuShown;
    private RunState? requestedStateAfterRecreate;

    public RunState State { get; private set; } = RunState.MainMenu;

    public void Initialize(RunnerBootstrap runnerBootstrap)
    {
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        bootstrap = runnerBootstrap;
        if (bootstrap != null) bootstrap.RunRecreated += OnRunRecreated;
        OnRunRecreated();
    }

    public void RequestStart()
    {
        if (State != RunState.MainMenu) return;
        if (bootstrap == null) return;
        if (transitionCoroutine != null) return;
        transitionCoroutine = StartCoroutine(TransitionRoutine(RunState.Running));
    }

    public void RequestRestart()
    {
        if (State != RunState.GameOver) return;
        if (transitionCoroutine != null) return;
        transitionCoroutine = StartCoroutine(TransitionRoutine(RunState.Running));
    }

    public void RequestResume()
    {
        if (State != RunState.Paused) return;
        State = RunState.Running;
        ApplyState();
    }

    public void RequestReturnToMenu()
    {
        if (State != RunState.Paused && State != RunState.GameOver) return;
        if (bootstrap == null) return;
        if (transitionCoroutine != null) return;
        transitionCoroutine = StartCoroutine(TransitionRoutine(RunState.MainMenu));
    }

    public void RequestQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (bootstrap != null) bootstrap.RunRecreated -= OnRunRecreated;
        UnbindRuntime();
    }

    private void Update()
    {
        if (State == RunState.GameOver)
        {
            if (restartAction != null && restartAction.triggered) RequestRestart();
            return;
        }

        if (pauseAction == null || !pauseAction.triggered) return;

        if (State == RunState.Running)
        {
            State = RunState.Paused;
            ApplyState();
        }
        else if (State == RunState.Paused)
        {
            RequestResume();
        }
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

        if (score != null)
        {
            score.Initialize(player != null ? player.transform : null, bootstrap.Config, health);
        }

        if (ui != null)
        {
            ui.Bind(health, difficulty, score, bootstrap.Config);
        }

        BindActions();

        if (requestedStateAfterRecreate.HasValue)
        {
            State = requestedStateAfterRecreate.Value;
            requestedStateAfterRecreate = null;
            initialMenuShown = true;
        }
        else if (!initialMenuShown && health != null)
        {
            State = RunState.MainMenu;
            initialMenuShown = true;
        }

        ApplyState();
    }

    private void UnbindRuntime()
    {
        if (health != null) health.Died -= OnDied;
        health = null;
        player = null;
        spawner = null;
        difficulty = null;
        score = null;
    }

    private void BindActions()
    {
        restartAction = null;
        pauseAction = null;

        if (bootstrap == null || bootstrap.InputActions == null) return;

        var map = bootstrap.InputActions.FindActionMap("Player", false);
        if (map == null) return;

        restartAction = map.FindAction("Restart", false);
        pauseAction = map.FindAction("Pause", false);
        map.Enable();
    }

    private void OnDied()
    {
        if (State != RunState.Running) return;
        State = RunState.GameOver;
        ApplyState();
    }

    private void ApplyState()
    {
        switch (State)
        {
            case RunState.MainMenu:
                Time.timeScale = 0f;
                SetGameplayActive(false);
                if (ui != null)
                {
                    ui.HidePauseMenu();
                    ui.HideGameOver();
                    ui.ShowMainMenu(score != null ? score.BestScore : 0);
                }
                break;

            case RunState.Running:
                Time.timeScale = 1f;
                SetGameplayActive(true);
                if (ui != null)
                {
                    ui.HideMainMenu();
                    ui.HidePauseMenu();
                    ui.HideGameOver();
                    ui.SetHudVisible(bootstrap != null && bootstrap.ShowHud);
                }
                break;

            case RunState.Paused:
                Time.timeScale = 0f;
                SetGameplayActive(false);
                if (ui != null)
                {
                    ui.HideMainMenu();
                    ui.HideGameOver();
                    ui.SetHudVisible(bootstrap != null && bootstrap.ShowHud);
                    ui.ShowPauseMenu(score != null ? score.Score : 0, score != null ? score.BestScore : 0);
                }
                break;

            case RunState.GameOver:
                Time.timeScale = 1f;
                SetGameplayActive(false);
                if (ui != null)
                {
                    ui.HideMainMenu();
                    ui.HidePauseMenu();
                    ui.SetHudVisible(bootstrap != null && bootstrap.ShowHud);
                    ui.ShowGameOver(score != null ? score.Score : 0, score != null ? score.BestScore : 0);
                }
                break;

            case RunState.Restarting:
                Time.timeScale = 1f;
                SetGameplayActive(false);
                if (ui != null)
                {
                    ui.HideMainMenu();
                    ui.HidePauseMenu();
                    ui.HideGameOver();
                }
                break;
        }
    }

    private void SetGameplayActive(bool active)
    {
        if (player != null)
        {
            player.enabled = active;
            player.Freeze(!active);
        }

        if (spawner != null) spawner.enabled = active;
        if (difficulty != null) difficulty.enabled = active;

        if (ui != null)
        {
            ui.SetHudVisible(active && bootstrap != null && bootstrap.ShowHud);
        }
    }

    private IEnumerator TransitionRoutine(RunState targetState)
    {
        State = RunState.Restarting;
        ApplyState();

        float duration = bootstrap != null && bootstrap.Config != null ? bootstrap.Config.fadeDuration : 0.3f;

        if (fader != null) yield return fader.FadeTo(1f, duration);

        requestedStateAfterRecreate = targetState;
        if (bootstrap != null) bootstrap.RestartRunImmediate();
        yield return null;

        if (fader != null) yield return fader.FadeTo(0f, duration);

        transitionCoroutine = null;
    }
}
