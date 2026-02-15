using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RunnerScreenFader : MonoBehaviour
{
    [SerializeField] private Image image;

    public void Initialize(Image targetImage)
    {
        image = targetImage;
        SetAlpha(0f);
        if (image != null) image.raycastTarget = false;
    }

    public void SetAlpha(float alpha)
    {
        if (image == null) return;
        var c = image.color;
        c.a = Mathf.Clamp01(alpha);
        image.color = c;
        image.raycastTarget = c.a > 0.001f;
    }

    public IEnumerator FadeTo(float alpha, float duration)
    {
        if (image == null) yield break;

        float start = image.color.a;
        float end = Mathf.Clamp01(alpha);
        if (duration <= 0f)
        {
            SetAlpha(end);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            SetAlpha(Mathf.Lerp(start, end, k));
            yield return null;
        }

        SetAlpha(end);
    }
}
