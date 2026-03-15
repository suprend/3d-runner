using System.Collections;
using UnityEngine;

public class RunnerStartHints : MonoBehaviour
{
    [SerializeField] private RunnerBootstrap bootstrap;

    private RunnerUIController ui;
    private RunnerPlayerController player;
    private Coroutine hideCoroutine;
    private bool hidden;

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

        ui = bootstrap.UI;
        player = bootstrap.PlayerController;
        hidden = false;

        if (player != null) player.AnyAction += OnAnyAction;

        ShowHint();
    }

    private void Unbind()
    {
        if (player != null) player.AnyAction -= OnAnyAction;
        player = null;
        ui = null;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private void ShowHint()
    {
        if (ui == null || player == null || bootstrap == null || bootstrap.Config == null) return;
        var gameManager = bootstrap.GetComponent<RunnerGameManager>();
        if (gameManager != null && gameManager.State != RunnerGameManager.RunState.Running) return;

        ui.ShowControlsHint("A/D or \u2190/\u2192: lanes    Space: jump (air Space: jump boost)    E: wall break    Down: slide / fast-fall");

        float d = Mathf.Max(0f, bootstrap.Config.hintDurationSeconds);
        if (d <= 0f) HideHint();
        else hideCoroutine = StartCoroutine(HideAfter(d));
    }

    private IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        HideHint();
        hideCoroutine = null;
    }

    private void OnAnyAction()
    {
        HideHint();
    }

    private void HideHint()
    {
        if (hidden) return;
        hidden = true;
        if (ui != null) ui.HideControlsHint(0.25f);
    }
}
