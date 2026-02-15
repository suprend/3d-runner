using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RunnerPlayerController : MonoBehaviour
{
    [SerializeField] private RunnerConfigSO config;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction abilityAction;
    private InputActionMap actionMap;
    private RunnerPowerups powerups;
    private RunnerObstacleSpawner spawner;

    private int laneIndex;
    private bool laneChangeArmed = true;
    private float forwardSpeed;
    private int jumpsRemaining;
    private float lastGroundedTime = -999f;
    private float jumpPressedTime = -999f;

    private bool downHeld;
    private bool downArmed = true;
    private float downPressedTime = -999f;
    private bool downPressedWhileGrounded;

    private bool isSliding;
    private float slideStartedAt = -999f;
    private float slideEndsAt = -999f;
    private float nextSlideAllowedTime = -999f;
    private float originalCapsuleHeight;
    private Vector3 originalCapsuleCenter;
    private bool hasOriginalCapsule;
    private Transform visual;
    private Vector3 originalVisualScale;
    private Vector3 originalVisualLocalPos;
    private Quaternion originalVisualLocalRot;
    private bool hasOriginalVisual;

    private const float DeadZone = 0.3f;
    private const float Trigger = 0.6f;
    private const float NegativeInfinityTime = -999f;

    private bool groundedCached;
    private bool wasGrounded;
    private bool anyActionSent;

    public bool IsSliding => isSliding;
    public bool IsGrounded => groundedCached;

    public event Action<int> LaneChanged;
    public event Action Jumped;
    public event Action Landed;
    public event Action SlideStarted;
    public event Action SlideEnded;
    public event Action AnyAction;

    public void Initialize(RunnerConfigSO runnerConfig, InputActionAsset actions)
    {
        config = runnerConfig;
        inputActions = actions;

        if (groundMask.value == ~0 || groundMask.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) groundMask = 1 << groundLayer;
        }

        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        powerups = GetComponent<RunnerPowerups>();

        CacheCapsuleDefaults();
        BindActions();
        ResetRun();
    }

    public void SetObstacleSpawner(RunnerObstacleSpawner obstacleSpawner)
    {
        spawner = obstacleSpawner;
    }

    public void ResetRun()
    {
        laneIndex = 0;
        laneChangeArmed = true;
        forwardSpeed = config != null ? config.forwardSpeedStart : 8f;
        jumpsRemaining = config != null ? Mathf.Clamp(config.maxJumps, 1, 2) : 1;
        lastGroundedTime = NegativeInfinityTime;
        jumpPressedTime = NegativeInfinityTime;
        downHeld = false;
        downArmed = true;
        downPressedTime = NegativeInfinityTime;
        groundedCached = false;
        wasGrounded = false;
        anyActionSent = false;
        EndSlide(applyCooldown: false);
        rb.linearVelocity = Vector3.zero;
    }

    public void SetForwardSpeed(float speed)
    {
        forwardSpeed = Mathf.Max(0f, speed);
    }

    public void Freeze(bool frozen)
    {
        if (rb == null) return;
        if (frozen)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = frozen;
    }

    private void BindActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("RunnerPlayerController: InputActionAsset is null.");
            return;
        }

        actionMap = inputActions.FindActionMap("Player", true);
        moveAction = actionMap.FindAction("Move", true);
        jumpAction = actionMap.FindAction("Jump", true);
        abilityAction = actionMap.FindAction("Ability", false);
        actionMap.Enable();
    }

    private void Update()
    {
        if (config == null || moveAction == null || jumpAction == null) return;

        var move = moveAction.ReadValue<Vector2>();
        float x = move.x;
        HandleLaneInput(x);

        downHeld = move.y < -Trigger;
        if (!downHeld)
        {
            downArmed = true;
            downPressedWhileGrounded = false;
        }
        else if (downArmed)
        {
            downArmed = false;
            downPressedTime = Time.time;
            float vy = rb != null ? rb.linearVelocity.y : 0f;
            downPressedWhileGrounded = groundedCached && vy <= 0.05f;
            MarkAnyAction();
        }

        if (jumpAction.triggered)
        {
            jumpPressedTime = Time.time;
            MarkAnyAction();
        }

        bool abilityPressed = abilityAction != null
            ? abilityAction.triggered
            : (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
        if (abilityPressed)
        {
            TryUseWallBreak();
            MarkAnyAction();
        }
    }

    private void FixedUpdate()
    {
        if (config == null) return;

        bool grounded = ComputeGrounded();
        groundedCached = grounded;
        if (grounded) lastGroundedTime = Time.time;

        bool landedThisFrame = !wasGrounded && grounded;
        if (landedThisFrame)
        {
            Landed?.Invoke();
        }

        if (grounded && rb.linearVelocity.y <= 0f)
        {
            jumpsRemaining = Mathf.Clamp(config.maxJumps, 1, 2);
        }

        float slideBuffer = Mathf.Max(0f, config.slideBufferSeconds);
        bool slideBuffered = Time.time - downPressedTime <= slideBuffer;
        bool fromGroundPress = slideBuffered && grounded && downHeld && downPressedWhileGrounded;
        bool fromAirHoldLanding = landedThisFrame && downHeld;
        if ((fromGroundPress || fromAirHoldLanding) && !isSliding && Time.time >= nextSlideAllowedTime)
        {
            downPressedTime = NegativeInfinityTime;
            downPressedWhileGrounded = false;
            StartSlide();
        }

        if (isSliding) UpdateSlidePose();

        if (isSliding && grounded && !downHeld)
        {
            float transition = Mathf.Clamp(config.slideTransitionSeconds, 0f, Mathf.Max(0f, config.slideDurationSeconds) * 0.5f);
            float quickEnd = Time.time + Mathf.Max(0f, transition);
            if (quickEnd < slideEndsAt) slideEndsAt = quickEnd;
        }

        if (isSliding && Time.time >= slideEndsAt)
        {
            EndSlide();
        }

        TryConsumeBufferedJump(grounded);

        float targetX = laneIndex * config.laneOffset;
        float deltaX = targetX - rb.position.x;
        float desiredXVel = Mathf.Clamp(deltaX / Time.fixedDeltaTime, -config.lateralSpeed, config.lateralSpeed);

        var v = rb.linearVelocity;
        v.x = desiredXVel;
        v.z = forwardSpeed;

        if (!grounded && downHeld && v.y > 0f)
        {
            v.y = 0f;
        }

        rb.linearVelocity = v;

        ApplyExtraGravity();

        wasGrounded = grounded;
    }

    private void ApplyExtraGravity()
    {
        if (rb == null || config == null) return;

        var v = rb.linearVelocity;
        if (downHeld && v.y > 0f)
        {
            float mult = Mathf.Max(1f, config.fastFallGravityMultiplier);
            rb.AddForce(Physics.gravity * (mult - 1f), ForceMode.Acceleration);
        }
        else if (v.y < 0f)
        {
            float mult = downHeld
                ? Mathf.Max(1f, config.fastFallGravityMultiplier)
                : Mathf.Max(1f, config.fallGravityMultiplier);
            rb.AddForce(Physics.gravity * (mult - 1f), ForceMode.Acceleration);
        }

        v = rb.linearVelocity;
        float maxFall = Mathf.Max(0f, config.maxFallSpeed);
        if (maxFall > 0f && v.y < -maxFall)
        {
            v.y = -maxFall;
            rb.linearVelocity = v;
        }
    }

    private void HandleLaneInput(float x)
    {
        if (Mathf.Abs(x) < DeadZone)
        {
            laneChangeArmed = true;
            return;
        }

        if (!laneChangeArmed) return;

        if (x > Trigger)
        {
            int before = laneIndex;
            laneIndex = Mathf.Min(laneIndex + 1, config.maxLaneIndex);
            laneChangeArmed = false;
            if (laneIndex != before)
            {
                LaneChanged?.Invoke(laneIndex);
                MarkAnyAction();
            }
        }
        else if (x < -Trigger)
        {
            int before = laneIndex;
            laneIndex = Mathf.Max(laneIndex - 1, config.minLaneIndex);
            laneChangeArmed = false;
            if (laneIndex != before)
            {
                LaneChanged?.Invoke(laneIndex);
                MarkAnyAction();
            }
        }
    }

    private void TryConsumeBufferedJump(bool grounded)
    {
        if (config == null) return;

        bool buffered = Time.time - jumpPressedTime <= Mathf.Max(0f, config.jumpBufferTime);
        if (!buffered) return;

        bool coyote = grounded || (Time.time - lastGroundedTime <= Mathf.Max(0f, config.coyoteTime));
        bool allowMidair = config.maxJumps > 1;
        bool canUseNormalJump = jumpsRemaining > 0 && (coyote || allowMidair);
        if (canUseNormalJump)
        {
            if (isSliding)
            {
                EndSlide();
            }

            jumpPressedTime = -999f;
            jumpsRemaining--;

            var v = rb.linearVelocity;
            v.y = config.jumpVelocity;
            rb.linearVelocity = v;

            Jumped?.Invoke();
            MarkAnyAction();
            return;
        }

        if (grounded) return;
        if (TryConsumeJumpBoost())
        {
            if (isSliding) EndSlide();

            jumpPressedTime = -999f;

            var v = rb.linearVelocity;
            float boostVel = Mathf.Max(0f, config.jumpBoostVelocity);
            v.y = boostVel > 0f ? boostVel : config.jumpVelocity;
            rb.linearVelocity = v;

            Jumped?.Invoke();
            MarkAnyAction();
        }
    }

    private bool TryConsumeJumpBoost()
    {
        if (powerups == null) powerups = GetComponent<RunnerPowerups>();
        if (powerups == null) return false;
        float cd = config != null ? Mathf.Max(0f, config.jumpBoostCooldownSeconds) : 2f;
        return powerups.TryConsumeJumpBoost(cd);
    }

    private void TryUseWallBreak()
    {
        if (powerups == null) powerups = GetComponent<RunnerPowerups>();
        if (powerups == null) return;

        if (spawner == null) spawner = FindFirstObjectByType<RunnerObstacleSpawner>();
        if (spawner == null) return;
        if (!powerups.TryConsumeWallBreak()) return;

        float radius = config != null ? Mathf.Max(1f, config.laneClearRadius) : 24f;
        spawner.ClearLaneObstacles(transform.position, radius);
    }

    private void StartSlide()
    {
        if (capsule == null || config == null) return;
        CacheCapsuleDefaults();
        CacheVisualDefaults();

        isSliding = true;
        slideStartedAt = Time.time;
        slideEndsAt = slideStartedAt + Mathf.Max(0f, config.slideDurationSeconds);
        UpdateSlidePose();

        SlideStarted?.Invoke();
    }

    private void EndSlide(bool applyCooldown = true)
    {
        bool was = isSliding;
        if (isSliding && capsule != null && hasOriginalCapsule)
        {
            capsule.height = originalCapsuleHeight;
            capsule.center = originalCapsuleCenter;
        }

        ResetVisual();

        if (applyCooldown && config != null)
        {
            nextSlideAllowedTime = Time.time + Mathf.Max(0f, config.slideCooldownSeconds);
        }
        isSliding = false;
        slideStartedAt = NegativeInfinityTime;
        slideEndsAt = -999f;

        if (was) SlideEnded?.Invoke();
    }

    private void MarkAnyAction()
    {
        if (anyActionSent) return;
        anyActionSent = true;
        AnyAction?.Invoke();
    }

    private void CacheCapsuleDefaults()
    {
        if (capsule == null || hasOriginalCapsule) return;
        originalCapsuleHeight = capsule.height;
        originalCapsuleCenter = capsule.center;
        hasOriginalCapsule = true;
    }

    private void CacheVisualDefaults()
    {
        if (hasOriginalVisual) return;

        if (visual == null)
        {
            visual = transform.Find("Visual");
            if (visual == null)
            {
                var r = GetComponentInChildren<Renderer>(true);
                if (r != null) visual = r.transform;
            }
        }

        if (visual == null) return;
        originalVisualScale = visual.localScale;
        originalVisualLocalPos = visual.localPosition;
        originalVisualLocalRot = visual.localRotation;
        hasOriginalVisual = true;
    }

    private void UpdateSlidePose()
    {
        if (!isSliding) return;
        if (capsule == null || config == null || !hasOriginalCapsule) return;

        float duration = Mathf.Max(0f, config.slideDurationSeconds);
        float transition = Mathf.Clamp(config.slideTransitionSeconds, 0f, duration * 0.5f);

        float t = Time.time;
        float factor;

        if (duration <= 0f) factor = 0f;
        else if (transition <= 0f) factor = 1f;
        else if (t < slideStartedAt + transition) factor = Smooth01((t - slideStartedAt) / transition);
        else if (t > slideEndsAt - transition) factor = Smooth01((slideEndsAt - t) / transition);
        else factor = 1f;

        float targetMult = Mathf.Clamp(config.slideHeightMultiplier, 0.2f, 1f);
        float currentHeight = Mathf.Lerp(originalCapsuleHeight, Mathf.Max(0.2f, originalCapsuleHeight * targetMult), factor);
        float delta = originalCapsuleHeight - currentHeight;

        capsule.height = currentHeight;
        capsule.center = originalCapsuleCenter + new Vector3(0f, -delta * 0.5f, 0f);

        ApplySlideVisual(factor);
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ApplySlideVisual(float factor)
    {
        if (!hasOriginalVisual || visual == null || config == null) return;

        float targetY = Mathf.Clamp(config.slideHeightMultiplier, 0.2f, 1f);
        float y = Mathf.Lerp(1f, targetY, factor);
        float widen = Mathf.Lerp(1f, 1.12f, factor);

        visual.localScale = new Vector3(originalVisualScale.x * widen, originalVisualScale.y * y, originalVisualScale.z * widen);
        visual.localPosition = originalVisualLocalPos + new Vector3(0f, -originalVisualScale.y * (1f - y), 0f);
    }

    private void ResetVisual()
    {
        if (!hasOriginalVisual || visual == null) return;
        visual.localScale = originalVisualScale;
        visual.localPosition = originalVisualLocalPos;
        visual.localRotation = originalVisualLocalRot;
    }

    private bool ComputeGrounded()
    {
        if (capsule == null) return false;

        float radius = Mathf.Max(0.05f, capsule.radius * 0.95f);
        float extra = 0.08f;

        var bounds = capsule.bounds;
        var origin = bounds.center;
        float distance = bounds.extents.y + extra;

        return Physics.SphereCast(origin, radius, Vector3.down, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
    }
}
