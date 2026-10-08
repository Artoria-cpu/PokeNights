using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>E: toss and kick the tethered sword; press E again to pull it back through enemies.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(-30)]
public sealed class SwordThrowSkill : MonoBehaviour
{
    public const string ThrowState = "SwordSkillThrow", KickState = "SwordSkillKick", PullState = "SwordSkillPull";
    public CharacterMotor driver;
    public CharacterWeaponEquipment equipment;
    public AnimationClip throwClip, kickClip, pullClip;
    public GameObject flightEffectsPrefab;
    public GameObject spinEffectsPrefab;
    public Material kickWindMaterial;
    [Header("Animation markers (on the trimmed clips)")]
    [Range(0, 1)] public float releaseProgress = 25f / 46f;
    [Range(0, 1)] public float kickProgress = 18f / 44f;
    [Min(.01f)] public float animationBlend = .16f;
    [Min(.1f)] public float throwPlaybackSpeed = 1.8f, kickPlaybackSpeed = 1.2f;
    [Header("Airborne spin")]
    [Min(.1f)] public float catchForward = 1.2f, catchHeight = 1.4f;
    [Tooltip("Downward acceleration during the airborne toss, in metres per second squared.")]
    [Min(.1f)] public float tossGravity = 12f;
    [Tooltip("Extra height at the middle of the toss, preserving the kick contact time.")]
    [Min(0)] public float tossHeightBoost = .5f;
    [Min(0)] public float aimTurnSpeed = 900f;
    public float spinDegreesPerSecond = 1440f;
    [Min(.05f)] public float spinRadius = .92f, spinHalfWidth = .62f;
    [Header("Skill damage")]
    [Tooltip("Minimum game seconds between hits on the same target, shared by spin and projectile.")]
    [Min(.1f)] public float hitInterval = .3f;
    [Header("Kick projectile")]
    [Min(0)] public float kickContactHitStop = .15f;
    [Min(.1f)] public float projectileSpeed = 22f, projectileRange = 14f;
    [Min(.1f)] public float projectileInitialSpeed = 32f;
    [Min(.01f)] public float launchBoostSeconds = .18f;
    [Min(.05f)] public float projectileRadius = .48f;
    [Min(0)] public float projectileGravity = 5.5f;
    [Range(.02f, .4f)] public float embedDepth = .18f;
    [Min(0)] public float knockbackDistance = 2.1f;
    [Min(.05f)] public float knockbackSeconds = .28f;
    [Header("Manual rope recall")]
    public Material ropeMaterial;
    public Color ropeColor = new Color(.72f, .43f, .21f, 1);
    [Min(.005f)] public float ropeWidth = .022f;
    [Min(0)] public float ropeSag = .55f;
    [Min(0)] public float ropeGravity = 9.81f, ropeDamping = 4f;
    [Min(.1f)] public float pullPlaybackSpeed = 1f;
    [Range(0, 1)] public float pullGripProgress = .1672f / .91f;
    [Range(0, 1)] public float pullProgress = .5756f / .91f;
    [Tooltip("Authored Harvesting turn in degrees, relative to the facing captured at recall start. The skill owns this yaw throughout the pull.")]
    public AnimationCurve pullTurnYaw = AnimationCurve.EaseInOut(.1672f / .91f, 0, .4256f / .91f, -116f);
    [Tooltip("Normalized accumulated hand pull, sampled from the one-shot pull clip.")]
    public AnimationCurve pullStroke = AnimationCurve.EaseInOut(.5756f / .91f, 0, .834f / .91f, 1);
    [Min(.1f)] public float returnSpeed = 40f, returnAcceleration = 220f;
    [Min(.1f)] public float returnSpring = 45f, returnDamping = 4f;
    [Min(0)] public float cooldownSeconds = 12f;
    [SerializeField, Tooltip("Last rejected E request, for diagnosing input without running an automated playthrough.")]
    string lastCastBlockReason;
    public string LastCastBlockReason => lastCastBlockReason;
    public bool IsPreparingCast => pendingDraw;
    public bool IsCasting { get; private set; }
    public bool OwnsBody => IsCasting && !bodyReleased;
    public bool WaitingForRecall => IsCasting && phase == BladePhase.Embedded;
    public bool RopeTaut => recalling && currentPullProgress >= pullGripProgress && phase != BladePhase.Caught;
    public Vector3 ActionForward
    {
        get
        {
            if (!recalling) return Forward;
            // Do not blend toward the blade/lock heading during the authored turn: that
            // correction could rotate against it and cancel the visible body motion.
            float turn = pullTurnYaw != null ? pullTurnYaw.Evaluate(Mathf.Clamp01(currentPullProgress)) : 0;
            return Quaternion.AngleAxis(turn, Vector3.up) * recallStartForward;
        }
    }
    public Vector3 Forward { get; private set; }
    public float CooldownRemaining => Mathf.Max(0, readyAt - Time.time);
    public void ReduceCooldown(float seconds)
    {
        if(!IsCasting)readyAt=Mathf.Max(Time.time,readyAt-Mathf.Max(0,seconds));
    }
    public int CastCount { get; private set; }
    public int SpinHitCount { get; private set; }
    public int ProjectileHitCount { get; private set; }
    public int ReturnHitCount { get; private set; }

    enum BladePhase { Held, Spin, Projectile, Embedded, Returning, Caught }
    BladePhase phase;
    Transform sword, socket;
    GameObject effects, spinEffects;
    SwordTether rope;
    Vector3 savedScale, center, tossFrom, flightVelocity, returnVelocity, recallStartForward;
    Quaternion tossRotation, flightRotation, returnRotation;
    float readyAt, releaseAt, spinDamageStartsAt, flightDistance, returnAt, castAt, kickAt, unexpectedStateSince;
    float tossDuration, tossVerticalSpeed, tossAcceleration, projectileAge, pendingDrawAt;
    float pullAt, returnDistance, strokeAtReturn, currentPullProgress;
    bool kicking, animationDone, pendingDraw, bodyReleased, recalling, recallQueued;
    CharacterComboAttack combo;
    CharacterController controller;
    readonly Collider[] contacts = new Collider[64];
    readonly RaycastHit[] obstacles = new RaycastHit[32];
    readonly Dictionary<int, float> nextHitByTarget = new Dictionary<int, float>();
    static readonly Vector3 BladeCenter = Vector3.up * .48f;
    float ThrowDuration => throwClip.length / Mathf.Max(.1f, throwPlaybackSpeed);
    float KickDuration => kickClip.length / Mathf.Max(.1f, kickPlaybackSpeed);
    float PullDuration => pullClip.length / Mathf.Max(.1f, pullPlaybackSpeed);
    // Keep the rotation plane vertical and aligned with the caster's heading, with no side cant.
    Vector3 SpinNormal => Vector3.Cross(Vector3.up, Forward);

    void Awake()
    {
        if (driver == null) driver = GetComponent<CharacterMotor>();
        combo = GetComponent<CharacterComboAttack>();
        controller = GetComponent<CharacterController>();
        if (equipment == null) equipment = FindAnyObjectByType<CharacterWeaponEquipment>();
    }
    bool IsOwner => driver != null && driver.enabled && driver.acceptPlayerInput && equipment != null
        && equipment.selection != null && equipment.selection.ActiveCharacter == driver && equipment.HasSword && equipment.SelectedSlot == 1;

    void Update()
    {
        if (IsCasting)
        {
            if (!IsOwner || (driver.IsActionBlocked && OwnsBody) || equipment.ActiveSkill != this || sword == null || driver.animator == null || !driver.animator.isActiveAndEnabled)
                Cancel(driver != null && !driver.IsActionBlocked);
            else
            {
                if (phase == BladePhase.Held || phase == BladePhase.Spin) TrackTarget();
                // Waiting for the player's second press has no automatic return timeout.
                if ((phase == BladePhase.Held || phase == BladePhase.Spin) && Time.time - castAt > ThrowDuration + KickDuration + .5f) Cancel(true);
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame
                    && (phase == BladePhase.Projectile || phase == BladePhase.Embedded)) recallQueued = true;
                if (recallQueued && bodyReleased) RequestRecall();
            }
            return;
        }
        if (pendingDraw)
        {
            if (!IsOwner || driver.IsActionBlocked || driver.IsDodging || driver.JumpPreparing)
            { pendingDraw = false; lastCastBlockReason = "拉剑准备被动作或角色切换中断"; }
            else if (Time.time - pendingDrawAt > CastPreparationSeconds)
            { pendingDraw = false; lastCastBlockReason = GroundedForCast ? "拔剑未完成" : "角色尚未落地"; }
            else TryBeginPreparedCast();
            return;
        }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) RequestCast();
    }

    public bool RequestCast()
    {
        if (driver!=null&&driver.BallInputBlocked) return false;
        if (pendingDraw) return true;
        if (IsCasting) return RejectCast("飞剑已经在外，第二次 E 用于收回");
        if (!IsOwner) return RejectCast("需要主控角色已获得剑，并选择剑武器（2）");
        if (driver.animator == null || throwClip == null || kickClip == null || pullClip == null) return RejectCast("技能动画未绑定");
        if (driver.IsActionBlocked || driver.IsDodging || driver.JumpPreparing) return RejectCast("正在受击、闪避或起跳");
        if (combo != null && combo.IsAttacking) return RejectCast("当前攻击尚未结束");
        if (CooldownRemaining > 0) return RejectCast("技能冷却中");
        if (!driver.animator.HasState(0, Animator.StringToHash(ThrowState)) || !driver.animator.HasState(0, Animator.StringToHash(KickState))
            || !driver.animator.HasState(0, Animator.StringToHash(PullState))) return RejectCast("Animator 缺少技能状态");
        // An E request itself reserves a drawn sword. Lock-on and enemy proximity only aim it.
        pendingDraw = true; pendingDrawAt = Time.time; lastCastBlockReason = "";
        if (!equipment.RequestAttackDraw(driver)) { pendingDraw = false; return RejectCast("无法拔剑"); }
        TryBeginPreparedCast();
        return true;
    }
    float CastPreparationSeconds => Mathf.Max(.5f, equipment.drawClip != null
        ? equipment.drawClip.length / Mathf.Max(.1f, equipment.animationSpeed * equipment.attackDrawSpeedMultiplier) + .4f : .8f);
    bool GroundedForCast => driver.IsGrounded || (controller != null && controller.enabled && controller.isGrounded);
    bool RejectCast(string reason) { lastCastBlockReason = reason; return false; }
    void TryBeginPreparedCast()
    {
        // Grounding is written later in the frame by the driver. Keep the request through a
        // transient false flag, and finish any accelerated draw without needing a second E.
        if (!GroundedForCast || !equipment.IsDrawn || equipment.IsTransitioning || (combo != null && combo.IsAttacking)) return;
        if (!equipment.BeginSwordSkill(this, driver)) return;
        pendingDraw = false;
        combo?.CancelForControlChange();
        sword = equipment.Sword; socket = sword.parent; savedScale = sword.localScale;
        Forward = Vector3.ProjectOnPlane((driver.visualRoot != null ? driver.visualRoot : transform).forward, Vector3.up).normalized;
        Forward = LockedHeading(transform.position, Forward);
        if (Forward.sqrMagnitude < .01f) Forward = Vector3.forward;
        kicking = animationDone = bodyReleased = recalling = recallQueued = false; unexpectedStateSince = -1; phase = BladePhase.Held; nextHitByTarget.Clear();
        IsCasting = true; castAt = Time.time; readyAt = Time.time + cooldownSeconds; CastCount++;
        driver.SetAttackActive(true); driver.animator.SetBool("IsAttacking", true);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, ThrowState, animationBlend, 0, 0);
    }

    public bool RequestRecall()
    {
        if (!IsCasting || !IsOwner || !bodyReleased || (phase != BladePhase.Projectile && phase != BladePhase.Embedded)
            || pullClip == null || driver.IsActionBlocked || driver.IsDodging || driver.IsAttacking || !driver.IsGrounded || driver.JumpPreparing
            || !driver.animator.HasState(0, Animator.StringToHash(PullState))) return false;
        var heading = Vector3.ProjectOnPlane(center - transform.position, Vector3.up);
        if (heading.sqrMagnitude > .001f) Forward = heading.normalized;
        var body = driver.visualRoot != null ? driver.visualRoot : transform;
        recallStartForward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        if (recallStartForward.sqrMagnitude < .001f) recallStartForward = Forward;
        combo?.CancelForControlChange();
        recalling = true; recallQueued = animationDone = bodyReleased = false;
        pullAt = Time.time; currentPullProgress = 0; unexpectedStateSince = -1;
        driver.equipmentBusy = true;
        driver.SetAttackActive(true); driver.animator.SetBool("IsAttacking", true);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, PullState, animationBlend, 0, 0);
        return true;
    }

    void LateUpdate()
    {
        if (!IsCasting || Time.deltaTime <= 0) return;
        var animator = driver.animator;
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (recalling)
        {
            UpdateRecall(state);
            return;
        }
        bool expected = state.IsName(kicking ? KickState : ThrowState);
        if (OwnsBody && !expected)
        {
            if (unexpectedStateSince < 0) unexpectedStateSince = Time.time;
            if (Time.time - unexpectedStateSince > animationBlend + .08f) { Cancel(true); return; }
        }
        else unexpectedStateSince = -1;
        if (!kicking)
        {
            float progress = state.IsName(ThrowState) ? state.normalizedTime : 0;
            if (phase == BladePhase.Held && (progress >= releaseProgress || Time.time - castAt >= ThrowDuration * releaseProgress + .12f)) Release();
            if (progress >= 1 || Time.time - castAt >= ThrowDuration + .15f)
            {
                kicking = true; kickAt = Time.time; unexpectedStateSince = -1;
                driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, KickState, animationBlend, 0, 0);
            }
        }
        float kickTime = kicking ? (Time.time - kickAt) / Mathf.Max(.01f, KickDuration) : 0;
        if (kicking)
        {
            if (state.IsName(KickState)) kickTime = Mathf.Max(kickTime, state.normalizedTime);
            // Launch sets the contact pose and freezes gameplay; do not advance the projectile
            // using this frame's already-computed delta time after starting hit-stop.
            if (phase == BladePhase.Spin && kickTime >= kickProgress) { Launch(); return; }
            // Latch completion. An external crossfade must not make a completed action incomplete again.
            animationDone |= kickTime >= .98f;
            if (animationDone && !bodyReleased) ReleaseBody(true);
        }
        if (phase == BladePhase.Spin) Spin(kickTime);
        else if (phase == BladePhase.Projectile) Fly();
    }

    void UpdateRecall(AnimatorStateInfo state)
    {
        if (OwnsBody && !state.IsName(PullState))
        {
            if (unexpectedStateSince < 0) unexpectedStateSince = Time.time;
            if (Time.time - unexpectedStateSince > animationBlend + .08f)
            { StopPull(false); return; }
        }
        else unexpectedStateSince = -1;
        float progress = (Time.time - pullAt) / Mathf.Max(.01f, PullDuration);
        if (state.IsName(PullState)) progress = Mathf.Max(progress, state.normalizedTime);
        currentPullProgress = progress;
        if (phase == BladePhase.Projectile || phase == BladePhase.Embedded)
        {
            var toward = Vector3.ProjectOnPlane(center - transform.position, Vector3.up);
            if (toward.sqrMagnitude > .01f) Forward = toward.normalized;
            if (progress >= pullProgress) BeginReturn();
            else if (phase == BladePhase.Projectile) Fly();
        }
        animationDone |= progress >= .98f;
        if (animationDone && !bodyReleased) ReleaseBody(true);
        if (OwnsBody)
        {
            var body = driver.visualRoot != null ? driver.visualRoot : transform;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(ActionForward), aimTurnSpeed * Time.deltaTime);
        }
        if (phase == BladePhase.Returning) Return();
        if (phase == BladePhase.Caught && animationDone) Cancel(true);
    }

    Transform LockedTarget => equipment != null && equipment.lockOn != null ? equipment.lockOn.Target : driver.combatLookTarget;
    Vector3 LockedHeading(Vector3 origin, Vector3 fallback)
    {
        var target = LockedTarget;
        if (target == null || !target.gameObject.activeInHierarchy) return fallback;
        var delta = Vector3.ProjectOnPlane(target.position - origin, Vector3.up);
        return delta.sqrMagnitude > .001f ? delta.normalized : fallback;
    }
    void TrackTarget()
    {
        Forward = Vector3.RotateTowards(Forward, LockedHeading(transform.position, Forward), aimTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0).normalized;
    }

    void Release()
    {
        tossFrom = sword.TransformPoint(BladeCenter); tossRotation = sword.rotation;
        sword.SetParent(null, true); center = tossFrom;
        releaseAt = Time.time; spinDamageStartsAt = Time.time + .08f; phase = BladePhase.Spin;
        tossDuration = Mathf.Max(.3f, ThrowDuration - (Time.time - castAt) + KickDuration * kickProgress);
        float dropHeight = transform.position.y + catchHeight;
        // Raise the arc while retaining constant downward acceleration and the same landing time.
        tossAcceleration = tossGravity + 8f * tossHeightBoost / (tossDuration * tossDuration);
        tossVerticalSpeed = (dropHeight - tossFrom.y) / tossDuration + .5f * tossAcceleration * tossDuration;
        if (flightEffectsPrefab != null) effects = Instantiate(flightEffectsPrefab, sword, false);
        if (spinEffectsPrefab != null) spinEffects = Instantiate(spinEffectsPrefab, center, Quaternion.LookRotation(SpinNormal));
        if (ropeMaterial != null)
        {
            var ropeObject = new GameObject("Hand to Sword Rope"); ropeObject.transform.SetParent(transform, false);
            var line = ropeObject.AddComponent<LineRenderer>(); line.sharedMaterial = ropeMaterial;
            line.widthMultiplier = ropeWidth; line.startColor = line.endColor = ropeColor;
            line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            rope = ropeObject.AddComponent<SwordTether>(); rope.Initialize(this, socket, sword);
        }
    }
    void SetBlade(Vector3 position, Quaternion rotation)
    {
        center = position;
        sword.rotation = rotation;
        sword.position = position - rotation * Vector3.Scale(BladeCenter, sword.lossyScale);
    }
    Vector3 KickCenter()
    {
        var foot = driver.animator.GetBoneTransform(HumanBodyBones.RightFoot);
        // Put the hilt just ahead of the boot, with the blade pointing away from the caster.
        var point = foot != null ? foot.position : transform.position + Vector3.up * 1.15f + Forward * .75f;
        point.y = Mathf.Max(point.y, transform.position.y + .85f);
        return point + Forward * .55f;
    }
    void Spin(float kickTime)
    {
        float age = Time.time - releaseAt;
        float t = Mathf.Min(age, tossDuration);
        var landing = transform.position + Forward * catchForward;
        var position = Vector3.Lerp(tossFrom, landing, Mathf.SmoothStep(0, 1, t / tossDuration));
        position.y = tossFrom.y + tossVerticalSpeed * t - .5f * tossAcceleration * t * t;
        var rotation = Quaternion.AngleAxis(age * spinDegreesPerSecond, SpinNormal) * Quaternion.LookRotation(SpinNormal);
        rotation = Quaternion.Slerp(tossRotation, rotation, Mathf.Clamp01(age / .06f));
        if (kicking)
        {
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(kickProgress - .12f, kickProgress, kickTime));
            position = Vector3.Lerp(position, KickCenter(), settle);
            float stopSpin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(kickProgress - .045f, kickProgress, kickTime));
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(-Vector3.up, Forward), stopSpin);
        }
        SetBlade(position, rotation);
        if (spinEffects != null)
        {
            spinEffects.transform.SetPositionAndRotation(center, Quaternion.LookRotation(SpinNormal) * Quaternion.Euler(0, 0, age * spinDegreesPerSecond));
            float appear = Mathf.Clamp01(age / .075f);
            float disappear = kicking ? 1 - Mathf.InverseLerp(kickProgress - .05f, kickProgress, kickTime) : 1;
            spinEffects.transform.localScale = Vector3.one * (appear * disappear);
        }
        if (Time.time >= spinDamageStartsAt)
        {
            // A vertically oriented disk in front, widened slightly across the blade for reliable contact.
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(spinRadius, spinRadius, spinHalfWidth), contacts,
                Quaternion.LookRotation(SpinNormal), ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Hit(contacts[i], false);
        }
    }
    void Launch()
    {
        GameplayAudio.Play(GameplayCue.SwordHeavy,transform.position+Vector3.up,.65f);
        Forward = LockedHeading(transform.position, Forward);
        var origin = KickCenter();
        var preciseHeading = LockedHeading(origin, Forward);
        // A point-blank target already overlaps the hilt; do not send the blade backwards through its owner.
        if (Vector3.Dot(preciseHeading, Forward) > .05f) Forward = preciseHeading;
        flightRotation = Quaternion.LookRotation(-Vector3.up, Forward);
        SetBlade(origin, flightRotation);
        if (spinEffects != null) Destroy(spinEffects);
        // Preserve each target's cooldown across the kick, so phase changes cannot double-hit it.
        phase = BladePhase.Projectile; flightDistance = projectileAge = 0;
        flightVelocity = Forward * projectileInitialSpeed;
        if (effects != null) foreach (var trail in effects.GetComponentsInChildren<TrailRenderer>()) trail.Clear();
        var windNormal=Vector3.ProjectOnPlane(Forward,Vector3.up).normalized;
        if(windNormal.sqrMagnitude<.001f)windNormal=Vector3.forward;
        CombatWindRing.CreatePair(origin,windNormal,kickWindMaterial,true,.45f);
        CombatHitFeedback.Play(sword.position, driver.movementCamera, sword: true, direction: Forward,
            hitStopOverride: kickContactHitStop, unscaledParticles: true);
    }
    void Fly()
    {
        int steps = Mathf.Clamp(Mathf.CeilToInt(Time.deltaTime / .012f), 1, 8);
        float dt = Time.deltaTime / steps;
        for (int step = 0; step < steps && phase == BladePhase.Projectile; step++)
        {
            var oldVelocity = flightVelocity;
            var planar = Vector3.ProjectOnPlane(flightVelocity, Vector3.up);
            float speed = Mathf.Lerp(projectileInitialSpeed, projectileSpeed, Mathf.Clamp01((projectileAge + dt) / Mathf.Max(.01f, launchBoostSeconds)));
            if (flightDistance >= projectileRange) speed = Mathf.MoveTowards(planar.magnitude, 0, 65f * dt);
            flightVelocity = (planar.sqrMagnitude > .0001f ? planar.normalized : Forward) * speed;
            flightVelocity.y = oldVelocity.y - projectileGravity * dt;
            projectileAge += dt;
            var previous = center; var next = center + (oldVelocity + flightVelocity) * (.5f * dt);
            var rotation = BladeRotation(flightVelocity);
            var oldTip = sword.TransformPoint(Vector3.up * 1.16f);
            var nextTip = next + rotation * Vector3.Scale(Vector3.up * .68f, sword.lossyScale);
            float sweepLength = Vector3.Distance(oldTip, nextTip);
            bool blocked = SceneSweep(oldTip, nextTip, .035f, out var impact);
            if (!blocked) { sweepLength = Vector3.Distance(previous, next); blocked = SceneSweep(previous, next, .09f, out impact); }
            if (blocked) next = Vector3.Lerp(previous, next, Mathf.Clamp01(impact.distance / Mathf.Max(.001f, sweepLength)));
            int count = Physics.OverlapCapsuleNonAlloc(previous, next, projectileRadius, contacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Hit(contacts[i], true, flightVelocity, previous);
            flightDistance += Vector3.ProjectOnPlane(next - previous, Vector3.up).magnitude;
            SetBlade(next, rotation);
            if (blocked) Embed(impact, flightVelocity);
        }
    }
    Quaternion BladeRotation(Vector3 heading)
    {
        if (heading.sqrMagnitude < .0001f) return sword.rotation;
        var up = heading.normalized;
        var face = Vector3.ProjectOnPlane(-Vector3.up, up);
        if (face.sqrMagnitude < .001f) face = Vector3.ProjectOnPlane(Forward, up);
        return Quaternion.LookRotation(face.normalized, up);
    }
    bool SceneSweep(Vector3 from, Vector3 to, float radius, out RaycastHit impact)
    {
        impact = default; var delta = to - from; float distance = delta.magnitude;
        if (distance < .00001f) return false;
        int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, obstacles, distance, ~0, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (!IsActorOrDummy(obstacles[i].collider) && !obstacles[i].transform.IsChildOf(transform) && obstacles[i].distance < closest)
            { closest = obstacles[i].distance; impact = obstacles[i]; }
        return closest < float.PositiveInfinity;
    }
    void Embed(RaycastHit hit, Vector3 incoming)
    {
        var tangent = Vector3.ProjectOnPlane(incoming, hit.normal).normalized;
        var heading = (tangent * .5f - hit.normal * .866f).normalized;
        var rotation = BladeRotation(heading);
        var tip = hit.point + heading * embedDepth;
        SetBlade(tip - rotation * Vector3.Scale(Vector3.up * .68f, sword.lossyScale), rotation);
        flightVelocity = returnVelocity = Vector3.zero; phase = BladePhase.Embedded;
        SetFlightEffects(false);
    }
    bool IsActorOrDummy(Collider collider)
        => collider.GetComponentInParent<CharacterMotor>() != null || collider.GetComponentInParent<CombatDummy>() != null;
    float ObstacleDistance(Vector3 origin, Vector3 direction, float length)
    {
        float closest = length;
        int count = Physics.SphereCastNonAlloc(origin, .1f, direction, obstacles, length, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!IsActorOrDummy(obstacles[i].collider) && !obstacles[i].transform.IsChildOf(transform))
                closest = Mathf.Min(closest, Mathf.Max(0, obstacles[i].distance - .02f));
        return closest;
    }
    void Hit(Collider contact, bool projectile, Vector3 travelDirection = default, Vector3 hitOrigin = default, bool returning = false)
    {
        if (contact == null || contact.transform.IsChildOf(transform)) return;
        var victim = contact.GetComponentInParent<CharacterMotor>();
        var dummy = contact.GetComponentInParent<CombatDummy>();
        if (victim == driver || (victim == null && dummy == null)) return;
        if (victim != null && System.Array.IndexOf(equipment.selection.characters, victim) < 0 && !victim.CompareTag("Enemy")) return;
        int id = victim != null ? victim.GetInstanceID() : dummy.GetInstanceID();
        if (nextHitByTarget.TryGetValue(id, out float nextHit) && Time.time < nextHit) return;
        var heading = travelDirection.sqrMagnitude > .001f ? travelDirection.normalized : Forward;
        var offset = contact.bounds.center - transform.position;
        if (!returning && Vector3.Dot(offset, Forward) < -.05f) return;
        var approach = returning ? hitOrigin : transform.position + Vector3.up * 1.05f;
        var toTarget = contact.bounds.center - approach;
        if (toTarget.sqrMagnitude > .01f && ObstacleDistance(approach, toTarget.normalized, toTarget.magnitude) < toTarget.magnitude - .15f) return;
        // One shared interval per actor also deduplicates targets with multiple colliders.
        // A dodge consumes this interval, while later contacts can be accepted after it expires.
        nextHitByTarget[id] = Time.time + Mathf.Max(.1f, hitInterval);
        if (victim != null)
        {
            var reaction = victim.GetComponent<CharacterHitReaction>();
            // Use the blade's incoming side on recall, not the caster now standing on the other side.
            var attackerPosition = returning ? contact.bounds.center - heading * 2f : transform.position;
            if (reaction == null || !reaction.ReceiveHit(attackerPosition, projectile,driver,attackType:PlayerProgression.AttackType(driver,1),weaponSlot:1,damageMultiplier:projectile?(returning?3.7f:3.4f):2.7f)) return;
            if (projectile && !CharacterCombatStats.Dead(victim))
            {
                var push = victim.GetComponent<SwordSkillKnockback>();
                if (push == null) push = victim.gameObject.AddComponent<SwordSkillKnockback>();
                push.Push(heading, knockbackDistance, knockbackSeconds, reaction.LastResponse != EnemyPoise.HitResponse.Downed,
                    returning && reaction.LastHitFromBack);
            }
        }
        else dummy.RegisterHit();
        if (returning) ReturnHitCount++; else if (projectile) ProjectileHitCount++; else SpinHitCount++;
        // Sustained hits use the existing layered impact, without restarting global hit-stop every tick.
        CombatHitFeedback.Play(contact.ClosestPoint(center), driver.movementCamera, true, heading, contact, false, false);
    }
    void SetFlightEffects(bool active)
    {
        if (effects == null) return;
        foreach (var particle in effects.GetComponentsInChildren<ParticleSystem>())
        {
            if (active) particle.Play(true);
            else particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        foreach (var trail in effects.GetComponentsInChildren<TrailRenderer>())
        { trail.emitting = active; if (active) trail.Clear(); }
    }
    void BeginReturn()
    {
        GameplayAudio.Play(GameplayCue.SwordSwing,transform.position+Vector3.up,.5f);
        if (socket == null) { Cancel(false); return; }
        // Extract an embedded blade along its own axis before rope tension accelerates it home.
        returnVelocity = phase == BladePhase.Embedded ? -sword.up * 1.2f : flightVelocity * .25f;
        phase = BladePhase.Returning; returnAt = Time.time; returnRotation = sword.rotation;
        returnDistance = Vector3.Distance(center, socket.TransformPoint(Vector3.Scale(BladeCenter, savedScale)));
        strokeAtReturn = pullStroke.Evaluate(pullProgress);
        SetFlightEffects(true);
    }
    void Return()
    {
        if (socket == null) { Cancel(false); return; }
        int steps = Mathf.Clamp(Mathf.CeilToInt(Time.deltaTime / .0084f), 1, 8);
        float dt = Time.deltaTime / steps;
        float stroke = Mathf.InverseLerp(strokeAtReturn, 1, pullStroke.Evaluate(Mathf.Clamp01(currentPullProgress)));
        // Hand travel shortens the rope's rest length. Tension accelerates the blade;
        // inertia carries it after the stroke, rather than assigning a lerped position.
        float restLength = Mathf.Lerp(returnDistance, .1f, stroke);
        for (int step = 0; step < steps && phase == BladePhase.Returning; step++)
        {
            var target = socket.TransformPoint(Vector3.Scale(BladeCenter, savedScale));
            var toward = target - center; float distance = toward.magnitude;
            if (distance < .18f) { Catch(); return; }
            var heading = toward / distance;
            float along = Vector3.Dot(returnVelocity, heading);
            float tension = Mathf.Clamp((distance - restLength) * returnSpring - along * returnDamping, 0, returnAcceleration);
            returnVelocity += (heading * tension + Vector3.down * (projectileGravity * .15f)) * dt;
            // Rope/air damping suppresses lateral orbiting while retaining forward momentum.
            returnVelocity = heading * Vector3.Dot(returnVelocity, heading)
                + Vector3.ProjectOnPlane(returnVelocity, heading) * Mathf.Exp(-8f * dt);
            returnVelocity *= Mathf.Exp(-.4f * dt);
            returnVelocity = Vector3.ClampMagnitude(returnVelocity, Mathf.Min(returnSpeed, Mathf.Sqrt(180f * distance)));
            var previous = center; var next = center + returnVelocity * dt;
            bool catchNow = Vector3.Dot(next - target, heading) >= 0;
            if (catchNow) next = target;
            bool blocked = SceneSweep(previous, next, .065f, out var impact);
            if (blocked) next = previous + (next - previous).normalized * Mathf.Max(0, impact.distance - .01f);
            var travel = next - previous;
            var hitDirection = travel.sqrMagnitude > .000001f ? travel.normalized : heading;
            int count = Physics.OverlapCapsuleNonAlloc(previous, next, projectileRadius, contacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Hit(contacts[i], true, hitDirection, previous, true);
            var rotation = Quaternion.Slerp(returnRotation, BladeRotation(-heading), Mathf.Clamp01((Time.time - returnAt) / .16f));
            rotation = Quaternion.Slerp(rotation, socket.rotation, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, .18f, distance)));
            SetBlade(next, rotation);
            if (blocked) { Embed(impact, returnVelocity); StopPull(true); return; }
            if (catchNow) { Catch(); return; }
        }
    }
    void Catch()
    {
        sword.SetParent(socket, false); sword.localPosition = Vector3.zero; sword.localRotation = Quaternion.identity; sword.localScale = savedScale;
        phase = BladePhase.Caught; if (effects != null) Destroy(effects);
        if (rope != null) Destroy(rope.gameObject);
    }
    void StopPull(bool recover)
    {
        if (phase == BladePhase.Returning)
        {
            phase = BladePhase.Projectile; flightVelocity = returnVelocity;
            flightDistance = projectileRange; projectileAge = launchBoostSeconds;
        }
        ReleaseBody(recover); recalling = recallQueued = false; animationDone = true;
    }
    public void InterruptForHit()
    {
        // A hit on the freely moving caster must not silently recall the waiting sword.
        if (!OwnsBody) return;
        if (phase == BladePhase.Projectile || phase == BladePhase.Embedded || phase == BladePhase.Returning) StopPull(false);
        else Cancel(false);
    }
    public void Cancel(bool recover)
    {
        pendingDraw = recallQueued = false;
        if (!IsCasting && (equipment == null || equipment.ActiveSkill != this)) return;
        readyAt=Time.time+cooldownSeconds;
        ReleaseBody(recover);
        IsCasting = false;
        if (effects != null) Destroy(effects);
        if (spinEffects != null) Destroy(spinEffects);
        if (rope != null) Destroy(rope.gameObject);
        if (equipment != null) equipment.EndSwordSkill(this);
        sword = socket = null; nextHitByTarget.Clear();
    }
    void ReleaseBody(bool recover)
    {
        if (bodyReleased) return;
        bodyReleased = true;
        if (driver != null)
        {
            if (equipment != null && equipment.ActiveSkill == this) driver.equipmentBusy = false;
            driver.SetAttackActive(false);
            if (driver.animator != null)
            {
                driver.animator.SetBool("IsAttacking", false);
                if (recover && !driver.IsActionBlocked && driver.animator.isActiveAndEnabled)
                    driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, driver.IsGrounded ? "Locomotion" : "Fall", .22f, 0, 0);
            }
        }
    }
    void OnDisable() { Cancel(true); }
    void OnDrawGizmosSelected()
    {
        if (!IsCasting || phase == BladePhase.Held) return;
        Gizmos.color = new Color(.65f, .2f, 1, .5f); Gizmos.DrawWireSphere(center, phase == BladePhase.Spin ? spinRadius : projectileRadius);
    }
}
