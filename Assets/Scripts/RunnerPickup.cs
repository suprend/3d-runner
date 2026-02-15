using System.Collections;
using UnityEngine;

public class RunnerPickup : MonoBehaviour
{
    [SerializeField] private PickupTypeSO type;

    private RunnerObstacleSpawner owner;
    private RunnerConfigSO config;
    private bool collected;
    private Coroutine popCoroutine;
    private Vector3 baseScale;
    private Vector3 basePosition;
    private bool hasBasePosition;
    private float floatPhase;
    private Collider cachedCollider;

    private const float FloatAmplitude = 0.20f;
    private const float FloatSpeed = 2.8f;

    public PickupTypeSO Type => type;
    public int ActiveListIndex { get; internal set; } = -1;
    public bool IsInPool { get; internal set; }
    public Collider CachedCollider => cachedCollider != null ? cachedCollider : (cachedCollider = GetComponent<Collider>());

    public void Configure(PickupTypeSO pickupType, RunnerObstacleSpawner spawner, RunnerConfigSO runnerConfig)
    {
        type = pickupType;
        owner = spawner;
        config = runnerConfig;
        collected = false;
        baseScale = transform.localScale;
        hasBasePosition = false;
        floatPhase = UnityEngine.Random.value * Mathf.PI * 2f;
        if (cachedCollider == null) cachedCollider = GetComponent<Collider>();
    }

    public void ResetForReuse()
    {
        collected = false;
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }
        transform.localScale = baseScale;
        hasBasePosition = false;
    }

    private void Update()
    {
        if (collected) return;
        if (type == null) return;

        if (!hasBasePosition)
        {
            basePosition = transform.position;
            hasBasePosition = true;
        }

        float bob = Mathf.Sin(Time.time * FloatSpeed + floatPhase) * FloatAmplitude;
        var p = basePosition;
        p.y += bob;
        transform.position = p;

        transform.Rotate(0f, 140f * Time.deltaTime, 0f, Space.Self);
        float pulse = 1f + Mathf.Sin(Time.time * 4.2f) * 0.05f;
        transform.localScale = baseScale * pulse;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other == null) return;

        var powerups = other.GetComponentInParent<RunnerPowerups>();
        if (powerups == null) return;

        collected = true;
        Apply(powerups);

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(PopAndDespawn());
    }

    private void Apply(RunnerPowerups powerups)
    {
        if (type == null || powerups == null) return;

        switch (type.kind)
        {
            case PickupKind.Shield:
                powerups.AddShield(1);
                break;
            case PickupKind.WallBreak:
                powerups.AddWallBreak(1);
                break;
            case PickupKind.JumpBoost:
                powerups.AddJumpBoost(1);
                break;
        }
    }

    private IEnumerator PopAndDespawn()
    {
        float t = 0f;
        float d = 0.10f;
        var start = transform.localScale;
        var up = start * 1.45f;
        while (t < d)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, up, Mathf.Clamp01(t / d));
            yield return null;
        }

        if (owner != null) owner.DespawnPickup(this);
        else gameObject.SetActive(false);
        popCoroutine = null;
    }
}
