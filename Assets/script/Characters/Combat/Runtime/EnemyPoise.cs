using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(10)]
public sealed class EnemyPoise : MonoBehaviour
{
    public enum HitResponse { Light, Big, RunningSide, Knockdown, Downed }
    public enum RecoveryPhase { Ready, Falling, Grounded, GettingUp }
    public PlayerRoster selection;
    public float maximum=100,damagePerHit=25,bigHitThreshold=50;
    [Min(0)] public float recoveryDelay=3f,recoveryPerSecond=3;
    [Min(5)] public float groundPause=5f;
    public float Current {get;private set;}=100;
    public RecoveryPhase Phase {get;private set;}
    public bool IsIncapacitated=>Phase!=RecoveryPhase.Ready;
    public bool IsEnemy=>driver!=null&&(selection!=null?selection.ActiveCharacter!=driver:!driver.acceptPlayerInput);
    public int KnockdownCount {get;private set;}
    public const string FallState="PoiseFall",GetUpState="PoiseGetUp";
    CharacterMotor driver;
    float lastHit=float.NegativeInfinity,groundedAt;
    bool wasEnemy;
    void OnEnable()
    {
        driver=GetComponent<CharacterMotor>();
        if(selection==null)selection=FindFirstObjectByType<PlayerRoster>();
        Current=maximum;Phase=RecoveryPhase.Ready;lastHit=float.NegativeInfinity;wasEnemy=IsEnemy;
    }
    public HitResponse ReceiveHit(bool running,float damageScale=1)
    {
        if(IsIncapacitated){Current=maximum;return HitResponse.Downed;}
        if(!IsEnemy)return HitResponse.Light;
        Current-=damagePerHit*Mathf.Max(0,damageScale);lastHit=Time.time;
        if(Current<0)
        {
            // Zero is still standing. Only crossing below zero starts recovery.
            Current=maximum;KnockdownCount++;Phase=RecoveryPhase.Falling;
            driver.SetIncapacitated(true);
            int weapon=driver.animator.GetLayerIndex("WeaponAction");if(weapon>=0)driver.animator.SetLayerWeight(weapon,0);
            driver.Animation.CrossFade(CharacterAnimationDirector.Action.Knockdown, FallState,.10f,0,0);
            return HitResponse.Knockdown;
        }
        return Current<bigHitThreshold?(running?HitResponse.RunningSide:HitResponse.Big):HitResponse.Light;
    }
    public void ReceiveParry()
    {
        if(IsIncapacitated||!IsEnemy)return;
        // The guard guarantees a Big Hit. Leave zero standing, as ordinary poise
        // already does, so a subsequent hit can trigger the normal knockdown.
        Current=Mathf.Max(0,Current-maximum*.75f);lastHit=Time.time;
    }
    void Update()
    {
        if(driver==null||driver.animator==null||CharacterCombatStats.Dead(driver))return;
        if(IsIncapacitated)
        {
            Current=maximum;
            int weapon=driver.animator.GetLayerIndex("WeaponAction");if(weapon>=0)driver.animator.SetLayerWeight(weapon,0);
            var animator=driver.animator;
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            if(Phase==RecoveryPhase.Falling&&state.IsName(FallState)&&state.normalizedTime>=.99f&&!animator.IsInTransition(0))
            {Phase=RecoveryPhase.Grounded;groundedAt=Time.time;}
            else if(Phase==RecoveryPhase.Grounded&&Time.time-groundedAt>=Mathf.Max(5f,groundPause))
            {Phase=RecoveryPhase.GettingUp;driver.Animation.CrossFade(CharacterAnimationDirector.Action.Knockdown, GetUpState,.24f,0,0);}
            else if(Phase==RecoveryPhase.GettingUp&&state.IsName(GetUpState)&&state.normalizedTime>=.98f&&!animator.IsInTransition(0))
            {
                Phase=RecoveryPhase.Ready;lastHit=Time.time;driver.SetIncapacitated(false);
                driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.18f,0,0);
            }
            return;
        }
        bool enemy=IsEnemy;
        if(enemy!=wasEnemy){Current=maximum;lastHit=Time.time;wasEnemy=enemy;}
        if(enemy&&Time.time-lastHit>=recoveryDelay)Current=Mathf.Min(maximum,Current+recoveryPerSecond*Time.deltaTime);
    }
    void OnDisable(){if(driver!=null)driver.SetIncapacitated(false);Phase=RecoveryPhase.Ready;Current=maximum;}
}

