using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(-20)]
public sealed class CharacterComboAttack : MonoBehaviour
{
    [Serializable] public sealed class Strike
    {
        public string stateName;
        public AnimationClip clip;
        [Range(.1f,1)] public float recoveryCancel = .68f;
        [Min(.1f)] public float playbackSpeed = 1f;
        [Min(0)] public float startFrame;
        [Min(0)] public float forwardDistance;
        [Min(0)] public float contactFrame;
        public HumanBodyBones contactBone = HumanBodyBones.RightHand;
        public float blendSecondsOverride = -1f;
        public float[] additionalContactFrames;
        public int ContactCount => 1 + (additionalContactFrames != null ? additionalContactFrames.Length : 0);
        public float ContactFrame(int index) => index == 0 ? contactFrame : additionalContactFrames[index - 1];
        public float LastContactFrame => ContactFrame(ContactCount - 1);
        public float ContactProgress => clip!=null?contactFrame/(clip.length*clip.frameRate):1;
        public float StartTime => clip != null ? startFrame / clip.frameRate : 0;
        public float StartProgress => clip != null ? StartTime / clip.length : 0;
        public float Duration => clip != null ? (clip.length-StartTime) / Mathf.Max(.1f,playbackSpeed) : 0;
    }
    public CharacterMotor driver;
    public Strike[] attacks;
    public Strike runningAttack;
    public Strike[] swordAttacks;
    public Strike swordRunningAttack;
    public CharacterWeaponEquipment equipment;
    public float swordRunningTravelDistance = 5.2f;
    public AnimationCurve swordRunningTravelCurve;
    public Strike[] orbAttacks;
    public Strike orbRunningAttack;
    bool orbSequence;
    public bool IsOrbAttack => IsAttacking && orbSequence;
    bool swordSequence;
    Strike[] ActiveAttacks => orbSequence ? orbAttacks : swordSequence ? swordAttacks : attacks;
    Strike RunningStrike => orbSequence ? orbRunningAttack : swordSequence ? swordRunningAttack : runningAttack;
    public bool IsSwordAttack => IsAttacking && swordSequence;
    [Min(0)] public float runningTravelDistance = 1.2f;
    [Min(0)] public float fightIdleDuration = 3f;
    [Min(.1f)] public float comboTimeout = 2f;
    [Tooltip("Real-time crossfade duration between attack poses; independent of clip playback speed.")]
    [Range(0,.25f)] public float blendSeconds = .16f;
    [Min(0)] public float fullComboCooldown = .3f;
    [Tooltip("Extra game seconds after the jump/dodge recovery marker before held movement can cancel the strike.")]
    [Min(0)] public float movementCancelDelay=.1f;
    public float CooldownRemaining => Mathf.Max(0, cooldownUntil-Time.time);
    float cooldownUntil;
    const float RecoveryInputBuffer=.18f;
    int recoveryAction; // 1 jump, 2 dodge; only consumed after the strike's recovery marker.
    float recoveryActionUntil;
    public int MovementCancelCount { get; private set; }
    public float LastMovementCancelProgress { get; private set; }
    public string LastMovementCancelAction { get; private set; }
    public float MovementCancelProgress
    {
        get { var strike=ActiveStrike;if(strike==null||strike.clip==null)return 1;
            float afterContact=(strike.LastContactFrame+1)/(strike.clip.length*strike.clip.frameRate);
            return Mathf.Clamp01(Mathf.Max(afterContact,strike.recoveryCancel<1?strike.recoveryCancel:afterContact)); }
    }

    public float LocomotionCancelProgress
    {
        get {var strike=ActiveStrike;if(strike==null||strike.clip==null)return 1;
            return Mathf.Clamp01(MovementCancelProgress+(IsRunningAttack&&!orbSequence?0:orbSequence?.025f:movementCancelDelay)*Mathf.Max(.1f,strike.playbackSpeed)/Mathf.Max(.01f,strike.clip.length));}
    }

    public bool IsAttacking { get; private set; }
    public bool IsRunningAttack { get; private set; }
    bool aiCombatReady;
    public bool IsFightReady => !IsAttacking && (aiCombatReady || Time.time < fightReadyUntil);
    public float FightIdleRemaining => Mathf.Max(0,fightReadyUntil-Time.time);
    public int CurrentStage { get; private set; }
    public static float StageDamageMultiplier(int stage) => stage<=1?1:stage==2?1.25f:stage==3?1.55f:stage==4?1.9f:2.3f;
    public float DamageMultiplier => IsRunningAttack?3f:StageDamageMultiplier(CurrentStage);
    public bool IsFifthStrike => IsAttacking && !IsRunningAttack && CurrentStage == 5;
    public int AttackCount { get; private set; }
    public int RecoveryCancelCount { get; private set; }
    public bool HasBufferedAttack => queued >= 0;
    public Strike ActiveStrike => IsRunningAttack ? RunningStrike : stage>=0 && ActiveAttacks!=null && stage<ActiveAttacks.Length ? ActiveAttacks[stage] : null;
    public float CurrentProgress => IsAttacking && ActiveStrike!=null ? Mathf.Clamp01((Time.time-started)/Mathf.Max(.01f,ActiveStrike.Duration)) : 0;
    public float SourceProgress { get; private set; }
    public float RunningDistanceMoved { get; private set; }
    public float AttackDistanceMoved { get; private set; }
    public float PreviousAttackDistanceMoved { get; private set; }
    // Stage zero denotes the running opener; ordinary stages use the active weapon profile.
    public event Action<int,float,bool> AttackStarted;
    int stage=-1,queued=-1;
    float started,lastClick=float.NegativeInfinity,fightReadyUntil=float.NegativeInfinity;
    float plannedTravel;
    Vector3 attackForward;
    CharacterMotor pursuitTarget;
    float pursuitDistance;
    bool UsesPursuitBlink => pursuitTarget != null && CurrentStage != 3 && CurrentStage != 5;
    float runningStartSpeed,entryElapsed,entryTravelApplied;
    public Vector3 AttackForward => attackForward;
    bool controlled,wasFightReady;
    bool attackInputArmed=true;
    bool pendingSwordAttack;
    EnemyTellDirector enemyTell;
    CharacterParryAction parry;
    public bool IsWaitingForSword => pendingSwordAttack;
    static readonly int Attacking=Animator.StringToHash("IsAttacking");
    static readonly int FightReady=Animator.StringToHash("FightReady");

    void OnEnable()
    {
        if(driver==null)driver=GetComponent<CharacterMotor>();
        if(equipment==null)equipment=FindAnyObjectByType<CharacterWeaponEquipment>();
        enemyTell=FindAnyObjectByType<EnemyTellDirector>();parry=GetComponent<CharacterParryAction>();
        swordSequence=orbSequence=false;
        stage=queued=-1;recoveryAction=0;MovementCancelCount=0;CurrentStage=0;AttackCount=RecoveryCancelCount=0;
        lastClick=fightReadyUntil=float.NegativeInfinity;cooldownUntil=0;attackInputArmed=true;
        IsAttacking=IsRunningAttack=wasFightReady=false;controlled=driver.acceptPlayerInput;
    }
    void Update()
    {
        if(driver==null||driver.animator==null)return;
        if(driver.BallInputBlocked){CancelForControlChange();UpdateFightIdle();return;}
        if(driver.IsActionBlocked){CancelForControlChange();if(!driver.IsHitStunned)UpdateFightIdle();return;}
        if(controlled&&!driver.acceptPlayerInput){if(IsSwordAttack || IsOrbAttack)Finish();ClearSequence();}
        controlled=driver.acceptPlayerInput;
        if(parry!=null&&parry.OwnsBody)return;
        if(controlled&&Pressed()&&enemyTell!=null&&enemyTell.TryParry(driver))
        {attackInputArmed=false;return;}
        if(pendingSwordAttack)
        {
            if(!controlled||equipment==null||equipment.SelectedSlot!=1||equipment.selection.ActiveCharacter!=driver||driver.IsDodging||!driver.IsGrounded||driver.JumpPreparing)
                pendingSwordAttack=false;
            else if(equipment.IsDrawn&&!equipment.IsTransitioning&&!driver.equipmentBusy)
            {pendingSwordAttack=false;RequestAttack();}
        }
        // A press during the previous pose's crossfade cannot reserve another unseen strike.
        bool visibleStrike=!IsAttacking||(!driver.animator.IsInTransition(0)&&driver.animator.GetCurrentAnimatorStateInfo(0).IsName(ActiveStrike.stateName));
        if(visibleStrike&&!AttackHeld())attackInputArmed=true;
        if(controlled&&attackInputArmed&&Pressed())RequestAttack();
        if(controlled&&IsAttacking){
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)RequestRecoveryAction(true);
            else if(Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame)RequestRecoveryAction(false);
#else
            if(Input.GetKeyDown(KeyCode.Space))RequestRecoveryAction(true);
            else if(Input.GetMouseButtonDown(1))RequestRecoveryAction(false);
#endif
        }
        if(Time.deltaTime<=0)return;
        UpdateFightIdle();
        if(!IsAttacking)return;
        AdvanceRunningEntry(Time.deltaTime);
        var strike=ActiveStrike;
        var animator=driver.animator;
        var state=animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if(!state.IsName(strike.stateName))
        {
            if(orbSequence && Time.time-started>strike.Duration+.3f){Finish();ClearSequence();}
            return;
        }
        SourceProgress=state.normalizedTime;
        if(recoveryAction!=0&&Time.time>recoveryActionUntil)recoveryAction=0;
        if(recoveryAction!=0&&SourceProgress>=MovementCancelProgress&&CancelForMovement())return;
        if(controlled&&queued<0&&recoveryAction==0&&SourceProgress>=LocomotionCancelProgress&&CancelForLocomotion())return;
        float travelDistance=IsRunningAttack?(orbSequence?0:swordSequence?swordRunningTravelDistance:runningTravelDistance):UsesPursuitBlink?0:Mathf.Max(strike.forwardDistance,pursuitDistance);
        if(travelDistance>0)
        {
            // Complete travel before recovery can be cancelled; the capsule still stops at obstacles.
            float phase=IsRunningAttack?(swordSequence?Mathf.InverseLerp(.06f,.52f,SourceProgress):Mathf.InverseLerp(3f/45f,27f/45f,SourceProgress)):Mathf.InverseLerp(.12f,Mathf.Min(.50f,strike.recoveryCancel),SourceProgress);
            if(swordSequence&&!IsRunningAttack&&CurrentStage==3)
            {
                float frames=strike.clip.length*strike.clip.frameRate;
                float from=Mathf.Max(strike.startFrame,strike.LastContactFrame-6)/frames;
                float to=Mathf.Min(strike.LastContactFrame+4,frames*strike.recoveryCancel)/frames;
                phase=Mathf.InverseLerp(from,Mathf.Max(from+.001f,to),SourceProgress);
            }
            float fraction=IsRunningAttack&&swordSequence&&swordRunningTravelCurve!=null&&swordRunningTravelCurve.length>1 ? Mathf.Clamp01(swordRunningTravelCurve.Evaluate(SourceProgress)) : Mathf.SmoothStep(0,1,phase);
            float target=travelDistance*fraction;
            float distance=Mathf.Max(0,target-plannedTravel);plannedTravel=target;
            if(pursuitTarget!=null)
            {
                if(!pursuitTarget.isActiveAndEnabled||CharacterCombatStats.Dead(pursuitTarget))distance=0;
                else distance=Mathf.Min(distance,Mathf.Max(0,Vector3.Dot(Vector3.ProjectOnPlane(pursuitTarget.transform.position-transform.position,Vector3.up),attackForward)-1.05f));
            }
            var moved=driver.ApplyAttackDisplacement(attackForward*distance);
            float actual=Mathf.Max(0,Vector3.Dot(moved,attackForward));
            AttackDistanceMoved+=actual;
            if(IsRunningAttack)RunningDistanceMoved+=actual;
        }
        bool finished=SourceProgress>=1 && !animator.IsInTransition(0);
        // Read the sampled animation frame so a pose is never cancelled before its strike peak.
        bool canContinue=(strike.recoveryCancel<1 ? SourceProgress>=strike.recoveryCancel : finished)
            && (!orbSequence || !IsRunningAttack || driver.IsGrounded);
        if(finished&&!IsRunningAttack&&stage==ActiveAttacks.Length-1&&queued<0){cooldownUntil=Time.time+fullComboCooldown;Finish();stage=-1;lastClick=float.NegativeInfinity;return;}
        if(queued>=0&&canContinue){int next=queued;queued=-1;StartStrike(next,false,!finished);}
        else if(finished)Finish();
    }
    void UpdateFightIdle()
    {
        bool ready=IsFightReady;
        if(ready||wasFightReady)driver.ResetIdleTimer();
        var animator=driver.animator;
        animator.SetBool(FightReady,ready);
        if(wasFightReady&&!ready&&!IsAttacking)
        {
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName("FightIdle"))driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.10f,0,0);
        }
        wasFightReady=ready;
    }
    public bool RequestAttack()
    {
        if(parry!=null&&parry.OwnsBody)return false;
        if(driver==null||!driver.acceptPlayerInput||!driver.enabled||driver.IsActionBlocked||driver.BallInputBlocked||driver.animator==null)return false;
        if(IsOrbAttack&&IsRunningAttack)return false;
        if(!IsAttacking)
        {
            bool held=equipment!=null && equipment.HasSword && equipment.SelectedSlot==1 && equipment.selection.ActiveCharacter==driver && equipment.ActiveSkill==null;
            bool orb=equipment!=null && equipment.SelectedSlot==2 && equipment.selection.ActiveCharacter==driver;
            if(held!=swordSequence || orb!=orbSequence){ClearSequence();swordSequence=held;orbSequence=orb;lastClick=float.NegativeInfinity;}
        }
        if(ActiveAttacks==null||ActiveAttacks.Length==0)return false;
        if(Time.time<cooldownUntil || (IsAttacking&&!IsRunningAttack&&stage==ActiveAttacks.Length-1))return false;
        if(!IsAttacking&&(!driver.IsGrounded||driver.JumpPreparing||driver.IsDodging))return false;
        if(!IsAttacking&&swordSequence&&(!equipment.IsDrawn||equipment.IsTransitioning))
        {
            if(pendingSwordAttack)return true;
            if(!equipment.RequestAttackDraw(driver))return false;
            pendingSwordAttack=true;attackInputArmed=false;return true;
        }
        if(driver.equipmentBusy)return false;
        bool continuation=IsAttacking || ((stage>=0||IsRunningAttack)&&Time.time-lastClick<=comboTimeout);
        int next=continuation&&!IsRunningAttack?(stage+1)%ActiveAttacks.Length:0;
        if(IsAttacking)
        {
            if(queued>=0||driver.animator.IsInTransition(0)||!driver.animator.GetCurrentAnimatorStateInfo(0).IsName(ActiveStrike.stateName))return false;
            queued=next;lastClick=Time.time;return true;
        }
        lastClick=Time.time;
        bool running=driver.IsRunning&&RunningStrike!=null&&RunningStrike.clip!=null;
        return StartStrike(running?0:next,running,false);
    }
    public bool RequestRecoveryAction(bool jump)
    {
        if(!IsAttacking||driver==null||!driver.acceptPlayerInput||!driver.IsGrounded)return false;
        if(!jump&&!driver.useDodge)return false;
        recoveryAction=jump?1:2;recoveryActionUntil=Time.time+RecoveryInputBuffer;return true;
    }
    public void SetAICombatReady(bool ready){aiCombatReady=ready;}
    public void CancelForControlChange()
    {
        if(IsAttacking)Finish(false);
        ClearSequence();aiCombatReady=false;fightReadyUntil=float.NegativeInfinity;
    }
    public int AIWeaponSlot => GetComponent<EnemyWeaponLoadout>()?.Slot ?? 0;
    public int AIAttackCount => (AIWeaponSlot==1?swordAttacks:AIWeaponSlot==2?orbAttacks:attacks)?.Length ?? 0;
    public Transform AttackSword => !driver.acceptPlayerInput?GetComponent<EnemyWeaponLoadout>()?.Weapon:equipment!=null?equipment.Sword:null;
    public bool RequestAIAttack(int index,bool running=false)
    {
        if(driver==null||driver.acceptPlayerInput||!driver.AIControlled||!driver.enabled||driver.animator==null||driver.equipmentBusy||driver.IsDodging||driver.IsActionBlocked||!driver.IsGrounded||driver.JumpPreparing)return false;
        var profile=AIWeaponSlot==1?swordAttacks:AIWeaponSlot==2?orbAttacks:attacks;
        var run=AIWeaponSlot==1?swordRunningAttack:runningAttack;
        if(profile==null||index<0||index>=profile.Length||profile[index].clip==null||Time.time<cooldownUntil)return false;
        if(running&&(AIWeaponSlot==2||IsAttacking||!driver.IsRunning||run==null||run.clip==null))return false;
        if(!IsAttacking&&GetComponent<PartyEnemyAI>() is PartyEnemyAI ai&&!ai.HasAttackSlot())return false;
        if(IsAttacking)
        {
            if(queued>=0||driver.animator.IsInTransition(0)||!driver.animator.GetCurrentAnimatorStateInfo(0).IsName(ActiveStrike.stateName))return false;
            queued=index;return true;
        }
        ClearSequence();swordSequence=AIWeaponSlot==1;orbSequence=AIWeaponSlot==2;lastClick=Time.time;
        return StartStrike(index,running,false);
    }
    bool CancelForMovement()
    {
        bool jump=recoveryAction==1;
        if(!driver.TryQueueRecoveryAction(jump))return false;
        bool final=!IsRunningAttack&&stage==ActiveAttacks.Length-1;
        LastMovementCancelProgress=SourceProgress;LastMovementCancelAction=jump?"Jump":"Dodge";MovementCancelCount++;
        recoveryAction=0;Finish(false);fightReadyUntil=Time.time+fightIdleDuration;
        driver.animator.SetBool(FightReady,true);
        if(final){cooldownUntil=Time.time+fullComboCooldown;stage=-1;lastClick=float.NegativeInfinity;}
        return true;
    }
    bool CancelForLocomotion()
    {
        if(!driver.enabled||!driver.IsGrounded||driver.IsActionBlocked||driver.equipmentBusy||driver.animator.IsInTransition(0)||!driver.HasPlayerMovementInput)return false;
        bool final=!IsRunningAttack&&stage==ActiveAttacks.Length-1;
        LastMovementCancelProgress=SourceProgress;LastMovementCancelAction="Move";MovementCancelCount++;
        Finish(false,true);fightReadyUntil=Time.time+fightIdleDuration;
        // The movement controller consumes the held direction later in this same frame.
        driver.animator.SetBool(FightReady,false);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.14f,0,0);
        if(final){cooldownUntil=Time.time+fullComboCooldown;stage=-1;lastClick=float.NegativeInfinity;}
        return true;
    }
    bool StartStrike(int index,bool running,bool cancelledRecovery)
    {
        var strike=running?RunningStrike:ActiveAttacks[index];var animator=driver.animator;
        if(strike==null||strike.clip==null||strike.Duration<=0||!animator.HasState(0,Animator.StringToHash(strike.stateName)))return false;
        if(swordSequence){int layer=animator.GetLayerIndex("WeaponAction");if(layer>=0)animator.SetLayerWeight(layer,0); }
        recoveryAction=0;stage=running?-1:index;IsRunningAttack=running;CurrentStage=running?0:index+1;
        started=Time.time;IsAttacking=true;queued=-1;attackInputArmed=false;SourceProgress=strike.StartProgress;
        AttackCount++;if(cancelledRecovery)RecoveryCancelCount++;
        attackForward=Vector3.ProjectOnPlane((driver.visualRoot!=null?driver.visualRoot:transform).forward,Vector3.up).normalized;
        if(attackForward.sqrMagnitude<.001f)attackForward=Vector3.forward;
        if(driver.combatLookTarget!=null)
        {
            var toward=Vector3.ProjectOnPlane(driver.combatLookTarget.position-transform.position,Vector3.up);
            // A running cut follows the run; a target already behind cannot reverse the lunge.
            if(toward.sqrMagnitude>.001f&&(!running||Vector3.Dot(attackForward,toward.normalized)>.25f))attackForward=toward.normalized;
        }
        pursuitTarget=null;pursuitDistance=0;
        if(!running&&!orbSequence&&driver.acceptPlayerInput)PrepareMeleePursuit();
        PreviousAttackDistanceMoved=AttackDistanceMoved;plannedTravel=AttackDistanceMoved=0;
        runningStartSpeed=running&&!orbSequence?driver.PlanarSpeed:0;entryElapsed=entryTravelApplied=0;
        if(running)RunningDistanceMoved=0;
        fightReadyUntil=float.NegativeInfinity;animator.SetBool(FightReady,false);
        driver.SetAttackActive(true);animator.SetBool(Attacking,true);
        BlinkMeleePursuit(strike.forwardDistance);
        // Fixed-time offsets are measured in state seconds, including the state's playback speed.
        float blend=strike.blendSecondsOverride>=0?strike.blendSecondsOverride:blendSeconds;
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, strike.stateName,blend,0,strike.StartTime/strike.playbackSpeed);
        AttackStarted?.Invoke(CurrentStage,started,cancelledRecovery);
        return true;
    }
    void PrepareMeleePursuit()
    {
        var selection=equipment!=null?equipment.selection:FindFirstObjectByType<PlayerRoster>();
        if(selection==null||selection.characters==null)return;
        float nearest=swordSequence?3.8f:3.3f;
        foreach(var candidate in selection.characters)
        {
            if(candidate==null||candidate==driver||!candidate.isActiveAndEnabled||CharacterCombatStats.Dead(candidate))continue;
            var stats=candidate.GetComponent<CharacterCombatStats>();if(stats==null||!stats.enemyOnly)continue;
            var offset=candidate.transform.position-transform.position;
            if(Mathf.Abs(offset.y)>1.2f)continue;
            var toward=Vector3.ProjectOnPlane(offset,Vector3.up);float distance=toward.magnitude;
            if(distance<.01f||distance>nearest||Vector3.Dot(attackForward,toward/distance)<.5f||!PartyEnemyAI.HasLineOfSight(driver,candidate))continue;
            nearest=distance;pursuitTarget=candidate;
        }
        if(pursuitTarget==null)return;
        attackForward=Vector3.ProjectOnPlane(pursuitTarget.transform.position-transform.position,Vector3.up).normalized;
        pursuitDistance=Mathf.Min(swordSequence?.8f:.65f,Mathf.Max(0,nearest-1.05f));
        var facing=driver.visualRoot!=null?driver.visualRoot:transform;
        facing.rotation=Quaternion.LookRotation(attackForward,Vector3.up);
    }
    void BlinkMeleePursuit(float strikeDistance)
    {
        if (!UsesPursuitBlink || !pursuitTarget.isActiveAndEnabled || CharacterCombatStats.Dead(pursuitTarget)) return;
        float gap = Vector3.Dot(Vector3.ProjectOnPlane(pursuitTarget.transform.position-transform.position, Vector3.up), attackForward);
        float distance = Mathf.Min(.2f, Mathf.Max(strikeDistance, pursuitDistance), Mathf.Max(0, gap-1.05f));
        // Apply the whole approach this frame; the character capsule still blocks walls.
        var moved = driver.ApplyAttackDisplacement(attackForward*distance);
        AttackDistanceMoved += Mathf.Max(0, Vector3.Dot(moved, attackForward));
    }
    public static float RunningEntryTravel(float speed,float seconds)
    {
        float u=Mathf.Clamp01(seconds/.12f);
        return Mathf.Max(0,speed)*.12f*(u-u*u*.5f);
    }
    void AdvanceRunningEntry(float dt)
    {
        if(!IsRunningAttack||orbSequence||entryElapsed>=.12f)return;
        entryElapsed+=dt;float desired=RunningEntryTravel(runningStartSpeed,entryElapsed);
        var moved=driver.ApplyAttackDisplacement(attackForward*Mathf.Max(0,desired-entryTravelApplied));entryTravelApplied=desired;
        float actual=Mathf.Max(0,Vector3.Dot(moved,attackForward));AttackDistanceMoved+=actual;RunningDistanceMoved+=actual;
    }
    void Finish(bool enterFightIdle=true,bool preserveMomentum=false)
    {
        bool resumeRun=(preserveMomentum||enterFightIdle)&&IsRunningAttack&&!orbSequence&&driver.acceptPlayerInput&&driver.HasPlayerMovementInput;
        bool wasOrbDash=IsOrbAttack&&IsRunningAttack;
        if(wasOrbDash)GetComponent<OrbDashAttack>()?.EndMotion();
        IsAttacking=false;queued=-1;recoveryAction=0;driver.SetAttackActive(false);
        if(resumeRun)driver.ResumeAttackMomentum(attackForward,runningStartSpeed);
        fightReadyUntil=enterFightIdle?Time.time+fightIdleDuration:float.NegativeInfinity;
        var animator=driver.animator;
        if(animator==null||!animator.isActiveAndEnabled)return;
        animator.SetBool(Attacking,false);animator.SetBool(FightReady,IsFightReady);
        if(!enterFightIdle)return;
        bool idle=IsFightReady&&driver.IsGrounded&&driver.PlanarSpeed<.08f;
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, !driver.IsGrounded?(wasOrbDash?"RunningFall":"Fall"):idle?"FightIdle":"Locomotion",.10f,0,0);
    }
    void ClearSequence(){pendingSwordAttack=false;queued=-1;recoveryAction=0;lastClick=float.NegativeInfinity;if(!IsAttacking){stage=-1;CurrentStage=0;IsRunningAttack=false;}}
    void OnDisable(){if(driver!=null)Finish(false);ClearSequence();}
    static bool AttackHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null&&Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
    static bool Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}








