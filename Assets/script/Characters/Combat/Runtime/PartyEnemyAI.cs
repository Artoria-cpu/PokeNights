using UnityEngine;
using UnityEngine.AI;

/// <summary>AI supplies movement and attack intent; the existing character controller owns animation and collisions.</summary>
[DefaultExecutionOrder(-30), DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor),typeof(CharacterComboAttack))]
public sealed class PartyEnemyAI : MonoBehaviour
{
    public enum Behaviour { PlayerControlled, Idle, Chasing, Locked, Attacking, Returning, Approaching, Evading, Downed, Staggered, Dead, Patrolling }
    public PlayerRoster selection;
    [Header("Activation and authored encounters")]
    public bool waitForHit;
    public bool neverFight;
    public bool useNavMesh = true;
    [Tooltip("-1 uses a random weapon; 0 fists, 1 sword, 2 orb.")]
    public int weaponSlotOverride = -1;
    [Tooltip("-1 uses random attacks; otherwise use this zero-based attack index.")]
    public int attackIndexOverride = -1;
    [Min(1)] public float detectionRange=25,loseRange=32,homeLeash=40;
    [Min(.1f)] public float lockRange=6,attackRange=1.25f,preferredDistance=1.05f;
    [Min(.1f)] public float orbitDistance=4;
    [Range(.1f,1)] public float orbitMovement=.65f;
    public Vector2 attackInterval=new Vector2(2,5);
    [Range(0,1)] public float secondAttackChance=.35f;
    [Range(0,1)] public float dodgeChance=.45f;
    public Vector2 dodgeCooldown=new Vector2(2.5f,4.5f);
    [Min(.1f)] public float lostSightSeconds=3;
    [Header("Nearby patrol")]
    [Min(1)] public float patrolRadius=6f;
    public Vector2 patrolPause=new Vector2(1.5f,4f);
    Vector3 patrolDestination;
    float nextPatrol,patrolDeadline;
    bool patrolWalking;
    public Behaviour State { get; private set; }
    public int SequencesStarted { get; private set; }
    public int LastOpeningStage { get; private set; }
    public int LastSequenceLength { get; private set; }
    public float NextAttackTime { get; private set; }
    public bool IsEngaged => target!=null&&(State==Behaviour.Chasing||State==Behaviour.Locked||State==Behaviour.Approaching||State==Behaviour.Attacking||State==Behaviour.Evading);
    CharacterMotor driver,target;
    CharacterComboAttack combo;
    CharacterComboAttack targetCombo;
    CharacterHitReaction reaction;
    Vector3 home,lastKnown;
    bool wasControlled,initialized,seen,secondPlanned,secondQueued,sequenceActive;
    int attackBase,secondIndex,orbitSign=1;
    float nextSense,lastSeen,nextPath,nextOrbit;
    float provokedUntil=float.NegativeInfinity;
    float nextDodge,pendingDodge=float.PositiveInfinity;
    int consideredAttack=-1;
    NavMeshPath path;
    Vector3[] corners=new Vector3[0];
    int corner;
    static readonly RaycastHit[] sightHits=new RaycastHit[24];
    public const int MaximumSimultaneousAttackers=3;
    public const int MaximumPursuers=6;
    bool pursuing;
    public bool IsPursuing=>pursuing&&isActiveAndEnabled&&driver!=null&&driver.isActiveAndEnabled&&!CharacterCombatStats.Dead(driver)&&!driver.IsIncapacitated;

    public bool TryClaimPursuit()
    {
        if(driver==null||!driver.isActiveAndEnabled||CharacterCombatStats.Dead(driver)||driver.IsIncapacitated||selection==null)return false;
        if(IsPursuing)return true;
        int count=0;
        foreach(var other in selection.characters)
        {
            if(other==null||other==driver)continue;
            var ai=other.GetComponent<PartyEnemyAI>();
            if(ai!=null&&ai.IsPursuing&&++count>=MaximumPursuers)return false;
        }
        pursuing=true;return true;
    }

    public bool HasAttackSlot()
    {
        if(selection==null||selection.characters==null)return false;
        int attacking=0;
        foreach(var other in selection.characters)
        {
            if(other==null||other==driver||!other.isActiveAndEnabled||CharacterCombatStats.Dead(other))continue;
            var stats=other.GetComponent<CharacterCombatStats>();
            if(stats==null||!stats.enemyOnly)continue;
            var ai=other.GetComponent<PartyEnemyAI>();var attack=other.GetComponent<CharacterComboAttack>();
            if((attack!=null&&attack.IsAttacking)||(ai!=null&&ai.isActiveAndEnabled&&ai.sequenceActive))
                if(++attacking>=MaximumSimultaneousAttackers)return false;
        }
        return true;
    }

    void Start()
    {
        var stats=GetComponent<CharacterCombatStats>();
        if(stats==null||!stats.enemyOnly)return;
        driver.SetPlayerInput(false);
        float roll=Random.value;
        var loadout=GetComponent<EnemyWeaponLoadout>()??gameObject.AddComponent<EnemyWeaponLoadout>();
        float authoredRange=attackRange,authoredDistance=preferredDistance,authoredLock=lockRange,authoredOrbit=orbitDistance;
        loadout.Initialize(combo.equipment,weaponSlotOverride>=0?weaponSlotOverride:roll<.4f?0:roll<.7f?1:2);
        if(weaponSlotOverride>=0){attackRange=authoredRange;preferredDistance=authoredDistance;lockRange=authoredLock;orbitDistance=authoredOrbit;}
    }
    void Awake()
    {
        path=new NavMeshPath();
        driver=GetComponent<CharacterMotor>();combo=GetComponent<CharacterComboAttack>();reaction=GetComponent<CharacterHitReaction>();
        if(selection==null)selection=FindFirstObjectByType<PlayerRoster>();
        home=transform.position;
    }
    public void RefreshControl()
    {
        if(driver==null||selection==null)return;
        bool controlled=selection.ActiveCharacter==driver;
        var player=selection.ActiveCharacter;
        if(initialized&&wasControlled==controlled&&target==player)return;
        initialized=true;wasControlled=controlled;target=player;pursuing=false;
        targetCombo=target!=null?target.GetComponent<CharacterComboAttack>():null;
        consideredAttack=-1;pendingDodge=float.PositiveInfinity;nextDodge=Time.time+.3f;
        combo.CancelForControlChange();driver.ClearAIInput();driver.combatLookTarget=null;
        sequenceActive=false;seen=false;patrolWalking=false;nextPatrol=Time.time+Random.Range(0f,2f);nextSense=nextPath=0;corners=new Vector3[0];home=transform.position;
        orbitSign=Random.value<.5f?-1:1;nextOrbit=Time.time+Random.Range(1.8f,3.4f);
        State=controlled?Behaviour.PlayerControlled:Behaviour.Idle;
    }
    public void NotifyAttacked(CharacterMotor attacker)
    {
        if(!isActiveAndEnabled||neverFight||attacker==null||CharacterCombatStats.Dead(this))return;
        var source=attacker.GetComponent<CharacterCombatStats>();
        if(source==null||source.enemyOnly)return;
        RefreshControl();
        if(driver==null||target==null||wasControlled)return;
        provokedUntil=Time.time+8f;
        lastSeen=Time.time;lastKnown=target.transform.position;nextSense=nextPath=0;
        if(State==Behaviour.Returning||State==Behaviour.Idle||State==Behaviour.Patrolling)
        {
            patrolWalking=false;State=Behaviour.Chasing;
            NextAttackTime=Time.time+.5f;
        }
    }
    bool ShouldReturn(float distance)
    {
        // Receiving damage keeps the enemy engaged even beyond its patrol boundary.
        if(Time.time<provokedUntil)return false;
        return distance>loseRange||Time.time-lastSeen>lostSightSeconds
            ||(PlanarDistance(home,transform.position)>homeLeash&&!(seen&&distance<=lockRange+2f));
    }
    void Update()
    {
        if(CharacterCombatStats.Dead(this)){pursuing=false;State=Behaviour.Dead;driver.ClearAIInput();return;}
        RefreshControl();
        var stats=GetComponent<CharacterCombatStats>();
        if(!wasControlled&&(stats==null||!stats.enemyOnly))
        {
            State=Behaviour.Idle;pursuing=false;sequenceActive=false;pendingDodge=float.PositiveInfinity;
            driver.ClearAIInput();driver.combatLookTarget=null;combo.SetAICombatReady(false);return;
        }
        if(selection==null||driver==null||target==null||wasControlled||Time.deltaTime<=0)return;
        if(neverFight||(waitForHit&&stats.DamageCount==0))
        {
            State=Behaviour.Idle;pursuing=false;sequenceActive=false;patrolWalking=false;pendingDodge=float.PositiveInfinity;
            driver.SetAIInput(Vector3.zero,false,null);driver.combatLookTarget=null;combo.SetAICombatReady(false);return;
        }
        if(CharacterCombatStats.Dead(target)){pursuing=false;driver.SetAIInput(Vector3.zero,false,null);combo.SetAICombatReady(false);return;}
        if(driver.IsIncapacitated)
        {
            State=Behaviour.Downed;pursuing=false;sequenceActive=false;pendingDodge=float.PositiveInfinity;
            driver.SetAIInput(Vector3.zero,false,null);combo.SetAICombatReady(false);return;
        }
        if(driver.IsHitStunned)
        {
            State=Behaviour.Staggered;sequenceActive=false;pendingDodge=float.PositiveInfinity;
            driver.SetAIInput(Vector3.zero,false,target.transform);combo.SetAICombatReady(false);return;
        }
        if(State==Behaviour.Downed||State==Behaviour.Staggered){State=Behaviour.Chasing;nextSense=nextPath=0;lastSeen=Time.time;ScheduleAttack();}
        if(!driver.enabled||!target.isActiveAndEnabled){LoseTarget();return;}
        float distance=PlanarDistance(transform.position,target.transform.position);
        if(Time.time>=nextSense)
        {
            nextSense=Time.time+.2f;seen=HasLineOfSight(driver,target);
            if(seen){lastSeen=Time.time;lastKnown=target.transform.position;}
        }
        if(State==Behaviour.Idle||State==Behaviour.Patrolling)
        {
            driver.SetAIInput(Vector3.zero,false,null);combo.SetAICombatReady(false);
            if(((distance<=detectionRange&&seen)||Time.time<provokedUntil)&&TryClaimPursuit()){patrolWalking=false;nextPath=0;State=Behaviour.Chasing;ScheduleAttack();}
            else {Patrol();return;}
        }
        if(State!=Behaviour.Returning&&ShouldReturn(distance))LoseTarget();
        if(State==Behaviour.Returning&&seen&&distance<=lockRange+2f&&TryClaimPursuit())
        {State=Behaviour.Chasing;nextPath=0;ScheduleAttack();}
        if(State==Behaviour.Returning)
        {
            if(PlanarDistance(home,transform.position)<.3f){State=Behaviour.Idle;driver.SetAIInput(Vector3.zero,false,null);return;}
            driver.SetAIInput(Navigate(home),false,null);return;
        }
        if(!TryClaimPursuit()){State=Behaviour.Idle;driver.SetAIInput(Vector3.zero,false,null);combo.SetAICombatReady(false);return;}
        if(driver.IsDodging)
        {
            State=Behaviour.Evading;driver.SetAIInput(Vector3.zero,false,target.transform);return;
        }
        if(sequenceActive)
        {
            driver.SetAIInput(Vector3.zero,false,target.transform);combo.SetAICombatReady(true);State=Behaviour.Attacking;
            int count=combo.AttackCount-attackBase;
            if(secondPlanned&&!secondQueued&&count==1&&combo.IsAttacking&&combo.SourceProgress>=Mathf.Min(.90f,combo.ActiveStrike.recoveryCancel)-.08f
                &&distance<=attackRange+.15f&&seen&&(reaction==null||!reaction.IsReacting))
                secondQueued=combo.RequestAIAttack(secondIndex);
            if(!combo.IsAttacking){LastSequenceLength=count;sequenceActive=false;State=Behaviour.Locked;ScheduleAttack();}
            return;
        }
        if(State==Behaviour.Chasing)
        {
            combo.SetAICombatReady(false);
            if(distance<=lockRange&&seen){State=Behaviour.Locked;ScheduleAttack();}
            else {driver.SetAIInput(Separate(Navigate(lastKnown)),distance>2.6f,null);return;}
        }
        if(distance>lockRange+.8f||!seen){State=Behaviour.Chasing;nextPath=0;driver.SetAIInput(Vector3.zero,false,null);return;}
        combo.SetAICombatReady(true);
        if(TryEvade(distance))return;
        Vector3 toward=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized;
        if(Time.time<NextAttackTime||!HasAttackSlot()||(reaction!=null&&reaction.IsReacting))
        {
            State=Behaviour.Locked;
            if(Time.time>=nextOrbit){orbitSign=-orbitSign;nextOrbit=Time.time+Random.Range(1.8f,3.4f);}
            float radius=Mathf.Clamp(orbitDistance,attackRange,Mathf.Max(attackRange,lockRange-.5f));
            float radial=Mathf.Clamp((distance-radius)*.35f,-.45f,.45f);
            Vector3 side=Vector3.Cross(Vector3.up,toward)*orbitMovement;
            Vector3 move=KeepOnNavigation(Separate(toward*radial+side*orbitSign));
            if(move.sqrMagnitude<.04f)
            {
                orbitSign=-orbitSign;nextOrbit=Time.time+Random.Range(1.8f,3.4f);
                move=KeepOnNavigation(Separate(toward*radial+side*orbitSign));
            }
            driver.SetAIInput(move,false,target.transform);return;
        }
        // A sprinting approach can open with the authored running strike before stopping.
        bool runningOpening=attackIndexOverride<0&&combo.AIWeaponSlot!=2&&driver.IsRunning&&distance>attackRange+.25f&&distance<=2.8f
            &&Vector3.Dot(Vector3.ProjectOnPlane((driver.visualRoot!=null?driver.visualRoot:transform).forward,Vector3.up).normalized,toward)>.65f
            &&HasLineOfSight(driver,target);
        if(distance>attackRange&&!runningOpening)
        {
            if(State!=Behaviour.Approaching)nextPath=0;
            State=Behaviour.Approaching;
            Vector3 destination=target.transform.position-toward*Mathf.Min(preferredDistance,attackRange*.85f);
            driver.SetAIInput(KeepOnNavigation(Separate(Navigate(destination,.08f))),distance>attackRange+.45f,target.transform);return;
        }
        if(combo.AIAttackCount==0)return;
        int opening=attackIndexOverride>=0?Mathf.Clamp(attackIndexOverride,0,combo.AIAttackCount-1):Random.Range(0,combo.AIAttackCount);
        attackBase=combo.AttackCount;
        if(combo.RequestAIAttack(opening,runningOpening))
        {
            LastOpeningStage=runningOpening?0:opening+1;SequencesStarted++;sequenceActive=true;secondQueued=false;
            secondPlanned=attackIndexOverride<0&&Random.value<secondAttackChance;
            secondIndex=(opening+1)%combo.AIAttackCount;
            driver.SetAIInput(Vector3.zero,false,target.transform);State=Behaviour.Attacking;
        }
    }
    void Patrol()
    {
        pursuing=false;combo.SetAICombatReady(false);
        if(patrolWalking&&(PlanarDistance(transform.position,patrolDestination)<.35f||Time.time>=patrolDeadline))
        {
            patrolWalking=false;nextPatrol=Time.time+Random.Range(Mathf.Max(.1f,patrolPause.x),Mathf.Max(patrolPause.x,patrolPause.y));
        }
        if(!patrolWalking&&Time.time>=nextPatrol)
        {
            patrolWalking=TryChoosePatrolPoint(out patrolDestination);
            nextPath=0;patrolDeadline=Time.time+12f;nextPatrol=Time.time+2f;
        }
        State=patrolWalking?Behaviour.Patrolling:Behaviour.Idle;
        driver.SetAIInput(patrolWalking?KeepOnNavigation(Separate(Navigate(patrolDestination))):Vector3.zero,false,null);
    }
    bool TryChoosePatrolPoint(out Vector3 destination)
    {
        destination=home;
        if(!NavMesh.SamplePosition(transform.position,out var from,1.2f,NavMesh.AllAreas))return false;
        var candidatePath=new NavMeshPath();
        for(int i=0;i<12;i++)
        {
            Vector2 offset=Random.insideUnitCircle*patrolRadius;
            Vector3 candidate=home+new Vector3(offset.x,0,offset.y);
            if(!NavMesh.SamplePosition(candidate,out var hit,1.5f,NavMesh.AllAreas)
                ||Mathf.Abs(hit.position.y-home.y)>2f||PlanarDistance(hit.position,home)>patrolRadius
                ||PlanarDistance(hit.position,transform.position)<1.2f)continue;
            if(NavMesh.CalculatePath(from.position,hit.position,NavMesh.AllAreas,candidatePath)&&candidatePath.status==NavMeshPathStatus.PathComplete)
            {destination=hit.position;return true;}
        }
        return false;
    }
    void ScheduleAttack(){NextAttackTime=Time.time+Random.Range(Mathf.Max(.1f,attackInterval.x),Mathf.Max(attackInterval.x,attackInterval.y));}
    bool TryEvade(float distance)
    {
        if(targetCombo==null||!targetCombo.IsAttacking||Time.time<nextDodge||!seen||combo.IsAttacking||(reaction!=null&&reaction.IsReacting))return false;
        float reach=targetCombo.IsSwordAttack?3.4f:2.4f;
        Vector3 toward=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized;
        if(distance>reach||Vector3.Dot(targetCombo.AttackForward,-toward)<.15f)return false;
        if(consideredAttack!=targetCombo.AttackCount)
        {
            consideredAttack=targetCombo.AttackCount;
            pendingDodge=Random.value<dodgeChance?Time.time+Random.Range(.12f,.24f):float.PositiveInfinity;
        }
        if(Time.time<pendingDodge)return false;
        pendingDodge=float.PositiveInfinity;
        if(targetCombo.SourceProgress>targetCombo.ActiveStrike.ContactProgress+.08f)return false;
        Vector3 side=Vector3.Cross(Vector3.up,toward)*(Random.value<.5f?-1:1);
        Vector3 direction=Random.value<.3f?-toward:(side-toward*.25f).normalized;
        if(!NavMesh.SamplePosition(transform.position,out var from,1,NavMesh.AllAreas))return false;
        if(NavMesh.Raycast(from.position,from.position+direction*1.8f,out _,NavMesh.AllAreas))
        {
            direction=(-side-toward*.25f).normalized;
            if(NavMesh.Raycast(from.position,from.position+direction*1.8f,out _,NavMesh.AllAreas))return false;
        }
        driver.SetAIInput(direction,false,target.transform);
        if(!driver.RequestAIDodge(direction))return false;
        State=Behaviour.Evading;nextPath=0;
        nextDodge=Time.time+Random.Range(dodgeCooldown.x,Mathf.Max(dodgeCooldown.x,dodgeCooldown.y));
        NextAttackTime=Mathf.Max(NextAttackTime,Time.time+.7f);
        return true;
    }
    void LoseTarget()
    {
        pursuing=false;
        if(State==Behaviour.Returning)return;
        combo.CancelForControlChange();combo.SetAICombatReady(false);sequenceActive=false;
        patrolWalking=false;nextPatrol=Time.time+2f;State=Behaviour.Returning;nextPath=0;driver.SetAIInput(Vector3.zero,false,null);
    }
    Vector3 Navigate(Vector3 destination,float arrivalDistance=.25f)
    {
        if(!useNavMesh)
        {
            var direction=Vector3.ProjectOnPlane(destination-transform.position,Vector3.up);
            return direction.magnitude>arrivalDistance?direction.normalized:Vector3.zero;
        }
        if(Time.time>=nextPath)
        {
            nextPath=Time.time+.25f;corner=1;
            if(path==null)path=new NavMeshPath();
            if(NavMesh.SamplePosition(transform.position,out var from,1.2f,NavMesh.AllAreas)&&NavMesh.SamplePosition(destination,out var to,1.2f,NavMesh.AllAreas)
                &&NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path))corners=path.corners;
            else corners=new Vector3[0];
        }
        while(corner<corners.Length&&PlanarDistance(transform.position,corners[corner])<arrivalDistance)corner++;
        return corner<corners.Length?Vector3.ProjectOnPlane(corners[corner]-transform.position,Vector3.up).normalized:Vector3.zero;
    }
    Vector3 Separate(Vector3 movement)
    {
        foreach(var other in selection.characters)
        {
            if(other==null||other==driver||other==target)continue;
            var away=Vector3.ProjectOnPlane(transform.position-other.transform.position,Vector3.up);
            if(away.sqrMagnitude>.001f&&away.sqrMagnitude<.64f)movement+=away.normalized*(.8f-away.magnitude);
        }
        return Vector3.ClampMagnitude(movement,1);
    }
    Vector3 KeepOnNavigation(Vector3 movement)
    {
        if(!useNavMesh)return movement;
        if(!NavMesh.SamplePosition(transform.position,out var from,1,NavMesh.AllAreas))return Vector3.zero;
        if(!NavMesh.Raycast(from.position,from.position+movement*.6f,out var hit,NavMesh.AllAreas))return movement;
        // Slide along an obstacle instead of standing still when an orbit meets its edge.
        movement=Vector3.ProjectOnPlane(movement,hit.normal);
        return NavMesh.Raycast(from.position,from.position+movement*.6f,out _,NavMesh.AllAreas)?Vector3.zero:movement;
    }
    static float PlanarDistance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    public static bool HasLineOfSight(CharacterMotor from,CharacterMotor to)
    {
        Vector3 start=from.transform.position+Vector3.up,end=to.transform.position+Vector3.up,direction=end-start;
        if(direction.sqrMagnitude<.001f)return true;
        int count=Physics.RaycastNonAlloc(start,direction.normalized,sightHits,direction.magnitude,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)if(sightHits[i].collider.GetComponentInParent<CharacterMotor>()==null)return false;
        return true;
    }
    void OnDisable()
    {
        pursuing=false;
        if(driver==null||driver.acceptPlayerInput)return;
        driver.ClearAIInput();driver.combatLookTarget=null;combo.CancelForControlChange();
    }
}
