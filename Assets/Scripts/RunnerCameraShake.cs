using UnityEngine;

public class RunnerCameraShake : MonoBehaviour
{
    [SerializeField] private float timeLeft;
    [SerializeField] private float duration;
    [SerializeField] private float amplitude;

    public void Kick(float kickAmplitude, float kickDuration)
    {
        kickAmplitude = Mathf.Max(0f, kickAmplitude);
        kickDuration = Mathf.Max(0f, kickDuration);
        if (kickAmplitude <= 0f || kickDuration <= 0f) return;

        amplitude = Mathf.Max(amplitude, kickAmplitude);
        duration = Mathf.Max(duration, kickDuration);
        timeLeft = Mathf.Min(duration, timeLeft + kickDuration);
    }

    public void Evaluate(out Vector3 positionOffset, out Vector3 rotationEuler)
    {
        if (timeLeft <= 0f)
        {
            positionOffset = Vector3.zero;
            rotationEuler = Vector3.zero;
            return;
        }

        timeLeft = Mathf.Max(0f, timeLeft - Time.unscaledDeltaTime);
        float k = duration > 0f ? Mathf.Clamp01(timeLeft / duration) : 0f;
        float strength = amplitude * k;

        positionOffset = UnityEngine.Random.insideUnitSphere * strength;

        float rotStrength = strength * 5f;
        rotationEuler = new Vector3(
            UnityEngine.Random.Range(-rotStrength, rotStrength),
            UnityEngine.Random.Range(-rotStrength, rotStrength),
            UnityEngine.Random.Range(-rotStrength, rotStrength));

        if (timeLeft <= 0f)
        {
            amplitude = 0f;
            duration = 0f;
        }
    }
}
