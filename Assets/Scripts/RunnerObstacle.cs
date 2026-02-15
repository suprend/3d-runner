using UnityEngine;

public class RunnerObstacle : MonoBehaviour
{
    [SerializeField] private ObstacleTypeSO type;
    [SerializeField] private int damage = 1;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private bool avoidableByJump = true;

    private RunnerObstacleSpawner spawner;
    private Collider cachedCollider;
    private bool hasDealtDamage;

    public ObstacleTypeSO Type => type;
    public int ActiveListIndex { get; internal set; } = -1;
    public bool IsInPool { get; internal set; }
    public Collider CachedCollider => cachedCollider != null ? cachedCollider : (cachedCollider = GetComponent<Collider>());

    public void Configure(ObstacleTypeSO obstacleType, RunnerObstacleSpawner ownerSpawner)
    {
        type = obstacleType;
        spawner = ownerSpawner;

        damage = obstacleType != null ? Mathf.Max(0, obstacleType.damage) : 1;
        destroyOnHit = obstacleType == null || obstacleType.destroyOnHit;
        avoidableByJump = obstacleType == null || obstacleType.avoidableByJump;

        if (cachedCollider == null) cachedCollider = GetComponent<Collider>();
        if (cachedCollider != null) cachedCollider.isTrigger = true;

        hasDealtDamage = false;
    }

    public void ResetForReuse()
    {
        hasDealtDamage = false;
        if (cachedCollider == null) cachedCollider = GetComponent<Collider>();
        if (cachedCollider != null) cachedCollider.enabled = true;
    }

    private void OnCollisionEnter(Collision other)
    {
        TryDealDamage(other.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider other)
    {
        if (other == null) return;
        if (hasDealtDamage) return;

        var health = other.GetComponentInParent<RunnerHealth>();
        if (health == null) return;

        health.TakeDamage(damage);
        hasDealtDamage = true;

        if (destroyOnHit)
        {
            if (spawner != null) spawner.Despawn(this);
            else Destroy(gameObject);
            return;
        }

        if (cachedCollider == null) cachedCollider = GetComponent<Collider>();
        if (cachedCollider != null) cachedCollider.enabled = false;
    }
}
