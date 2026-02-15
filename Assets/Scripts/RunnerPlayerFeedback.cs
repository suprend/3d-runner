using System.Collections;
using UnityEngine;

public class RunnerPlayerFeedback : MonoBehaviour
{
    [SerializeField] private RunnerBootstrap bootstrap;

    private RunnerConfigSO config;
    private RunnerPlayerController player;
    private Transform visual;
    private Coroutine landCoroutine;

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

        config = bootstrap.Config;
        player = bootstrap.PlayerController;

        if (player != null)
        {
            player.Landed += OnLanded;
            visual = FindVisual(player.transform);
        }
    }

    private void Unbind()
    {
        if (player != null)
        {
            player.Landed -= OnLanded;
        }
        player = null;
        config = null;
        visual = null;

        if (landCoroutine != null)
        {
            StopCoroutine(landCoroutine);
            landCoroutine = null;
        }
    }

    private static Transform FindVisual(Transform playerTransform)
    {
        if (playerTransform == null) return null;
        var direct = playerTransform.Find("Visual");
        if (direct != null) return direct;
        var r = playerTransform.GetComponentInChildren<Renderer>(true);
        return r != null ? r.transform : null;
    }

    private void OnLanded()
    {
        if (player == null || config == null) return;
        if (player.IsSliding) return;
        if (visual == null) return;

        if (landCoroutine != null) StopCoroutine(landCoroutine);
        landCoroutine = StartCoroutine(LandingSquash());
    }

    private IEnumerator LandingSquash()
    {
        float duration = Mathf.Max(0f, config.landingSquashSeconds);
        float amount = Mathf.Clamp01(config.landingSquashAmount);
        if (duration <= 0f || amount <= 0f)
        {
            landCoroutine = null;
            yield break;
        }

        Vector3 baseScale = visual.localScale;
        float t = 0f;
        while (t < duration)
        {
            if (player != null && player.IsSliding) break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float curve = u < 0.5f ? Smooth01(u * 2f) : Smooth01((1f - u) * 2f);

            float y = 1f - amount * curve;
            float xz = 1f + (amount * 0.5f) * curve;
            visual.localScale = new Vector3(baseScale.x * xz, baseScale.y * y, baseScale.z * xz);
            yield return null;
        }

        visual.localScale = baseScale;
        landCoroutine = null;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
