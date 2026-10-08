using UnityEngine;
using Unity.Cinemachine;

/// <summary>Close-range clash: the first contact freezes both actors together, then resolves into recoil and guard.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(80)]
public sealed class CharacterParryAction : MonoBehaviour
{
    public const string LayerName="ParryUpper",StepState="ParryStep",RecoilState="ParryBigHit",ApproachState="ParryApproach",PaceParameter="ParryPace",StrikeState="ParryStrike";
    public static CharacterParryAction Active {get;private set;}
    public float approachSpeed=18f,approachTimeout=.8f,slowScale=.25f;
    public float duration=1.5f,contactTime=.48f,recoilDuration=2.5f,clashHold=.5f,contactTimeout=1.6f;
    public float slideDistance=3f,slideSeconds=.8f,recoilDistance=1.5f;
    public float actionSpeed=1.5f;
    [Tooltip("On contact, switch an existing lock-on to the parried opponent instead of dropping it.")]
    public bool lockOntoParriedTarget=true;
    [Tooltip("Also engage lock-on when the player was not locked. Off keeps the free camera free.")]
    public bool lockEvenWhenUnlocked;
    [Tooltip("Playback speed for the recoil/react half. New field, so existing prefabs pick up this default.")]
    public float resolveSpeed=1.35f;
    public AnimationCurve recoilRootX=new AnimationCurve(),recoilRootZ=new AnimationCurve();
    [Header("Clash effects")]
    [Tooltip("两人接触瞬间在碰撞点生成的特效。预制体或场景里的模板物体都可以。")]
    public GameObject contactEffect;
    [Tooltip("接触瞬间在摄像机前生成的特效。会挂到摄像机下，跟着镜头走。")]
    public GameObject cameraEffect;
    [Tooltip("碰撞点特效在接触点基础上的偏移（世界坐标，米）。Y 为正往上抬。")]
    public Vector3 contactEffectOffset=new Vector3(0f,.25f,0f);
    [Tooltip("摄像机前特效放在镜头前多远（米）。太近会被近裁剪面切掉。")]
    public float cameraEffectDistance=2f;
    [Tooltip("摄像机前特效在画面里的偏移（镜头本地坐标，米）。Y 为正往画面上方移。")]
    public Vector3 cameraEffectOffset=Vector3.zero;
    [Tooltip("特效存活秒数，到点自动销毁。")]
    public float effectLifetime=3f;
    [Tooltip("强制特效用 unscaled time。弹反期间 Time.timeScale 是 0，不开这个粒子会整个冻住。")]
    public bool effectsIgnoreTimeScale=true;
    [Tooltip("碰撞点特效绕 Y 轴转向对手。只加水平朝向，模板自己的旋转（比如默认的 X -90°）会保留。关掉则完全按模板的旋转生成。")]
    public bool alignContactEffectToClash=true;
    public AnimationClip blockClip,recoilClip;
    public bool OwnsBody {get;private set;}
    public bool IsCounterRecoiling=>recoiling;
    public bool FreezesWorld=>OwnsBody;
    public float RequestedScale=>0;
    public void SetPace(float scale){if(OwnsBody)driver.animator.SetFloat(PaceParameter,1);}
    public bool CanBegin=>isActiveAndEnabled&&driver!=null&&driver.acceptPlayerInput&&driver.IsGrounded
        &&!driver.IsDodging&&!driver.JumpPreparing&&!driver.IsActionBlocked&&!driver.equipmentBusy&&!OwnsBody&&blockClip!=null&&layer>=0;
    enum Phase { Clash, Hold, Resolve }
    Phase phase;
    CharacterMotor driver,enemy;
    CharacterParryAction enemyAction;
    CharacterComboAttack combo;
    AnimatorUpdateMode playerMode,enemyMode;
    float playerSpeed,enemySpeed,elapsed,recoilStarted,recoilTravelSeconds=.55f;
    int layer=-1;
    bool playerContact,enemyContact,mirror,recoiling,counterApplied;
    Transform playerLimb,enemyLimb;
    Vector3 previousPlayerLimb,previousEnemyLimb;
    Vector3 slideHeading,lastRecoilRoot;
    Quaternion recoilHeading;
    float slideApplied;
    ParryCamera parryCamera;
    CinemachineBrain parryBrain;
    bool brainIgnoredTimeScale;
    void OnEnable(){driver=GetComponent<CharacterMotor>();combo=GetComponent<CharacterComboAttack>();if(driver.animator!=null)layer=driver.animator.GetLayerIndex(LayerName);}
    static ParryCamera ResolveParryCamera()
    {
        var selection=FindAnyObjectByType<PlayerRoster>();
        return selection!=null&&selection.followCamera!=null?selection.followCamera.GetComponent<ParryCamera>():null;
    }
    /// <summary>Serialised values on an existing prefab ignore code defaults; run this to pull them across.</summary>
    [ContextMenu("Apply recommended clash timing")]
    void ApplyRecommendedTiming()
    {
        slideSeconds=.55f;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[CharacterParryAction] slideSeconds set to 0.55s.",this);
    }
    public bool Begin(CharacterMotor attacker)
    {
        if(!CanBegin||Active!=null||attacker==null||attacker.IsActionBlocked)return false;
        var action=attacker.GetComponent<CharacterParryAction>();var otherCombo=attacker.GetComponent<CharacterComboAttack>();
        var incoming=otherCombo!=null?otherCombo.ActiveStrike:null;
        if(action==null||action.recoilClip==null||incoming==null||combo.attacks==null||combo.attacks.Length==0)return false;
        if(!driver.animator.HasState(0,Animator.StringToHash(StrikeState)))return false;
        var capsule=GetComponent<CharacterController>();var other=attacker.GetComponent<CharacterController>();
        float gap=Mathf.Max(.68f,(capsule!=null?capsule.radius:0)+(other!=null?other.radius:0)+.06f);
        var enemyBody=attacker.visualRoot!=null?attacker.visualRoot:attacker.transform;
        var forward=Vector3.ProjectOnPlane(enemyBody.forward,Vector3.up).normalized;
        var destination=attacker.transform.position+forward*gap;
        if(Mathf.Abs(destination.y-transform.position.y)>.65f||!PartyEnemyAI.HasLineOfSight(driver,attacker))return false;
        var incomingState=attacker.animator.IsInTransition(0)?attacker.animator.GetNextAnimatorStateInfo(0):attacker.animator.GetCurrentAnimatorStateInfo(0);
        var start=transform.position;
        combo.CancelForControlChange();driver.SetAttackActive(true);
        driver.ApplyAttackDisplacement(Vector3.ProjectOnPlane(destination-start,Vector3.up));
        if(Vector3.ProjectOnPlane(destination-transform.position,Vector3.up).magnitude>.18f)
        {driver.SetAttackActive(false);return false;}
        enemy=attacker;enemyAction=action;OwnsBody=true;Active=this;phase=Phase.Clash;elapsed=0;
        playerContact=enemyContact=counterApplied=mirror=false;
        playerMode=driver.animator.updateMode;enemyMode=enemy.animator.updateMode;
        playerSpeed=driver.animator.speed;enemySpeed=enemy.animator.speed;
        driver.animator.updateMode=enemy.animator.updateMode=AnimatorUpdateMode.UnscaledTime;
        driver.animator.speed=enemy.animator.speed=actionSpeed;
        var body=driver.visualRoot!=null?driver.visualRoot:transform;body.rotation=Quaternion.LookRotation(-forward);
        driver.animator.SetBool("IsAttacking",true);driver.animator.SetFloat(PaceParameter,1);driver.animator.SetLayerWeight(layer,0);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, StrikeState,.04f,0,0);
        playerLimb=driver.animator.GetBoneTransform(combo.attacks[0].contactBone);
        enemyLimb=enemy.animator.GetBoneTransform(incoming.contactBone);
        // Cancel ordinary damage, while preserving the attack pose and its remaining animation.
        enemy.InterruptForHit(contactTimeout+clashHold+duration+1,false);
        // Re-stage the same incoming strike's windup for the paired contact; a late click
        // must not leave its limb already past the newly placed player.
        enemy.Animation.Play(CharacterAnimationDirector.Action.Hit, incomingState.fullPathHash,0,Mathf.Min(incomingState.normalizedTime,.05f));
        previousPlayerLimb=playerLimb!=null?playerLimb.position:transform.position;
        previousEnemyLimb=enemyLimb!=null?enemyLimb.position:enemy.transform.position;
        // Start the camera arc now, timed to land exactly when the limbs are due to meet.
        parryCamera=ResolveParryCamera();
        if(parryCamera!=null&&parryCamera.enabled)parryCamera.Begin(driver,enemy,contactTime);
        // The clash parks Time.timeScale at zero, which would otherwise hand the brain a
        // delta of zero and freeze the camera in place while both actors keep moving.
        var selection=FindAnyObjectByType<PlayerRoster>();
        parryBrain=selection!=null&&selection.outputCamera!=null?selection.outputCamera.GetComponent<CinemachineBrain>():null;
        if(parryBrain!=null){brainIgnoredTimeScale=parryBrain.IgnoreTimeScale;parryBrain.IgnoreTimeScale=true;}
        return true;
    }
    void Update()
    {
        if(driver==null||driver.animator==null)return;
        if(recoiling&&(driver.IsIncapacitated||Time.unscaledTime-recoilStarted>=recoilDuration))
        {
            recoiling=false;driver.EndAnimatedHit();
            if(!driver.IsActionBlocked)driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, driver.IsGrounded?"Locomotion":"Fall",.14f,0,0);
        }
        // The recoil travel belongs to whoever is recoiling, so it survives the parry ending.
        else if(recoiling)ApplyRecoilMotion(Mathf.Clamp01((Time.unscaledTime-recoilStarted)/Mathf.Max(.01f,recoilTravelSeconds)));
        if(!OwnsBody)return;
        if(!driver.acceptPlayerInput||driver.IsActionBlocked||enemy==null||!enemy.gameObject.activeInHierarchy||enemy.IsIncapacitated||enemyAction==null||!enemyAction.isActiveAndEnabled)
        {Finish();return;}
        elapsed+=Time.unscaledDeltaTime;
        int weapon=driver.animator.GetLayerIndex("WeaponAction");if(weapon>=0)driver.animator.SetLayerWeight(weapon,0);
        if(phase==Phase.Clash){if(elapsed>contactTimeout)Finish();return;}
        if(phase==Phase.Hold&&elapsed>=clashHold)
        {
            // The camera keeps the clash framing through the slide; Finish() releases it.
            phase=Phase.Resolve;elapsed=0;driver.animator.speed=enemy.animator.speed=Mathf.Max(.1f,resolveSpeed);
            driver.animator.SetLayerWeight(layer,0);
            driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, mirror?"ParryReactMirror":"ParryReact",.06f,0,0);
            slideHeading=Vector3.ProjectOnPlane(transform.position-enemy.transform.position,Vector3.up).normalized;slideApplied=0;
            enemyAction.ReceiveCounter(slideSeconds);counterApplied=true;
        }
        if(phase==Phase.Resolve)
        {
            float u=Mathf.Clamp01(elapsed/Mathf.Max(.01f,slideSeconds));
            float desired=slideDistance*(2*u-u*u);
            driver.ApplyAttackDisplacement(slideHeading*(desired-slideApplied));slideApplied=desired;
            // Both actors stop travelling together, and control comes back at that moment.
            if(elapsed>=slideSeconds)Finish();
        }
    }
    void LateUpdate()
    {
        if(!OwnsBody||phase!=Phase.Clash||enemy==null)return;
        playerContact=playerLimb!=null&&elapsed>.06f&&LimbTouches(previousPlayerLimb,playerLimb,enemy.animator);
        enemyContact=enemyLimb!=null&&elapsed>.06f&&LimbTouches(previousEnemyLimb,enemyLimb,driver.animator);
        if(enemyLimb!=null&&(playerContact||enemyContact))
        {
            var body=driver.visualRoot!=null?driver.visualRoot:transform;
            var chest=driver.animator.GetBoneTransform(HumanBodyBones.Chest)??driver.animator.GetBoneTransform(HumanBodyBones.Hips);
            mirror=Vector3.Dot(enemyLimb.position-chest.position,body.right)<0;
        }
        if(playerLimb!=null)previousPlayerLimb=playerLimb.position;
        if(enemyLimb!=null)previousEnemyLimb=enemyLimb.position;
        if(playerContact||enemyContact)
        {
            // A single shared contact boundary: neither actor waits alone in a frozen pose.
            driver.animator.speed=enemy.animator.speed=0;
            phase=Phase.Hold;elapsed=0;
            GetComponent<ParryClashAudio>()?.Play();
            var selection=FindAnyObjectByType<PlayerRoster>();
            var lockOn=selection!=null?selection.GetComponent<CombatLockOn>():null;
            // Keep the lock through the clash and hand it to whoever was parried.
            if(lockOn!=null&&lockOntoParriedTarget)lockOn.SwitchLockTo(enemy.transform,lockEvenWhenUnlocked);
            // The arc has been running since Begin; fold whatever is left into the freeze.
            if(parryCamera==null)parryCamera=ResolveParryCamera();
            if(parryCamera!=null&&parryCamera.enabled)parryCamera.MarkContact(mirror);
            SpawnClashEffects();
        }
    }
    // Sweep the attacking hand/foot against anatomical capsules, independent of ordinary damage markers.
    static readonly HumanBodyBones[] segments={HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Chest,HumanBodyBones.Head,
        HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
        HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,
        HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
    static bool LimbTouches(Vector3 previous,Transform limb,Animator target)=>Touches(previous,limb.position,target)||limb.parent!=null&&Touches(limb.parent.position,limb.position,target);
    static bool Touches(Vector3 from,Vector3 to,Animator target)
    {
        int steps=Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from,to)/.035f),1,64);
        for(int i=0;i<segments.Length;i+=2){var a=target.GetBoneTransform(segments[i]);var b=target.GetBoneTransform(segments[i+1]);if(a==null||b==null)continue;
            float radius=i<4?.32f:.22f;var ab=b.position-a.position;
            for(int j=0;j<=steps;j++){var p=Vector3.Lerp(from,to,(float)j/steps);var closest=a.position+ab*Mathf.Clamp01(Vector3.Dot(p-a.position,ab)/Mathf.Max(.00001f,ab.sqrMagnitude));if((p-closest).sqrMagnitude<=radius*radius)return true;}}
        return false;
    }
    void ReceiveCounter(float travelSeconds){recoilTravelSeconds=Mathf.Max(.01f,travelSeconds);GetComponent<EnemyPoise>()?.ReceiveParry();driver.InterruptForHit(recoilDuration+.1f,false);recoiling=true;recoilStarted=Time.unscaledTime;
        recoilHeading=(driver.visualRoot!=null?driver.visualRoot:transform).rotation;lastRecoilRoot=Vector3.zero;
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Hit, RecoilState,.06f,0,0);}
    void ApplyRecoilMotion(float progress)
    {
        if(driver.IsIncapacitated||recoilClip==null)return;
        float t=progress*recoilClip.length;
        var root=new Vector3(recoilRootX.Evaluate(t)-recoilRootX.Evaluate(0),0,recoilRootZ.Evaluate(t)-recoilRootZ.Evaluate(0));
        var controller=GetComponent<CharacterController>();
        float authoredDistance=Mathf.Abs(recoilRootZ.Evaluate(recoilClip.length)-recoilRootZ.Evaluate(0));
        float scale=recoilDistance/Mathf.Max(.001f,authoredDistance);
        if(controller!=null&&controller.enabled)controller.Move(recoilHeading*((root-lastRecoilRoot)*scale));
        lastRecoilRoot=root;
    }
    [Tooltip("在 Console 打印特效生成情况，排查用。")]
    public bool logEffectSpawns=true;

    /// <summary>不打架直接测一发：右键组件标题栏 -> Test clash effects。</summary>
    [ContextMenu("Test clash effects")]
    void TestClashEffects()
    {
        if(!Application.isPlaying){Debug.LogWarning("[Parry] 需要在 Play 模式下测试。",this);return;}
        Debug.Log("[Parry] 手动测试特效生成。",this);
        SpawnClashEffects();
    }

    /// <summary>接触瞬间：碰撞点一个，摄像机前一个。两个字段都可以留空。</summary>
    void SpawnClashEffects()
    {
        if(logEffectSpawns)
            Debug.Log($"[Parry] 接触判定成立，开始生成特效。contactEffect={(contactEffect!=null?contactEffect.name:"<空>")} "
                     +$"cameraEffect={(cameraEffect!=null?cameraEffect.name:"<空>")}，组件挂在 {name} 上。",this);
        if(contactEffect==null&&cameraEffect==null)
            Debug.LogWarning($"[Parry] {name} 上的 Contact Effect / Camera Effect 两个字段都是空的。"
                            +"特效要拖在【实际触发弹反的那个角色】的 CharacterParryAction 上，两个角色各有一份。",this);
        if(contactEffect!=null)
        {
            // 模板自己的旋转必须保留：Unity 新建的 Particle System 默认是 X -90°（朝上喷），
            // 直接用 LookRotation 覆盖会把它整个放倒。这里只在它外面再套一层水平朝向。
            var authored=contactEffect.transform.localRotation;
            var yaw=Quaternion.identity;
            if(alignContactEffectToClash&&enemy!=null)
            {
                var heading=Vector3.ProjectOnPlane(enemy.transform.position-transform.position,Vector3.up);
                if(heading.sqrMagnitude>.0001f)yaw=Quaternion.LookRotation(heading.normalized,Vector3.up);
            }
            SpawnEffect(contactEffect,ContactPoint()+contactEffectOffset,yaw*authored,null);
        }
        if(cameraEffect!=null)
        {
            var view=ResolveOutputCamera();
            if(view==null)Debug.LogWarning("[Parry] 找不到输出摄像机，Camera Effect 跳过。",this);
            if(view!=null)
            {
                var lens=view.transform;
                // 挂在摄像机下面，这样弹反期间镜头推进、dutch 翻滚时特效跟着走，不会甩出画面。
                // 同样保留模板旋转：相对镜头的朝向 = 模板在场景里相对世界的朝向。
                SpawnEffect(cameraEffect,lens.position+lens.forward*Mathf.Max(.05f,cameraEffectDistance)
                                        +lens.rotation*cameraEffectOffset,
                            lens.rotation*cameraEffect.transform.localRotation,lens);
            }
        }
    }
    /// <summary>两只接触肢体的中点；取不到就退回到出手方的肢体，再不行用胸口高度。</summary>
    Vector3 ContactPoint()
    {
        if(playerLimb!=null&&enemyLimb!=null)return (playerLimb.position+enemyLimb.position)*.5f;
        if(enemyLimb!=null)return enemyLimb.position;
        if(playerLimb!=null)return playerLimb.position;
        return transform.position+Vector3.up*1.1f;
    }
    void SpawnEffect(GameObject source,Vector3 position,Quaternion rotation,Transform parent)
    {
        var instance=Instantiate(source,position,rotation,parent);
        instance.SetActive(true);   // 场景里的模板物体通常是关着的
        var systems=instance.GetComponentsInChildren<ParticleSystem>(true);
        // 1) 先把所有层都准备好：激活、切 unscaled time、停掉并清空（Instantiate 时 playOnAwake 可能已经开播了）
        foreach(var system in systems)
        {
            system.gameObject.SetActive(true);
            if(effectsIgnoreTimeScale){var main=system.main;main.useUnscaledTime=true;}
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        // 2) 只从顶层启动一次，子层由它带着一起播。
        //    不能对每一层都 Clear+Play：父级 Play 时子级第 0 帧的 burst 已经发射了，
        //    轮到子级 Clear 会把它清掉，而子级已处于"播放中"，再 Play 不会重新触发 burst。
        //    Simulate(0,true,true) 把整棵树的时间归零，再 Play(true)，是 Unity 里最稳的重启写法。
        int count=systems.Length,roots=0;
        foreach(var system in systems)
            if(IsTopLevel(system,instance.transform))
            {
                system.Simulate(0f,true,true);
                system.Play(true);
                roots++;
            }
        if(logEffectSpawns)
            Debug.Log($"[Parry] 已生成 {instance.name}，位置 {position}，共 {count} 个 ParticleSystem（从 {roots} 个顶层启动）。"
                     +(count==0?" 注意：这个物体上一个 ParticleSystem 都没有。":""),instance);
        if(logEffectSpawns)StartCoroutine(ProbeEffect(instance,systems));
        // 注意用协程而不是 Destroy(obj,t)：后者的延时走 scaled time，
        // 弹反期间 timeScale 是 0，计时不走，特效会一直留在场上。
        StartCoroutine(DestroyUnscaled(instance,Mathf.Max(.1f,effectLifetime)));
    }
    /// <summary>在 root 之内往上找，路上没有别的 ParticleSystem 才算顶层。</summary>
    static bool IsTopLevel(ParticleSystem system,Transform root)
    {
        for(var t=system.transform;t!=root;)
        {
            t=t.parent;
            if(t==null)break;
            if(t.GetComponent<ParticleSystem>()!=null)return false;
        }
        return true;
    }
    /// <summary>排查用：生成后下一帧和 0.1 秒后，把每一层的实际运行状态打出来。</summary>
    static System.Collections.IEnumerator ProbeEffect(GameObject instance,ParticleSystem[] systems)
    {
        yield return null;
        Report("下一帧",instance,systems);
        yield return new WaitForSecondsRealtime(.1f);
        Report("0.1 秒后",instance,systems);
    }
    // 每层单独一条 Debug.Log：Console 列表里每条只显示前两行，合成一条多行日志会把后面几层折叠掉。
    static void Report(string when,GameObject instance,ParticleSystem[] systems)
    {
        if(instance==null){Debug.Log($"[Parry][探针 {when}] 实例已被销毁。");return;}
        Debug.Log($"[Parry][探针 {when}] {instance.name} 共 {systems.Length} 层，Time.timeScale={Time.timeScale}",instance);
        foreach(var ps in systems)
        {
            if(ps==null){Debug.Log($"[Parry][探针 {when}]   <已销毁>");continue;}
            var r=ps.GetComponent<ParticleSystemRenderer>();
            Debug.Log($"[Parry][探针 {when}]   {ps.name}: 粒子数={ps.particleCount} 在播={ps.isPlaying} 被看见={(r!=null&&r.isVisible)} "
                     +$"time={ps.time:0.000} unscaled={ps.main.useUnscaledTime} 激活={ps.gameObject.activeInHierarchy} "
                     +$"坐标={ps.transform.position} 缩放={ps.transform.lossyScale}",ps);
        }
    }
    static System.Collections.IEnumerator DestroyUnscaled(GameObject instance,float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if(instance!=null)Destroy(instance);
    }
    static Camera ResolveOutputCamera()
    {
        var selection=FindAnyObjectByType<PlayerRoster>();
        return selection!=null&&selection.outputCamera!=null?selection.outputCamera:Camera.main;
    }
    void Finish()
    {
        if(!OwnsBody)return;
        if(parryCamera!=null)parryCamera.End();
        if(parryBrain!=null){parryBrain.IgnoreTimeScale=brainIgnoredTimeScale;parryBrain=null;}
        OwnsBody=false;if(Active==this)Active=null;
        driver.animator.updateMode=playerMode;driver.animator.speed=playerSpeed;driver.animator.SetFloat(PaceParameter,1);driver.animator.SetLayerWeight(layer,0);
        driver.SetAttackActive(false);driver.animator.SetBool("IsAttacking",false);
        if(!driver.IsActionBlocked)driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, driver.IsGrounded?"Locomotion":"Fall",.12f,0,0);
        if(enemy!=null){enemy.animator.updateMode=enemyMode;enemy.animator.speed=enemySpeed;if(!counterApplied){enemy.EndAnimatedHit();if(!enemy.IsActionBlocked)enemy.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.12f,0,0);}}
        enemy=null;enemyAction=null;
    }
    public void Interrupt()
    {
        if(driver!=null){Finish();if(recoiling)driver.EndAnimatedHit();}
        recoiling=false;
    }
    void OnDisable(){Interrupt();}
}

