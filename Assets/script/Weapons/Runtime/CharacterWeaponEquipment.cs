using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Party-shared ownership of the acquired sword and floating orb.</summary>
[DefaultExecutionOrder(-40), DisallowMultipleComponent]
public sealed class CharacterWeaponEquipment : MonoBehaviour
{
    public PlayerRoster selection;
    public CombatLockOn lockOn;
    public GameObject swordPrefab;
    public GameObject orbPrefab;
    public bool HasOrb { get; private set; }
    public Transform Orb => orb;
    Transform orb;
    public AnimationClip drawClip, stowClip;
    public Transform[] handSockets, backSockets;
    public float animationSpeed = 1.35f;
    [Min(1)] public float attackDrawSpeedMultiplier = 3f;
    [Range(0,1)] public float drawAttachProgress = .48f;
    [Range(0,1)] public float stowAttachProgress = .60f;
    [Min(0)] public float enemyDrawRange = 6f;
    [Min(0)] public float enemyStowRange = 8f;
    public bool HasNearbyEnemy { get; private set; }
    float nextEnemyScan;
    static readonly int SwordBlend = Animator.StringToHash("SwordBlend");
    public bool HasSword { get; private set; }
    public int SelectedSlot { get; private set; }
    public bool IsDrawn { get; private set; }
    public bool IsTransitioning { get; private set; }
    public Transform Sword => sword;
    public SwordThrowSkill ActiveSkill { get; private set; }
    public int PickupCount { get; private set; }
    public int DrawCount { get; private set; }
    public int StowCount { get; private set; }
    CharacterMotor actor;
    Transform sword;
    int layer = -1, actorIndex = -1;
    bool transitionDraw, attached;
    bool ballHidden;
    float transitionStart, transitionDuration;
    bool attackDraw;
    float attackHoldUntil;
    static readonly int DrawSpeed = Animator.StringToHash("WeaponDrawSpeed");
    static readonly int Empty = Animator.StringToHash("WeaponAction.Empty");
    static readonly int Held = Animator.StringToHash("WeaponAction.Held");
    static readonly int Draw = Animator.StringToHash("WeaponAction.Draw");
    static readonly int Stow = Animator.StringToHash("WeaponAction.Stow");

    void Awake()
    {
        if (selection == null) selection = GetComponent<PlayerRoster>();
        if (lockOn == null) lockOn = GetComponent<CombatLockOn>();
    }
    public bool AcquireSword(CharacterMotor collector)
    {
        if (HasSword || selection == null || collector != selection.ActiveCharacter || swordPrefab == null) return false;
        HasSword = true;
        SelectedSlot = 1;
        PickupCount++;
        sword = Instantiate(swordPrefab).transform;
        sword.name = "Equipped Crimson Greatsword";
        BindActor();
        Attach(false);
        return true;
    }
    public bool SelectSlot(int slot)
    {
        if (actor!=null&&actor.BallInputBlocked) return false;
        if (ActiveSkill != null) return false;
        if (slot < 0 || slot > 2 || (slot == 1 && !HasSword) || (slot == 2 && !HasOrb)) return false;
        if (actor != null && (actor.IsAttacking || actor.IsDodging || actor.JumpPreparing || !actor.IsGrounded || actor.IsActionBlocked || IsTransitioning)) return false;
        SelectedSlot = slot;
        return true;
    }
    public bool AcquireOrb(CharacterMotor collector)
    {
        if(HasOrb||selection==null||collector!=selection.ActiveCharacter||orbPrefab==null)return false;
        orb=Instantiate(orbPrefab).transform;orb.name="Equipped Sapphire Orb";
        var follow=orb.GetComponent<FloatingOrbFollower>()??orb.gameObject.AddComponent<FloatingOrbFollower>();
        follow.equipment=this;HasOrb=true;return true;
    }
    public bool RequestAttackDraw(CharacterMotor requester)
    {
        if (ActiveSkill != null) return false;
        if(!HasSword||SelectedSlot!=1||selection==null||selection.ActiveCharacter!=requester||sword==null)return false;
        if(actor!=requester)BindActor();
        attackHoldUntil=Time.time+1f;
        if(IsDrawn&&!IsTransitioning)return true;
        if(IsTransitioning&&attackDraw)return true;
        BeginTransition(true,true);
        return true;
    }
    public bool BeginSwordSkill(SwordThrowSkill skill, CharacterMotor requester)
    {
        if (ActiveSkill != null || actor != requester || !HasSword || SelectedSlot != 1 || !IsDrawn || IsTransitioning || sword == null) return false;
        ActiveSkill = skill;
        actor.equipmentBusy = true;
        if (layer >= 0) actor.animator.SetLayerWeight(layer, 0);
        return true;
    }
    public void EndSwordSkill(SwordThrowSkill skill)
    {
        if (ActiveSkill != skill) return;
        ActiveSkill = null;
        if (actor != null) actor.equipmentBusy = false;
        Attach(IsDrawn);
        attackHoldUntil = Time.time + 1f;
    }
    void Update()
    {
        if (selection == null) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) SelectSlot(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) SelectSlot(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) SelectSlot(2);
        }
        if (selection.ActiveCharacter != actor) BindActor();
        bool aiming=actor!=null&&actor.IsBallThrowing;
        if(ballHidden!=aiming){ballHidden=aiming;if(sword!=null)sword.gameObject.SetActive(!aiming);}
        if(aiming)
        {
            actor.animator.SetFloat("OrbBlend",0);actor.animator.SetFloat(SwordBlend,0);
            if(layer>=0)actor.animator.SetLayerWeight(layer,0);
            return;
        }
        if (ActiveSkill != null && (!ActiveSkill.isActiveAndEnabled || !ActiveSkill.IsCasting))
        {
            var stale = ActiveSkill;
            stale.Cancel(true);
            EndSwordSkill(stale);
        }
        if (actor != null && actor.animator != null) { actor.animator.SetFloat("OrbBlend", SelectedSlot == 2 ? 1 : 0); if (SelectedSlot == 2) actor.ResetIdleTimer(); }
        if (!HasSword || actor == null || sword == null) return;
        UpdateEnemyProximity();
        UpdateLocomotion();
        if (ActiveSkill != null)
        {
            actor.equipmentBusy = ActiveSkill.OwnsBody;
            if (layer >= 0) actor.animator.SetLayerWeight(layer, 0);
            return;
        }
        if (IsTransitioning)
        {
            float progress = (Time.time - transitionStart) / transitionDuration;
            if (!attached && progress >= (transitionDraw ? drawAttachProgress : stowAttachProgress))
            {
                Attach(transitionDraw);
                attached = true;
            }
            if (progress >= 1 || (attackDraw && attached))
            {
                Attach(transitionDraw);
                IsDrawn = transitionDraw;
                IsTransitioning = false;
                actor.equipmentBusy = false;
                attackDraw=false;actor.animator.SetFloat(DrawSpeed,1);
                if (layer >= 0) actor.Animation.CrossFade(CharacterAnimationDirector.Action.Overlay, IsDrawn ? Held : Empty, .12f, layer, 0);
            }
            else
            {
                if (layer >= 0) actor.animator.SetLayerWeight(layer, Mathf.MoveTowards(actor.animator.GetLayerWeight(layer), 1, Time.deltaTime * 9));
                return;
            }
        }
        var combo=actor.GetComponent<CharacterComboAttack>();
        var skill=actor.GetComponent<SwordThrowSkill>();
        bool wantDrawn = SelectedSlot == 1 && ((skill!=null&&skill.IsPreparingCast) || HasNearbyEnemy || Time.time<attackHoldUntil || actor.IsAttacking || (combo!=null&&combo.IsFightReady) || (lockOn != null && lockOn.Target != null));
        // Finish the current combat action before moving a weapon between sockets.
        if (wantDrawn != IsDrawn && !actor.IsAttacking && !actor.IsDodging && actor.IsGrounded && !actor.JumpPreparing)
            BeginTransition(wantDrawn);
        if (layer >= 0)
        {
            var state = actor.animator.GetCurrentAnimatorStateInfo(0);
            bool needsGrip = actor.PlanarSpeed > actor.walkSpeed + .1f || actor.animator.GetFloat("LockMoveX") > .15f
                || (!state.IsName("Locomotion") && !state.IsName("FightIdle"));
            float target = IsTransitioning || (IsDrawn && needsGrip && !actor.IsAttacking && !actor.IsDodging) ? 1 : 0;
            actor.animator.SetLayerWeight(layer, Mathf.MoveTowards(actor.animator.GetLayerWeight(layer), target, Time.deltaTime * 9));
        }
    }
    void UpdateEnemyProximity()
    {
        if (SelectedSlot != 1) { HasNearbyEnemy = false; return; }
        if (Time.time < nextEnemyScan) return;
        nextEnemyScan = Time.time + .2f;
        float radius = HasNearbyEnemy ? Mathf.Max(enemyDrawRange, enemyStowRange) : enemyDrawRange;
        bool nearby = false;
        foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if ((enemy.transform.position - actor.transform.position).sqrMagnitude > radius * radius) continue;
            nearby = true; break;
        }
        HasNearbyEnemy = nearby;
    }
    void UpdateLocomotion()
    {
        actor.animator.SetFloat(SwordBlend, IsDrawn && !IsTransitioning && ActiveSkill == null ? 1 : 0, .16f, Time.deltaTime);
        if (!IsDrawn || IsTransitioning) return;
        actor.ResetIdleTimer();
        var state = actor.animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("SadIdle") && actor.IsGrounded && !actor.IsAttacking && !actor.IsDodging)
            actor.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion", .16f, 0, 0);
    }
    void BindActor()
    {
        if (ActiveSkill != null) ActiveSkill.Cancel(true);
        if (actor != null)
        {
            actor.equipmentBusy = false;
            if (layer >= 0 && actor.animator != null) { actor.animator.SetLayerWeight(layer, 0); actor.animator.SetFloat(SwordBlend, 0); actor.animator.SetFloat("OrbBlend", 0); }
        }
        actor = selection.ActiveCharacter;
        actorIndex = selection.ActiveIndex;
        layer = actor != null && actor.animator != null ? actor.animator.GetLayerIndex("WeaponAction") : -1;
        IsTransitioning = IsDrawn = false;
        attackDraw=false;attackHoldUntil=0;
        HasNearbyEnemy = false; nextEnemyScan = 0;
        if (actor == null) { if (sword != null) sword.gameObject.SetActive(false); return; }
        if (sword != null) { sword.gameObject.SetActive(true); Attach(false); }
        if (layer >= 0)
        {
            actor.animator.SetLayerWeight(layer, 0);
            actor.animator.SetFloat(DrawSpeed,1);
            actor.Animation.Play(CharacterAnimationDirector.Action.Overlay, Empty, layer, 0);
        }
    }
    void BeginTransition(bool draw,bool forAttack=false)
    {
        if (layer < 0) { Attach(draw); IsDrawn = draw;IsTransitioning=false;actor.equipmentBusy=false;return; }
        attackDraw=draw&&forAttack;
        transitionDraw = draw; attached = false; IsTransitioning = true;
        actor.equipmentBusy = true;
        actor.ResetIdleTimer();
        transitionStart = Time.time;
        var clip = draw ? drawClip : stowClip;
        float multiplier=attackDraw?attackDrawSpeedMultiplier:1;
        transitionDuration = Mathf.Max(.1f, (clip != null ? clip.length : 1) / (animationSpeed*multiplier));
        actor.animator.SetFloat(DrawSpeed,multiplier);
        if (draw) DrawCount++; else StowCount++;
        actor.Animation.CrossFade(CharacterAnimationDirector.Action.Overlay, draw ? Draw : Stow, attackDraw?.035f:.1f, layer, 0);
        if(attackDraw)actor.animator.SetLayerWeight(layer,1);
    }
    void Attach(bool hand)
    {
        var sockets = hand ? handSockets : backSockets;
        if (sword == null || sockets == null || actorIndex < 0 || actorIndex >= sockets.Length || sockets[actorIndex] == null) return;
        sword.SetParent(sockets[actorIndex], false);
        sword.localPosition = Vector3.zero;
        sword.localRotation = Quaternion.identity;
        sword.localScale = Vector3.one;
    }
    void LateUpdate()
    {
        if (sword == null || actor == null || sword.parent == null) return;
        // Back socket follows the chest; only a tiny local hover is added.
        if (actorIndex >= 0 && backSockets != null && actorIndex < backSockets.Length && sword.parent == backSockets[actorIndex])
            sword.localPosition = new Vector3(0, Mathf.Sin(Time.time * 1.7f) * .014f, 0);
    }
    void OnDisable()
    {
        if (ActiveSkill != null) ActiveSkill.Cancel(true);
        if (actor != null)
        {
            actor.equipmentBusy = false;
            if (layer >= 0 && actor.animator != null) { actor.animator.SetLayerWeight(layer, 0); actor.animator.SetFloat(SwordBlend, 0); actor.animator.SetFloat("OrbBlend", 0); }
        }
        IsTransitioning = false;
        attackDraw=false;attackHoldUntil=0;
        IsDrawn = false;
        if (sword != null) Attach(false);
    }
    void OnDestroy() { if (sword != null) Destroy(sword.gameObject);if(orb!=null)Destroy(orb.gameObject); }
}






