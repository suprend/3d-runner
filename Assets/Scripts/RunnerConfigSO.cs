using UnityEngine;

[CreateAssetMenu(menuName = "Runner/Runner Config", fileName = "RunnerConfig")]
public class RunnerConfigSO : ScriptableObject
{
    [Header("Presentation")]
    public string gameTitle = "3D Runner";
    public string developerName = "Mikhail Pospelov";

    [Header("Lanes")]
    [Min(0f)] public float laneOffset = 2f;
    public int minLaneIndex = -1;
    public int maxLaneIndex = 1;

    [Header("Player Movement")]
    [Min(0f)] public float forwardSpeedStart = 8f;
    [Min(0f)] public float lateralSpeed = 12f;
    [Min(0f)] public float jumpVelocity = 7.5f;
    [Range(1, 2)] public int maxJumps = 1;

    [Header("Jump Feel")]
    [Min(0f)] public float coyoteTime = 0.10f;
    [Min(0f)] public float jumpBufferTime = 0.10f;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 1.0f;
    [Min(1f)] public float fallGravityMultiplier = 2.20f;
    [Min(1f)] public float lowJumpGravityMultiplier = 1.00f;
    [Min(0f)] public float maxFallSpeed = 25f;

    [Header("Down / Slide")]
    [Tooltip("Extra gravity multiplier while holding Down in mid-air (fast fall). 1 = normal gravity.")]
    [Min(1f)] public float fastFallGravityMultiplier = 3.50f;
    [Tooltip("How long the slide lasts (seconds).")]
    [Min(0f)] public float slideDurationSeconds = 0.45f;
    [Tooltip("Crouch/stand animation time at slide start/end (seconds). Lower = more responsive.")]
    [Min(0f)] public float slideTransitionSeconds = 0.08f;
    [Tooltip("Cooldown after a slide ends (seconds).")]
    [Min(0f)] public float slideCooldownSeconds = 0.10f;
    [Tooltip("How long after pressing Down we still allow starting a slide (seconds). The press must be on the ground.")]
    [Min(0f)] public float slideBufferSeconds = 0.12f;
    [Tooltip("Slide height multiplier for the collider/visual. 0.5 = half height.")]
    [Range(0.2f, 1f)] public float slideHeightMultiplier = 0.50f;

    [Header("Health")]
    [Min(1)] public int maxHealth = 3;
    [Min(0f)] public float deathRestartDelay = 1f;

    [Header("Restart")]
    [Min(0f)] public float fadeDuration = 0.3f;

    [Header("Damage")]
    [Min(0f)] public float damageInvulnerabilitySeconds = 0.35f;
    [Min(0f)] public float damageFlashSeconds = 0.12f;
    [Min(0f)] public float damageScreenFlashSeconds = 0.12f;
    [Range(0f, 1f)] public float damageScreenFlashAlpha = 0.30f;
    [Min(0f)] public float damageShakeSeconds = 0.15f;
    [Min(0f)] public float damageShakeAmplitude = 0.20f;

    [Header("Health Regen")]
    [Tooltip("Enable passive HP regeneration.")]
    public bool regenEnabled = true;
    [Tooltip("After last HP damage, wait this many seconds before regen can start.")]
    [Min(0f)] public float regenDelaySeconds = 8f;
    [Tooltip("Time between regen ticks (seconds). Each tick heals regenAmount HP.")]
    [Min(0.1f)] public float regenIntervalSeconds = 6f;
    [Tooltip("HP restored per regen tick.")]
    [Min(1)] public int regenAmount = 1;

    [Header("Obstacles")]
    public ObstacleTypeSO[] obstacleTypes;
    [Tooltip("Seconds between spawn rows at the start of the run.")]
    [Min(0.05f)] public float spawnIntervalStart = 1.2f;
    [Tooltip("Minimum seconds between spawn rows (after difficulty scaling).")]
    [Min(0.05f)] public float spawnIntervalMin = 0.45f;
    [Tooltip("Requested spawn horizon ahead of the player (meters). Effective horizon is also affected by camera/fog visibility.")]
    [Min(5f)] public float spawnDistanceAhead = 160f;
    [Tooltip("At run start, the first spawn row begins this many meters ahead of the player. Smaller = obstacles appear earlier.")]
    [Min(0f)] public float initialSpawnStartMeters = 30f;
    [Tooltip("Minimum number of rows pre-spawned immediately when the run starts.")]
    [Min(0)] public int initialSpawnMinRows = 14;
    [Tooltip("Minimum distance between consecutive spawn rows (meters).")]
    [Min(0f)] public float minRowGapMeters = 8f;
    [Tooltip("Despawn obstacles that are this many meters behind the player.")]
    [Min(5f)] public float cleanupDistanceBehind = 20f;
    [Min(0)] public int obstaclePoolPrewarmPerType = 30;
    [Min(1)] public int obstaclePoolMaxPerType = 60;

    [Header("Pickups")]
    [Tooltip("Enable pickups (shield / wall break / jump boost).")]
    public bool pickupsEnabled = true;
    [Tooltip("Chance to spawn a pickup on a row (0..1). Pickups only spawn on empty lanes.")]
    [Range(0f, 1f)] public float pickupChancePerRow = 0.08f;
    [Tooltip("Pickup types available for spawning.")]
    public PickupTypeSO[] pickupTypes;
    [Tooltip("Lane clear radius in meters. Obstacles on the player's current lane inside this radius are destroyed.")]
    [Min(1f)] public float laneClearRadius = 24f;
    [Tooltip("Vertical speed applied when consuming a jump boost in air.")]
    [Min(0f)] public float jumpBoostVelocity = 9f;
    [Tooltip("Cooldown after consuming a jump boost (seconds).")]
    [Min(0f)] public float jumpBoostCooldownSeconds = 2f;

    [Header("Score / Combo")]
    [Tooltip("Enable combo multiplier that grows over time without taking HP damage.")]
    public bool comboEnabled = true;
    [Tooltip("Points per meter at multiplier 1.0.")]
    [Min(0f)] public float basePointsPerMeter = 1f;
    [Tooltip("Every N seconds without HP damage, increase multiplier by comboStepMultiplier.")]
    [Min(0.1f)] public float comboStepSeconds = 6f;
    [Tooltip("Multiplier increase per combo step.")]
    [Min(0f)] public float comboStepMultiplier = 0.25f;
    [Tooltip("Maximum combo multiplier.")]
    [Min(1f)] public float comboMaxMultiplier = 3.0f;

    [Header("Difficulty")]
    [Tooltip("Forward speed growth per second. For logarithmic mode, this is the initial growth slope.")]
    [Min(0f)] public float speedIncreasePerSecond = 0.25f;
    [Tooltip("Use logarithmic growth for forward speed (fast early growth, slower later growth).")]
    public bool useLogarithmicSpeed = true;
    [Tooltip("Log curve shape for speed growth. Higher = stronger early growth and earlier slowdown.")]
    [Min(0.01f)] public float speedLogScale = 0.35f;
    [Tooltip("Hard cap for forward speed. Set 0 to disable cap.")]
    [Min(0f)] public float maxForwardSpeed = 22f;
    [Min(0f)] public float spawnIntervalDecreasePerSecond = 0.01f;

    [Header("Track")]
    [Min(1f)] public float trackWidth = 8f;
    [Min(1f)] public float trackSegmentLength = 30f;
    [Min(2)] public int trackSegmentCount = 12;
    [Min(0.1f)] public float trackThickness = 0.2f;
    public Color trackColor = new Color(0.043137256f, 0.05882353f, 0.101960786f, 1f);
    [Min(1f)] public float trackRecycleBehindPlayerDistance = 60f;

    [Header("Camera")]
    [Tooltip("Camera position = player position + this offset (world-space). X=side, Y=up, Z=behind (usually negative).")]
    public Vector3 cameraOffset = new Vector3(0f, 14f, -6f);
    [Tooltip("Camera LookAt target = player position + this offset. Increase Z to look further ahead.")]
    public Vector3 cameraLookAtOffset = new Vector3(0f, 1f, 10f);
    [Tooltip("SmoothDamp time (seconds). 0 = no smoothing.")]
    [Min(0f)] public float cameraSmoothTime = 0.1f;
    [Tooltip("Far clip distance (meters). If fog is enabled, actual far clip is limited to fogEnd + 10.")]
    [Min(10f)] public float cameraFarClip = 70f;

    [Header("Fog")]
    [Tooltip("Enable linear fog (also helps hide spawning/pop-in).")]
    public bool fogEnabled = true;
    [Tooltip("Fog color (linear fog).")]
    public Color fogColor = new Color(0.101960786f, 0.043137256f, 0.18039216f, 1f);
    [Tooltip("Fog start distance (meters).")]
    [Min(0f)] public float fogStart = 25f;
    [Tooltip("Fog end distance (meters). If fog is enabled, it also clamps camera far clip to fogEnd + 10.")]
    [Min(0f)] public float fogEnd = 65f;

    [Header("Feedback")]
    [Tooltip("Landing squash duration (seconds).")]
    [Min(0f)] public float landingSquashSeconds = 0.10f;
    [Tooltip("Landing squash amount (0..1). 0.10 = 10% squash.")]
    [Range(0f, 0.5f)] public float landingSquashAmount = 0.10f;
    [Tooltip("Lane change whoosh duration (seconds).")]
    [Min(0f)] public float laneWhooshSeconds = 0.12f;
    [Tooltip("Controls hint visibility time at run start (seconds). 0 = hide immediately.")]
    [Min(0f)] public float hintDurationSeconds = 5f;
}
