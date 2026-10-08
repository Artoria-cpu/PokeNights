using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CharacterComboAttack))]
public sealed class OrbDashAttack : MonoBehaviour
{
    public const float RiseEnd = .20f, FlightEnd = .78f;
    [Min(0)] public float distance = 7f, lift = .65f;
    [Range(0,60)] public float maximumSteeringAngle = 22f;
    [Min(0)] public float steeringSpeed = 65f;
    [Min(.1f)] public float contactRadius = .65f;
    public int ContactCount { get; private set; }
    public float DistanceMoved { get; private set; }
    public bool IsDashing => combo != null && combo.IsOrbAttack && combo.IsRunningAttack && !motionDone;
    CharacterComboAttack combo;
    OrbDashDissolve effect;
    Vector3 initialForward, heading;
    float previousTravel, previousLift;
    bool motionDone;
    readonly HashSet<int> hits = new HashSet<int>();

    void OnEnable()
    {
        combo = GetComponent<CharacterComboAttack>();effect = GetComponent<OrbDashDissolve>();
        combo.AttackStarted += Begin;
    }
    void Begin(int stage,float time,bool cancelledRecovery)
    {
        if(!combo.IsOrbAttack||!combo.IsRunningAttack)return;
        motionDone=false;hits.Clear();ContactCount=0;DistanceMoved=0;previousTravel=previousLift=0;
        initialForward=heading=combo.AttackForward;
        if(effect!=null)effect.Begin();
    }
    public bool TickMotion(float dt,Vector3 inputDirection)
    {
        if(!IsDashing)return false;
        var driver=combo.driver;
        if(!driver.acceptPlayerInput||driver.IsActionBlocked){EndMotion();return false;}
        float progress=Mathf.Clamp01(combo.SourceProgress);
        if(progress>=FlightEnd){EndMotion();return false;}
        if(inputDirection.sqrMagnitude>.01f)
        {
            float yaw=Mathf.Clamp(Vector3.SignedAngle(initialForward,inputDirection,Vector3.up),-maximumSteeringAngle,maximumSteeringAngle);
            var target=Quaternion.AngleAxis(yaw,Vector3.up)*initialForward;
            heading=Vector3.RotateTowards(heading,target,steeringSpeed*Mathf.Deg2Rad*dt,0);
        }
        var body=driver.visualRoot!=null?driver.visualRoot:transform;
        body.rotation=Quaternion.LookRotation(heading,Vector3.up);
        float travel=distance*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,FlightEnd,progress));
        float height=lift*Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/RiseEnd));
        var before=transform.position;
        var moved=driver.ApplyOrbDashDisplacement(heading*Mathf.Max(0,travel-previousTravel)+Vector3.up*(height-previousLift),dt);
        previousTravel=travel;previousLift=height;DistanceMoved+=Vector3.ProjectOnPlane(moved,Vector3.up).magnitude;
        if(progress>=.10f)Sweep(before,transform.position);
        return true;
    }
    bool Ignore(Collider c)=>c==null||c.transform.IsChildOf(transform);
    static UnityEngine.Object Victim(Collider c)
    {
        var reaction=c.GetComponentInParent<CharacterHitReaction>();
        if(reaction!=null)return reaction;
        return c.GetComponentInParent<CombatDummy>();
    }
    void Sweep(Vector3 from,Vector3 to)
    {
        // Cover the full torso and the swept interval, including the first overlapping frame.
        var low=from+Vector3.up*.55f;var high=from+Vector3.up*1.45f;
        var delta=to-from;
        var contacts=delta.sqrMagnitude>.000001f?
            Physics.CapsuleCastAll(low,high,contactRadius,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore):Array.Empty<RaycastHit>();
        Array.Sort(contacts,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var contact in contacts)
        {
            if(Ignore(contact.collider))continue;
            if(Victim(contact.collider)==null)continue;
            Hit(contact.collider,from+delta.normalized*contact.distance);
        }
        foreach(var contact in Physics.OverlapCapsule(to+Vector3.up*.55f,to+Vector3.up*1.45f,contactRadius,~0,QueryTriggerInteraction.Ignore))
            if(!Ignore(contact)&&Victim(contact)!=null)Hit(contact,to);
    }
    void Hit(Collider contact,Vector3 origin)
    {
        var victim=Victim(contact);if(victim==null||hits.Contains(victim.GetInstanceID()))return;
        Vector3 chest=origin+Vector3.up;
        var point=contact.ClosestPoint(chest);var delta=point-chest;
        foreach(var obstruction in Physics.RaycastAll(chest,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(!Ignore(obstruction.collider)&&obstruction.collider!=contact&&Victim(obstruction.collider)==null)return;
        hits.Add(victim.GetInstanceID()); // Invulnerability consumes this dash's contact as well.
        if(victim is CharacterHitReaction reaction){if(!reaction.ReceiveHit(origin-heading,attacker:combo.driver,special:true,power:.35f,attackType:PlayerProgression.AttackType(combo.driver,2),weaponSlot:2,damageMultiplier:3f))return;}
        else if(victim is CombatDummy dummy)dummy.RegisterHit();
        ContactCount++;
        CombatHitFeedback.Play(point,combo.driver.movementCamera,false,heading,contact,false,false);
    }
    public void EndMotion()
    {
        motionDone=true;
        if(effect!=null)effect.Release();
    }
    void LateUpdate()
    {
        if(combo==null||!combo.IsOrbAttack||!combo.IsRunningAttack||combo.driver.IsActionBlocked)EndMotion();
    }
    void OnDisable()
    {
        if(combo!=null)combo.AttackStarted-=Begin;
        EndMotion();
    }
}
