using UnityEngine;

/// <summary>A short additive flinch above the existing action, with no root or leg motion.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(20)]
public sealed class CharacterHitReaction : MonoBehaviour
{
    public const string LayerName = "HitReaction";
    public CharacterMotor driver;
    public float[] durations = { .32f, .36f, .30f };
    [Range(0,1)] public float strength = .65f;
    public float[] bigDurations={.82f,.86f,.40f,.42f};
    [Min(.1f)] public float bigStaggerDuration=.5f;
    [Min(.1f)] public float runningStaggerDuration=.16f;
    [Min(.01f)] public float bigReleaseDuration=.26f;
    [Range(0,1)] public float bigStrength=1f;
    public EnemyPoise.HitResponse LastResponse {get;private set;}
    public int HitCount { get; private set; }
    public int DodgeRejectCount { get; private set; }
    int rewardedDodge=-1;
    float nextImpactSound;
    public int LastVariant { get; private set; } = -1;
    public bool LastHitFromBack { get; private set; }
    public float Weight { get; private set; }
    public bool IsReacting => Weight > .001f || (driver!=null&&driver.IsActionBlocked);
    EnemyPoise poise;
    int layer = -1;
    float started = float.NegativeInfinity, duration, initialWeight,activeStrength;
    static readonly int[] States = {
        Animator.StringToHash("HitReaction.Rib"),
        Animator.StringToHash("HitReaction.Stomach"),
        Animator.StringToHash("HitReaction.BackHead"),
        Animator.StringToHash("HitReaction.BigHeadA"),Animator.StringToHash("HitReaction.BigHeadB"),
        Animator.StringToHash("HitReaction.RunningBody"),Animator.StringToHash("HitReaction.RunningHead")
    };

    void OnEnable()
    {
        if(driver==null)driver=GetComponent<CharacterMotor>();
        poise=GetComponent<EnemyPoise>();
        layer=driver.animator!=null?driver.animator.GetLayerIndex(LayerName):-1;
        Clear();
    }
    public static float DamageScale(int slot) => slot==1?1.4f:.8f;
    public static float PoiseScale(int slot) => slot==1?.6f:slot==2?.12f:.64f;
    public bool ReceiveHit(Vector3 attackerPosition,bool fullBodyKnockback=false,CharacterMotor attacker=null,bool special=false,float power=.35f,string attackType=null,int weaponSlot=0,float damageMultiplier=1)
    {
        if(!isActiveAndEnabled||driver==null||!driver.enabled||driver.animator==null||!driver.animator.isActiveAndEnabled)return false;
        if(CharacterCombatStats.Dead(driver))return false;
        if(driver.IsDodging)
        {
            DodgeRejectCount++;
            // One reward per roll, even if several hitboxes reach the invulnerable player.
            if(driver.acceptPlayerInput&&rewardedDodge!=driver.DodgeCount)
            {
                rewardedDodge=driver.DodgeCount;
                GetComponent<SwordThrowSkill>()?.ReduceCooldown(3);
                GetComponent<OrbBeamSkill>()?.ReduceCooldown(3);
            }
            return false;
        }
        if(layer<0)layer=driver.animator.GetLayerIndex(LayerName);
        var facing=driver.visualRoot!=null?driver.visualRoot:driver.transform;
        var toward=Vector3.ProjectOnPlane(attackerPosition-driver.transform.position,Vector3.up);
        LastHitFromBack=Vector3.Dot(facing.forward,toward)<0;
        var stats=GetComponent<CharacterCombatStats>();
        if(stats!=null)
        {
            // Player fists also need rebalancing: their fixed damage bypasses the attack stat.
            float fistDamage=attacker!=null&&attacker.GetComponent<CharacterCombatStats>() is CharacterCombatStats source&&!source.enemyOnly?3f:5f;
            if(!stats.ReceiveDamage(attacker,LastHitFromBack,weaponSlot==2?false:special,power*DamageScale(weaponSlot)*Mathf.Max(0,damageMultiplier),attackType,fixedBaseDamage:weaponSlot==0?fistDamage*Mathf.Max(0,damageMultiplier):-1))return false;
            if(Time.unscaledTime>=nextImpactSound)
            {
                GameplayAudio.Play(weaponSlot==1?GameplayCue.SwordHit:weaponSlot==2?GameplayCue.MagicBurst:GameplayCue.PunchHit,transform.position+Vector3.up,weaponSlot==2?.22f:.5f);
                nextImpactSound=Time.unscaledTime+.12f;
            }
            if(stats.IsDead){Clear();return true;}
        }
        if(poise==null)poise=GetComponent<EnemyPoise>();
        LastResponse=poise!=null?poise.ReceiveHit(driver.IsRunning,PoiseScale(weaponSlot)):EnemyPoise.HitResponse.Light;
        HitCount++;
        if(layer<0)return true;
        if(LastResponse==EnemyPoise.HitResponse.Knockdown||LastResponse==EnemyPoise.HitResponse.Downed){Clear();return true;}
        var knockback=GetComponent<SwordSkillKnockback>();
        // Damage/poise still resolve, while the authored full-body recoil owns the visible reaction.
        bool authoredKnockback=fullBodyKnockback&&knockback!=null&&knockback.reactionClip!=null&&driver.animator.HasState(0,Animator.StringToHash(SwordSkillKnockback.ReactionState));
        if(authoredKnockback||(knockback!=null&&knockback.IsReacting)
            ||(GetComponent<CharacterParryAction>() is CharacterParryAction counter&&counter.IsCounterRecoiling)){Clear();return true;}
        LastVariant=LastResponse==EnemyPoise.HitResponse.Big?Random.Range(3,5):LastResponse==EnemyPoise.HitResponse.RunningSide?Random.Range(5,7):LastHitFromBack?2:Random.Range(0,2);
        initialWeight=Weight;started=Time.time;duration=LastVariant<3?durations[LastVariant]:bigDurations[LastVariant-3];
        activeStrength=LastVariant<3?strength:bigStrength;
        if(LastVariant>=3)driver.InterruptForHit(LastResponse==EnemyPoise.HitResponse.Big?bigStaggerDuration:runningStaggerDuration);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Hit, States[LastVariant],LastResponse==EnemyPoise.HitResponse.Big?.025f:LastVariant>=3?.035f:.075f,layer,0);
        return true;
    }
    void Update()
    {
        if(layer<0||driver.animator==null)return;
        var knockback=GetComponent<SwordSkillKnockback>();
        if(driver.IsDodging||driver.IsIncapacitated||(knockback!=null&&knockback.IsReacting)
            ||(GetComponent<CharacterParryAction>() is CharacterParryAction counter&&counter.IsCounterRecoiling)){Clear();return;}
        float elapsed=Time.time-started;
        if(elapsed>=duration){Clear();return;}
        bool bigHit=LastResponse==EnemyPoise.HitResponse.Big;
        float attack=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/(bigHit?.025f:.035f)));
        float release=Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-elapsed)/(bigHit?bigReleaseDuration:.14f)));
        Weight=Mathf.Lerp(initialWeight,activeStrength,attack)*release;
        driver.animator.SetLayerWeight(layer,Weight);
    }
    void Clear()
    {
        Weight=0;started=float.NegativeInfinity;
        if(layer>=0&&driver!=null&&driver.animator!=null)driver.animator.SetLayerWeight(layer,0);
    }
    void OnDisable(){Clear();}
}
