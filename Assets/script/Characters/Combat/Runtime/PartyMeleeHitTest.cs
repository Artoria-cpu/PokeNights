using System.Collections.Generic;
using UnityEngine;

/// <summary>Unarmed contact pulses and sword ribbon hits against playground characters.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(9000)]
public sealed class PartyMeleeHitTest : MonoBehaviour
{
    public PlayerRoster selection;
    [Min(.1f)] public float unarmedReach=2.4f;
    [Range(10,180)] public float arcDegrees=155f;
    [Header("Running contact window")]
    [Min(.1f)] public float runningReach=2.8f,runningHalfWidth=1.25f;
    [Min(0)] public float runningEarlyFrames=3,runningLateFrames=5;
    public int ContactCount { get; private set; }
    public int HitCount { get; private set; }
    sealed class ActorContacts
    {
        public CharacterMotor driver;
        public CharacterComboAttack combo;
        public CharacterComboAttack.Strike strike;
        public int attackId=-1,nextContact;
        public float previousFrame;
        public Vector3 previousPosition;
        public readonly HashSet<int> runningHits=new HashSet<int>();
    }
    ActorContacts[] actors;
    CharacterHitReaction[] targets;

    void OnEnable()=>RefreshRoster();
    public void RefreshRoster()
    {
        if(selection==null)selection=GetComponent<PlayerRoster>();
        if(selection==null||selection.characters==null)return;
        targets=new CharacterHitReaction[selection.characters.Length];
        actors=new ActorContacts[selection.characters.Length];
        for(int i=0;i<actors.Length;i++)
        {
            var actor=selection.characters[i];
            targets[i]=actor!=null?actor.GetComponent<CharacterHitReaction>():null;
            actors[i]=new ActorContacts{driver=actor,combo=actor!=null?actor.GetComponent<CharacterComboAttack>():null};
        }
    }
    void LateUpdate()
    {
        if(actors==null)return;
        foreach(var tracked in actors)
        {
            var actor=tracked.driver;var combo=tracked.combo;
            if(actor==null||combo==null)continue;
            if(tracked.attackId!=combo.AttackCount){tracked.attackId=combo.AttackCount;tracked.strike=combo.IsAttacking?combo.ActiveStrike:null;tracked.nextContact=0;tracked.previousFrame=0;tracked.previousPosition=actor.transform.position;tracked.runningHits.Clear();}
            var strike=tracked.strike;
            if(combo.IsOrbAttack)continue;
            if(!combo.IsAttacking){tracked.strike=null;continue;}
            if(strike==null||strike.clip==null||actor.IsDodging||(!actor.acceptPlayerInput&&!actor.AIControlled))continue;
            // Read the sampled pose for each actor; AI uses exactly the same authored hit markers.
            float progress=combo.SourceProgress;
            var animator=actor.animator;
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName(strike.stateName))progress=Mathf.Max(progress,state.normalizedTime);
            float frame=progress*strike.clip.length*strike.clip.frameRate;
            if(combo.IsRunningAttack&&!combo.IsSwordAttack&&frame>=strike.contactFrame-runningEarlyFrames&&tracked.previousFrame<=strike.contactFrame+runningLateFrames)
                ResolveContact(actor,combo,runningReach,tracked);
            while(tracked.nextContact<strike.ContactCount&&frame>=strike.ContactFrame(tracked.nextContact)){
                tracked.nextContact++;ContactCount++;if(!combo.IsSwordAttack&&!combo.IsRunningAttack)ResolveContact(actor,combo,unarmedReach);
            }
            tracked.previousFrame=frame;tracked.previousPosition=actor.transform.position;
        }
    }
    public bool ResolveSwordContact(CharacterMotor actor,Collider contact,Vector3 center,HashSet<int> hit,Vector3 origin)
    {
        if(!isActiveAndEnabled||selection==null||(!actor.acceptPlayerInput&&!actor.AIControlled)||targets==null)return false;
        var target=contact.GetComponentInParent<CharacterHitReaction>();
        if(target==null||System.Array.IndexOf(targets,target)<0)return false;
        if(target.driver==actor||!target.isActiveAndEnabled||hit.Contains(target.GetInstanceID()))return true;
        if(!actor.acceptPlayerInput&&(target.driver!=selection.ActiveCharacter||!PartyEnemyAI.HasLineOfSight(actor,target.driver)))return true;
        // An avoided slash is consumed too, so its lingering ribbon cannot hit as the dodge ends.
        hit.Add(target.GetInstanceID());
        if(target.ReceiveHit(origin,attacker:actor,attackType:PlayerProgression.AttackType(actor,1),weaponSlot:1,damageMultiplier:actor.GetComponent<CharacterComboAttack>().DamageMultiplier)){
            HitCount++;
            Vector3 approach=origin;
            approach.y=Mathf.Clamp(center.y,contact.bounds.min.y,contact.bounds.max.y);
            var combo=actor.GetComponent<CharacterComboAttack>();
            CombatHitFeedback.Play(contact.ClosestPoint(approach),actor.movementCamera,true,target.transform.position-origin,contact,combo!=null&&combo.IsFifthStrike);
        }
        return true;
    }
    void ResolveContact(CharacterMotor actor,CharacterComboAttack combo,float reach,ActorContacts running=null)
    {
        Vector3 facing=combo.AttackForward;
        float threshold=Mathf.Cos(arcDegrees*.5f*Mathf.Deg2Rad);
        foreach(var target in targets){
            if(target==null||target.driver==actor||!target.isActiveAndEnabled)continue;
            if(!actor.acceptPlayerInput&&(target.driver!=selection.ActiveCharacter||!PartyEnemyAI.HasLineOfSight(actor,target.driver)))continue;
            var origin=actor.transform.position;
            if(running!=null)
            {
                var travel=Vector3.ProjectOnPlane(origin-running.previousPosition,Vector3.up);
                float along=travel.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(target.transform.position-running.previousPosition,travel)/travel.sqrMagnitude):1;
                origin=running.previousPosition+travel*along;
            }
            var delta=target.transform.position-origin;
            var planar=Vector3.ProjectOnPlane(delta,Vector3.up);
            var capsule=target.GetComponent<CharacterController>();float radius=capsule!=null?capsule.radius:.25f;
            if(Mathf.Abs(delta.y)>1.15f)continue;
            if(running!=null)
            {
                float forward=Vector3.Dot(planar,combo.AttackForward);
                float side=(planar-combo.AttackForward*forward).magnitude;
                bool originalArc=planar.magnitude<=unarmedReach+radius&&(planar.sqrMagnitude<.001f||forward/planar.magnitude>=threshold);
                bool corridor=forward>=-radius-.03f&&forward<=reach+radius&&side<=runningHalfWidth+radius;
                if(!originalArc&&!corridor)continue;
                // Consume dodges too; one running strike cannot hit again after invulnerability ends.
                if(!running.runningHits.Add(target.GetInstanceID()))continue;
            }
            else
            {
                if(planar.magnitude>reach+radius)continue;
                if(planar.sqrMagnitude>.001f&&Vector3.Dot(facing,planar.normalized)<threshold)continue;
            }
            if(target.ReceiveHit(origin,attacker:actor,damageMultiplier:combo.DamageMultiplier)){
                HitCount++;
                Vector3 impact=origin+Vector3.up*1.05f;
                CombatHitFeedback.Play(capsule!=null?capsule.ClosestPoint(impact):target.transform.position+Vector3.up,actor.movementCamera,false,delta,capsule,combo.IsFifthStrike);
            }
        }
    }
}

