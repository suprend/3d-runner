using System.Collections.Generic;
using UnityEngine;

public class RunnerObstacleSpawner : MonoBehaviour
{
    [SerializeField] private RunnerConfigSO config;
    [SerializeField] private Transform player;

    private readonly List<RunnerObstacle> active = new();
    private int activeHead;
    private readonly Dictionary<ObstacleTypeSO, Stack<RunnerObstacle>> pool = new();
    private readonly Dictionary<ObstacleTypeSO, int> createdCount = new();
    private readonly List<ObstacleTypeSO> anyTypes = new();
    private readonly List<ObstacleTypeSO> jumpableTypes = new();
    private readonly List<ObstacleTypeSO> slideableTypes = new();
    private readonly List<ObstacleTypeSO> bothTypes = new();
    private readonly List<ObstacleTypeSO> unavoidableTypes = new();
    private float spawnInterval;
    private float currentSpeed;
    private float nextRowZ;
    private bool warnedPoolExhausted;
    private bool warnedSpawnBudgetExceeded;
    private float maxObstacleDepth;
    private ObstacleTypeSO[] rowTypesBuffer;
    private bool[] occupiedBuffer;

    private readonly List<RunnerPickup> activePickups = new();
    private int activePickupsHead;
    private readonly Dictionary<PickupTypeSO, Stack<RunnerPickup>> pickupPool = new();
    private readonly Dictionary<PickupTypeSO, int> pickupCreatedCount = new();
    private PickupTypeSO[] pickupTypesCached;
    private float[] pickupTypeCdf;
    private float pickupTypeTotalWeight;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static MaterialPropertyBlock colorPropertyBlock;

    private int obstacleLayer = -1;
    private int pickupLayer = -1;

    public void Initialize(RunnerConfigSO runnerConfig, Transform playerTransform)
    {
        config = runnerConfig;
        player = playerTransform;
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        pickupLayer = LayerMask.NameToLayer("Pickup");
        BuildPools();
        ResetRun();
    }

    public void ResetRun()
    {
        spawnInterval = config != null ? config.spawnIntervalStart : 1f;
        currentSpeed = config != null ? config.forwardSpeedStart : 8f;
        if (player != null && config != null)
        {
            float startAhead = Mathf.Max(0f, config.initialSpawnStartMeters);
            nextRowZ = player.position.z + startAhead;
        }
        else
        {
            nextRowZ = 0f;
        }
        warnedPoolExhausted = false;
        warnedSpawnBudgetExceeded = false;

        for (int i = 0; i < active.Count; i++)
        {
            var obstacle = active[i];
            if (obstacle == null) continue;
            active[i] = null;
            ReturnObstacleToPool(obstacle);
        }
        active.Clear();
        activeHead = 0;

        for (int i = 0; i < activePickups.Count; i++)
        {
            var pickup = activePickups[i];
            if (pickup == null) continue;
            activePickups[i] = null;
            ReturnPickupToPool(pickup);
        }
        activePickups.Clear();
        activePickupsHead = 0;

        PreSpawnInitialRows();
    }

    private void PreSpawnInitialRows()
    {
        if (config == null || player == null) return;
        if (config.obstacleTypes == null || config.obstacleTypes.Length == 0) return;

        float fillToZ = player.position.z + InvisibleAhead();
        int minRows = Mathf.Max(0, config.initialSpawnMinRows);

        int spawned = 0;
        while (spawned < minRows || nextRowZ < fillToZ)
        {
            SpawnRow();
            spawned++;
            if (spawned > 300) break;
        }
    }

    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.05f, interval);
    }

    public void SetForwardSpeed(float speed)
    {
        currentSpeed = Mathf.Max(0f, speed);
    }

    private void Update()
    {
        if (config == null || player == null) return;
        if (config.obstacleTypes == null || config.obstacleTypes.Length == 0) return;

        CleanupBehindPlayer();
        CleanupPickupsBehindPlayer();

        // Distance-based spawning: always keep a continuous horizon of rows ahead of the player.
        // The previous time-based spawning could "jump" nextRowZ forward (InvisibleAhead clamp) and create gaps,
        // which looked like obstacles were disappearing before the player reached them.
        float fillToZ = player.position.z + InvisibleAhead();
        int spawned = 0;
        const int maxRowsPerFrame = 120;
        while (nextRowZ < fillToZ && spawned < maxRowsPerFrame)
        {
            SpawnRow();
            spawned++;
        }

        if (nextRowZ < fillToZ && !warnedSpawnBudgetExceeded)
        {
            warnedSpawnBudgetExceeded = true;
            Debug.LogWarning($"RunnerObstacleSpawner: spawn budget exceeded ({maxRowsPerFrame} rows/frame). Consider increasing minRowGapMeters or reducing spawnDistanceAhead.");
        }
    }

    private void SpawnRow()
    {
        float rowZ = nextRowZ;

        int minLane = config.minLaneIndex;
        int maxLane = config.maxLaneIndex;
        int laneCount = maxLane - minLane + 1;
        if (laneCount <= 0) return;
        EnsureRowBuffers(laneCount);

        var rowTypes = rowTypesBuffer;
        var occupied = occupiedBuffer;
        System.Array.Clear(rowTypes, 0, laneCount);
        System.Array.Clear(occupied, 0, laneCount);
        int filledCount = ChooseFilledCount(laneCount);

        if (filledCount >= laneCount)
        {
            for (int i = 0; i < laneCount; i++) rowTypes[i] = PickWeightedType();
        }
        else if (filledCount == 2 && laneCount >= 3)
        {
            int emptyIndex = UnityEngine.Random.Range(0, laneCount);
            for (int i = 0; i < laneCount; i++)
            {
                if (i == emptyIndex) continue;
                rowTypes[i] = PickWeightedType();
            }
        }
        else
        {
            int laneIndex = UnityEngine.Random.Range(0, laneCount);
            rowTypes[laneIndex] = PickWeightedType();
        }

        EnsureRowSolvable(rowTypes);

        for (int i = 0; i < laneCount; i++)
        {
            var type = rowTypes[i];
            if (type == null) continue;
            int lane = minLane + i;
            SpawnOne(type, lane, rowZ);
            occupied[i] = true;
        }

        TrySpawnPickupForRow(occupied, minLane, rowZ);

        float safeDepthGap = Mathf.Max(0f, maxObstacleDepth) + 1.0f;
        float rowGap = Mathf.Max(config.minRowGapMeters, currentSpeed * spawnInterval, safeDepthGap);
        nextRowZ += rowGap * UnityEngine.Random.Range(0.9f, 1.1f);
    }

    private static int ChooseFilledCount(int laneCount)
    {
        if (laneCount <= 1) return 1;
        if (laneCount == 2) return UnityEngine.Random.value < 0.60f ? 2 : 1;

        float r = UnityEngine.Random.value;
        if (r < 0.10f) return 1;
        if (r < 0.55f) return 2;
        return 3;
    }

    private void EnsureRowBuffers(int laneCount)
    {
        if (laneCount <= 0) return;
        if (rowTypesBuffer == null || rowTypesBuffer.Length != laneCount) rowTypesBuffer = new ObstacleTypeSO[laneCount];
        if (occupiedBuffer == null || occupiedBuffer.Length != laneCount) occupiedBuffer = new bool[laneCount];
    }

    private void EnsureRowSolvable(ObstacleTypeSO[] rowTypes)
    {
        if (rowTypes == null || rowTypes.Length == 0) return;

        bool solvable = false;
        for (int i = 0; i < rowTypes.Length; i++)
        {
            var t = rowTypes[i];
            if (t == null || t.avoidableByJump || t.avoidableBySlide)
            {
                solvable = true;
                break;
            }
        }

        if (solvable) return;

        int laneIndex = UnityEngine.Random.Range(0, rowTypes.Length);
        var replacement = PickSolvableType();
        rowTypes[laneIndex] = replacement;
    }

    private ObstacleTypeSO PickSolvableType()
    {
        if (jumpableTypes.Count > 0 && slideableTypes.Count > 0 && bothTypes.Count > 0)
        {
            float r = UnityEngine.Random.value;
            if (r < 0.34f) return jumpableTypes[UnityEngine.Random.Range(0, jumpableTypes.Count)];
            if (r < 0.67f) return slideableTypes[UnityEngine.Random.Range(0, slideableTypes.Count)];
            return bothTypes[UnityEngine.Random.Range(0, bothTypes.Count)];
        }

        if (jumpableTypes.Count > 0 && slideableTypes.Count > 0)
        {
            return UnityEngine.Random.value < 0.5f
                ? jumpableTypes[UnityEngine.Random.Range(0, jumpableTypes.Count)]
                : slideableTypes[UnityEngine.Random.Range(0, slideableTypes.Count)];
        }

        if (bothTypes.Count > 0) return bothTypes[UnityEngine.Random.Range(0, bothTypes.Count)];
        if (jumpableTypes.Count > 0) return jumpableTypes[UnityEngine.Random.Range(0, jumpableTypes.Count)];
        if (slideableTypes.Count > 0) return slideableTypes[UnityEngine.Random.Range(0, slideableTypes.Count)];
        return null;
    }

    private void SpawnOne(ObstacleTypeSO type, int lane, float z)
    {
        if (type == null) return;

        float x = lane * config.laneOffset;

        var obstacle = Get(type);
        if (obstacle == null) return;

        obstacle.ResetForReuse();

        float y = type.yOffset;
        var col = obstacle.CachedCollider;
        if (col != null) y += col.bounds.extents.y;

        obstacle.transform.position = new Vector3(x, y, z);
    }

    public int ClearLaneObstacles(Vector3 playerPosition, float radius)
    {
        if (config == null) return 0;

        float laneOffset = Mathf.Max(0.01f, config.laneOffset);
        int laneIndex = Mathf.RoundToInt(playerPosition.x / laneOffset);
        laneIndex = Mathf.Clamp(laneIndex, config.minLaneIndex, config.maxLaneIndex);
        float laneX = laneIndex * laneOffset;
        float laneTolerance = Mathf.Max(0.20f, laneOffset * 0.35f);
        float zRadius = Mathf.Max(0.1f, radius);

        int removed = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var obstacle = active[i];
            if (obstacle == null) continue;

            Vector3 p = obstacle.transform.position;
            if (Mathf.Abs(p.x - laneX) > laneTolerance) continue;
            if (Mathf.Abs(p.z - playerPosition.z) > zRadius) continue;

            DespawnAtIndex(i);
            removed++;
        }

        return removed;
    }

    private RunnerObstacle Get(ObstacleTypeSO type)
    {
        if (type == null) return null;

        if (!pool.TryGetValue(type, out var stack))
        {
            stack = new Stack<RunnerObstacle>();
            pool[type] = stack;
            createdCount[type] = 0;
        }

        RunnerObstacle obstacle;
        if (stack.Count > 0)
        {
            obstacle = null;
            while (stack.Count > 0 && obstacle == null)
            {
                var candidate = stack.Pop();
                if (candidate == null) continue;
                // Safety: if pooling got corrupted (e.g. an active object ended up in the pool),
                // never reuse it from the pool, or it will "teleport" and look like it despawned.
                if (candidate.gameObject.activeSelf) continue;
                obstacle = candidate;
            }
            if (obstacle != null)
            {
                obstacle.gameObject.SetActive(true);
                obstacle.Configure(type, this);
                obstacle.IsInPool = false;
                obstacle.ActiveListIndex = active.Count;
                active.Add(obstacle);
                return obstacle;
            }
        }

        {
            int created = createdCount[type];
            int max = config != null ? Mathf.Max(1, config.obstaclePoolMaxPerType) : 60;
            if (created >= max)
            {
                // Soft cap: never recycle active obstacles. Recycling can make obstacles "disappear" before the
                // player reaches them if the spawn logic gets ahead of the runner. Instead, allow the pool to
                // grow to whatever size is required by the configured spawn horizon.
                obstacle = CreateInstance(type);
                createdCount[type] = created + 1;
                if (!warnedPoolExhausted)
                {
                    warnedPoolExhausted = true;
                    Debug.LogWarning($"RunnerObstacleSpawner: pool soft-cap reached for '{type.name}' (max={max}). Growing pool to avoid recycling active obstacles.");
                }
            }
            else
            {
                obstacle = CreateInstance(type);
                createdCount[type] = created + 1;
            }
        }

        obstacle.gameObject.SetActive(true);
        obstacle.Configure(type, this);
        obstacle.IsInPool = false;
        obstacle.ActiveListIndex = active.Count;
        active.Add(obstacle);
        return obstacle;
    }

    private RunnerObstacle CreateInstance(ObstacleTypeSO type)
    {
        var go = GameObject.CreatePrimitive(type.primitive);
        go.transform.SetParent(transform);
        go.name = string.IsNullOrWhiteSpace(type.displayName) ? "Obstacle" : type.displayName;
        if (obstacleLayer >= 0) go.layer = obstacleLayer;
        var scale = type.localScale;
        if (config != null)
        {
            float laneOffset = Mathf.Max(0.01f, config.laneOffset);
            float targetWidth = Mathf.Clamp(laneOffset * 0.70f, 0.2f, laneOffset * 0.95f);
            scale.x = targetWidth;
        }
        go.transform.localScale = scale;

        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var r = go.GetComponent<Renderer>();
        ApplyRendererColor(r, type.color);

        AddReadabilityPillarsIfNeeded(go, type, scale);

        var obstacle = go.AddComponent<RunnerObstacle>();
        obstacle.Configure(type, this);
        obstacle.IsInPool = false;
        obstacle.ActiveListIndex = -1;
        obstacle.gameObject.SetActive(false);
        return obstacle;
    }

    private void AddReadabilityPillarsIfNeeded(GameObject obstacleRoot, ObstacleTypeSO type, Vector3 rootScale)
    {
        if (obstacleRoot == null || type == null) return;

        // Only for "slide-under" obstacles: visually connect the floating block to the ground so the clearance is readable.
        if (!type.avoidableBySlide) return;
        if (type.yOffset <= 0.01f) return;

        float w = Mathf.Max(0.01f, rootScale.x);
        float h = Mathf.Max(0.01f, rootScale.y);
        float d = Mathf.Max(0.01f, rootScale.z);

        float pillarHeightWorld = Mathf.Max(0.05f, type.yOffset);
        float pillarWidthWorld = Mathf.Clamp(w * 0.12f, 0.10f, 0.16f);
        float pillarDepthWorld = d;

        // Convert desired world-space dimensions into local scale (parent is non-uniformly scaled).
        var pillarLocalScale = new Vector3(
            pillarWidthWorld / w,
            pillarHeightWorld / h,
            pillarDepthWorld / d);

        float xWorld = (w * 0.5f) - (pillarWidthWorld * 0.5f);
        float yWorld = (-h * 0.5f) - (pillarHeightWorld * 0.5f);
        var pillarLocalPos = new Vector3(xWorld / w, yWorld / h, 0f);

        CreatePillar("Pillar_L", new Vector3(-pillarLocalPos.x, pillarLocalPos.y, 0f), pillarLocalScale);
        CreatePillar("Pillar_R", new Vector3(+pillarLocalPos.x, pillarLocalPos.y, 0f), pillarLocalScale);

        void CreatePillar(string name, Vector3 localPos, Vector3 localScale)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = name;
            pillar.transform.SetParent(obstacleRoot.transform, worldPositionStays: false);
            pillar.transform.localPosition = localPos;
            pillar.transform.localRotation = Quaternion.identity;
            pillar.transform.localScale = localScale;
            if (obstacleLayer >= 0) pillar.layer = obstacleLayer;

            var pillarCol = pillar.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);

            var pr = pillar.GetComponent<Renderer>();
            ApplyRendererColor(pr, type.color);
        }
    }

    public void Despawn(RunnerObstacle obstacle)
    {
        if (obstacle == null) return;

        int index = obstacle.ActiveListIndex;
        if (index >= 0 && index < active.Count && ReferenceEquals(active[index], obstacle))
        {
            DespawnAtIndex(index);
            return;
        }

        ReturnObstacleToPool(obstacle);
    }

    private void DespawnAtIndex(int index)
    {
        if (index < 0 || index >= active.Count) return;
        var obstacle = active[index];
        active[index] = null;
        ReturnObstacleToPool(obstacle);
    }

    private void ReturnObstacleToPool(RunnerObstacle obstacle)
    {
        if (obstacle == null) return;
        obstacle.ActiveListIndex = -1;

        if (!obstacle.IsInPool)
        {
            var type = obstacle.Type;
            if (type != null)
            {
                if (!pool.TryGetValue(type, out var stack))
                {
                    stack = new Stack<RunnerObstacle>();
                    pool[type] = stack;
                    createdCount[type] = 0;
                }
                stack.Push(obstacle);
                obstacle.IsInPool = true;
            }
        }

        obstacle.gameObject.SetActive(false);
    }

    private void CleanupBehindPlayer()
    {
        float minZ = player.position.z - config.cleanupDistanceBehind;
        while (activeHead < active.Count)
        {
            var obstacle = active[activeHead];
            if (obstacle == null)
            {
                activeHead++;
                continue;
            }
            if (!obstacle.gameObject.activeSelf)
            {
                active[activeHead] = null;
                activeHead++;
                continue;
            }

            if (obstacle.transform.position.z >= minZ) break;

            DespawnAtIndex(activeHead);
            activeHead++;
        }

        CompactActiveObstaclesIfNeeded();
    }

    private void CleanupPickupsBehindPlayer()
    {
        if (config == null || player == null) return;
        float minZ = player.position.z - config.cleanupDistanceBehind;
        while (activePickupsHead < activePickups.Count)
        {
            var p = activePickups[activePickupsHead];
            if (p == null)
            {
                activePickupsHead++;
                continue;
            }
            if (!p.gameObject.activeSelf)
            {
                activePickups[activePickupsHead] = null;
                activePickupsHead++;
                continue;
            }

            if (p.transform.position.z >= minZ) break;

            DespawnPickupAtIndex(activePickupsHead);
            activePickupsHead++;
        }

        CompactActivePickupsIfNeeded();
    }

    private void BuildPools()
    {
        pool.Clear();
        createdCount.Clear();
        anyTypes.Clear();
        jumpableTypes.Clear();
        slideableTypes.Clear();
        bothTypes.Clear();
        unavoidableTypes.Clear();
        maxObstacleDepth = 0f;
        rowTypesBuffer = null;
        occupiedBuffer = null;
        pickupPool.Clear();
        pickupCreatedCount.Clear();
        activePickups.Clear();
        activePickupsHead = 0;
        active.Clear();
        activeHead = 0;
        pickupTypesCached = null;
        pickupTypeCdf = null;
        pickupTypeTotalWeight = 0f;

        if (config == null || config.obstacleTypes == null) return;
        int prewarm = Mathf.Max(0, config.obstaclePoolPrewarmPerType);
        for (int i = 0; i < config.obstacleTypes.Length; i++)
        {
            var type = config.obstacleTypes[i];
            if (type == null) continue;
            anyTypes.Add(type);
            maxObstacleDepth = Mathf.Max(maxObstacleDepth, Mathf.Abs(type.localScale.z));
            if (type.avoidableByJump && type.avoidableBySlide) bothTypes.Add(type);
            else if (type.avoidableBySlide) slideableTypes.Add(type);
            else if (type.avoidableByJump) jumpableTypes.Add(type);
            else unavoidableTypes.Add(type);
            if (!pool.ContainsKey(type)) pool[type] = new Stack<RunnerObstacle>();
            if (!createdCount.ContainsKey(type)) createdCount[type] = 0;
            Prewarm(type, prewarm);
        }

        BuildPickupTypeCache();
    }

    private void TrySpawnPickupForRow(bool[] occupied, int minLane, float rowZ)
    {
        if (config == null || occupied == null) return;
        if (!config.pickupsEnabled) return;
        if (pickupTypesCached == null || pickupTypesCached.Length == 0) return;
        if (UnityEngine.Random.value >= Mathf.Clamp01(config.pickupChancePerRow)) return;

        int laneCount = occupied.Length;
        int emptyCount = 0;
        for (int i = 0; i < laneCount; i++)
        {
            if (!occupied[i]) emptyCount++;
        }
        if (emptyCount == 0) return;

        int emptyPick = UnityEngine.Random.Range(0, emptyCount);
        int laneIndex = -1;
        for (int i = 0; i < laneCount; i++)
        {
            if (occupied[i]) continue;
            if (emptyPick == 0)
            {
                laneIndex = i;
                break;
            }
            emptyPick--;
        }

        if (laneIndex < 0) return;
        int lane = minLane + laneIndex;
        occupied[laneIndex] = true;

        var type = PickPickupTypeWeighted();
        if (type == null) return;
        SpawnPickup(type, lane, rowZ);
    }

    private PickupTypeSO PickPickupTypeWeighted()
    {
        if (pickupTypesCached == null || pickupTypeCdf == null) return null;
        if (pickupTypesCached.Length == 0) return null;
        if (pickupTypeTotalWeight <= 0f) return null;

        float r = UnityEngine.Random.value * pickupTypeTotalWeight;
        for (int i = 0; i < pickupTypeCdf.Length; i++)
        {
            if (r <= pickupTypeCdf[i]) return pickupTypesCached[i];
        }
        return pickupTypesCached[pickupTypesCached.Length - 1];
    }

    private void SpawnPickup(PickupTypeSO type, int lane, float z)
    {
        if (type == null) return;
        var pickup = GetPickup(type);
        if (pickup == null) return;

        pickup.ResetForReuse();
        pickup.Configure(type, this, config);

        float x = lane * config.laneOffset;
        float y = type.yOffset;
        var col = pickup.CachedCollider;
        if (col != null) y += col.bounds.extents.y;
        pickup.transform.position = new Vector3(x, y, z);
    }

    private RunnerPickup GetPickup(PickupTypeSO type)
    {
        if (type == null) return null;

        if (!pickupPool.TryGetValue(type, out var stack))
        {
            stack = new Stack<RunnerPickup>();
            pickupPool[type] = stack;
            pickupCreatedCount[type] = 0;
        }

        RunnerPickup pickup = null;
        while (stack.Count > 0 && pickup == null)
        {
            var candidate = stack.Pop();
            if (candidate == null) continue;
            if (candidate.gameObject.activeSelf) continue;
            pickup = candidate;
        }

        if (pickup == null)
        {
            int created = pickupCreatedCount[type];
            if (created >= 60) return null;
            pickup = CreatePickupInstance(type);
            pickupCreatedCount[type] = created + 1;
        }

        if (pickup == null) return null;
        pickup.gameObject.SetActive(true);
        pickup.IsInPool = false;
        pickup.ActiveListIndex = activePickups.Count;
        activePickups.Add(pickup);
        return pickup;
    }

    private RunnerPickup CreatePickupInstance(PickupTypeSO type)
    {
        var go = GameObject.CreatePrimitive(type.primitive);
        go.transform.SetParent(transform);
        go.name = string.IsNullOrWhiteSpace(type.displayName) ? "Pickup" : type.displayName;
        if (pickupLayer >= 0) go.layer = pickupLayer;
        go.transform.localScale = type.localScale;

        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var r = go.GetComponent<Renderer>();
        ApplyRendererColor(r, type.color);

        var pickup = go.AddComponent<RunnerPickup>();
        pickup.Configure(type, this, config);
        pickup.IsInPool = false;
        pickup.ActiveListIndex = -1;
        go.SetActive(false);
        return pickup;
    }

    public void DespawnPickup(RunnerPickup pickup)
    {
        if (pickup == null) return;

        int index = pickup.ActiveListIndex;
        if (index >= 0 && index < activePickups.Count && ReferenceEquals(activePickups[index], pickup))
        {
            DespawnPickupAtIndex(index);
            return;
        }

        ReturnPickupToPool(pickup);
    }

    private void DespawnPickupAtIndex(int index)
    {
        if (index < 0 || index >= activePickups.Count) return;
        var pickup = activePickups[index];
        activePickups[index] = null;
        ReturnPickupToPool(pickup);
    }

    private void ReturnPickupToPool(RunnerPickup pickup)
    {
        if (pickup == null) return;
        pickup.ActiveListIndex = -1;

        if (!pickup.IsInPool)
        {
            var type = pickup.Type;
            if (type != null)
            {
                if (!pickupPool.TryGetValue(type, out var stack))
                {
                    stack = new Stack<RunnerPickup>();
                    pickupPool[type] = stack;
                    pickupCreatedCount[type] = 0;
                }
                stack.Push(pickup);
                pickup.IsInPool = true;
            }
        }

        pickup.gameObject.SetActive(false);
    }

    private void Prewarm(ObstacleTypeSO type, int count)
    {
        if (type == null) return;
        if (!pool.TryGetValue(type, out var stack))
        {
            stack = new Stack<RunnerObstacle>();
            pool[type] = stack;
        }

        if (!createdCount.ContainsKey(type)) createdCount[type] = 0;

        int max = config != null ? Mathf.Max(1, config.obstaclePoolMaxPerType) : 60;
        while (stack.Count < count && createdCount[type] < max)
        {
            var o = CreateInstance(type);
            createdCount[type] = createdCount[type] + 1;
            o.IsInPool = true;
            o.ActiveListIndex = -1;
            stack.Push(o);
        }
    }

    private ObstacleTypeSO PickWeightedType()
    {
        if (anyTypes.Count == 0) return null;

        float wUnavoidable = unavoidableTypes.Count > 0 ? 0.35f : 0f;
        float wJump = jumpableTypes.Count > 0 ? 0.30f : 0f;
        float wSlide = slideableTypes.Count > 0 ? 0.25f : 0f;
        float wBoth = bothTypes.Count > 0 ? 0.10f : 0f;
        float total = wUnavoidable + wJump + wSlide + wBoth;
        if (total <= 0f) return anyTypes[UnityEngine.Random.Range(0, anyTypes.Count)];

        float r = UnityEngine.Random.value * total;
        if (r < wUnavoidable) return unavoidableTypes[UnityEngine.Random.Range(0, unavoidableTypes.Count)];
        r -= wUnavoidable;
        if (r < wJump) return jumpableTypes[UnityEngine.Random.Range(0, jumpableTypes.Count)];
        r -= wJump;
        if (r < wSlide && slideableTypes.Count > 0) return slideableTypes[UnityEngine.Random.Range(0, slideableTypes.Count)];
        if (bothTypes.Count > 0) return bothTypes[UnityEngine.Random.Range(0, bothTypes.Count)];
        if (slideableTypes.Count > 0) return slideableTypes[UnityEngine.Random.Range(0, slideableTypes.Count)];
        return anyTypes[UnityEngine.Random.Range(0, anyTypes.Count)];
    }

    private float InvisibleAhead()
    {
        float ahead = config != null ? config.spawnDistanceAhead : 0f;
        if (config != null)
        {
            ahead = Mathf.Max(ahead, config.cameraFarClip + 20f);
            if (config.fogEnabled) ahead = Mathf.Max(ahead, config.fogEnd + 10f);
        }
        return ahead;
    }

    private static void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        if (colorPropertyBlock == null) colorPropertyBlock = new MaterialPropertyBlock();
        colorPropertyBlock.Clear();
        colorPropertyBlock.SetColor(BaseColorId, color);
        colorPropertyBlock.SetColor(ColorId, color);
        renderer.SetPropertyBlock(colorPropertyBlock);
    }

    private void CompactActiveObstaclesIfNeeded()
    {
        if (activeHead <= 0) return;
        if (activeHead < 256 && activeHead < active.Count / 2) return;

        active.RemoveRange(0, activeHead);
        for (int i = 0; i < active.Count; i++)
        {
            var obstacle = active[i];
            if (obstacle == null) continue;
            obstacle.ActiveListIndex = i;
        }
        activeHead = 0;
    }

    private void CompactActivePickupsIfNeeded()
    {
        if (activePickupsHead <= 0) return;
        if (activePickupsHead < 256 && activePickupsHead < activePickups.Count / 2) return;

        activePickups.RemoveRange(0, activePickupsHead);
        for (int i = 0; i < activePickups.Count; i++)
        {
            var pickup = activePickups[i];
            if (pickup == null) continue;
            pickup.ActiveListIndex = i;
        }
        activePickupsHead = 0;
    }

    private void BuildPickupTypeCache()
    {
        pickupTypesCached = null;
        pickupTypeCdf = null;
        pickupTypeTotalWeight = 0f;

        if (config == null || config.pickupTypes == null || config.pickupTypes.Length == 0) return;

        int count = 0;
        for (int i = 0; i < config.pickupTypes.Length; i++)
        {
            if (config.pickupTypes[i] != null) count++;
        }
        if (count == 0) return;

        pickupTypesCached = new PickupTypeSO[count];
        pickupTypeCdf = new float[count];

        float total = 0f;
        int write = 0;
        for (int i = 0; i < config.pickupTypes.Length; i++)
        {
            var type = config.pickupTypes[i];
            if (type == null) continue;
            total += Mathf.Max(0.01f, type.spawnWeight);
            pickupTypesCached[write] = type;
            pickupTypeCdf[write] = total;
            write++;
        }

        pickupTypeTotalWeight = total;
    }
}
