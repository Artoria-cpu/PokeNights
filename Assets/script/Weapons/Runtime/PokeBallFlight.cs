using UnityEngine;
using System.Collections.Generic;

/// <summary>Swept prediction up to first impact; PhysX owns all subsequent bouncing and rolling.</summary>
public sealed class PokeBallFlight : MonoBehaviour
{
    public const float Radius=.065f, Step=.02f, Duration=5f;
    static readonly RaycastHit[] hits=new RaycastHit[64];
    static readonly Collider[] overlaps=new Collider[64];
    readonly HashSet<CharacterCombatStats> attempted=new HashSet<CharacterCombatStats>();
    Transform owner;
    Vector3 origin,velocity;
    float time,accumulator,age;
    Rigidbody body;
    PokeBallCapture capture;
    public static Vector3 Position(Vector3 origin,Vector3 velocity,float time)=>origin+velocity*time+Physics.gravity*(.5f*time*time);
    public static Vector3 VelocityTo(Vector3 from,Vector3 target,float seconds)
    {
        seconds=Mathf.Max(Step,seconds);
        return (target-from-Physics.gravity*(.5f*seconds*seconds))/seconds;
    }
    public static Vector3 SafeOrigin(Transform owner,Vector3 hand)
    {
        return owner!=null&&Contact(owner.position+Vector3.up*1.1f,hand,owner,out var center,out _,out _,out _)?center:hand;
    }
    public static void Launch(GameObject prefab,Vector3 position,Quaternion rotation,Vector3 velocity,Transform owner)
    {
        var ball=Instantiate(prefab,position,rotation);ball.name="Thrown Poke Ball";
        var flight=ball.AddComponent<PokeBallFlight>();flight.origin=position;flight.velocity=velocity;flight.owner=owner;
        flight.capture=ball.GetComponent<PokeBallCapture>();
    }
    void Update()
    {
        if(capture!=null&&capture.IsBusy)return;
        age+=Time.deltaTime;
        if(age>=20&&(capture==null||capture.CapturedEnemy==null)){Destroy(gameObject);return;}
        if(body!=null||Time.deltaTime<=0)return;
        accumulator+=Time.deltaTime;
        while(accumulator>=Step)
        {
            accumulator-=Step;float next=time+Step;
            Vector3 from=Position(origin,velocity,time),to=Position(origin,velocity,next);
            if(Contact(from,to,owner,out var center,out _,out var normal,out var collider))
            {
                transform.position=center+normal*.003f;
                if(TryCapture(collider))return;
                Vector3 incoming=velocity+Physics.gravity*next;
                EnablePhysics(Vector3.Reflect(incoming,normal)*.55f);return;
            }
            time=next;transform.position=to;transform.Rotate(Vector3.right,720*Step,Space.Self);
            if(time>=Duration){EnablePhysics(velocity+Physics.gravity*time);return;}
        }
    }
    bool TryCapture(Collider collider)
    {
        var enemy=collider!=null?collider.GetComponentInParent<CharacterCombatStats>():null;
        if(capture==null||enemy==null||!enemy.CanCapture||!attempted.Add(enemy))return false;
        return capture.TryCapture(enemy);
    }
    void OnCollisionEnter(Collision collision){if(capture==null||!capture.IsBusy)TryCapture(collision.collider);}
    public void EnablePhysics(Vector3 initialVelocity)
    {
        if(capture==null)capture=GetComponent<PokeBallCapture>();
        if(body==null)
        {
            var sphere=gameObject.AddComponent<SphereCollider>();sphere.radius=Radius;sphere.contactOffset=.001f;
            sphere.sharedMaterial=capture!=null?capture.bounceMaterial:null;
            body=gameObject.AddComponent<Rigidbody>();body.mass=.15f;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearDamping=.08f;body.angularDamping=.35f;
            if(owner!=null)foreach(var c in owner.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(sphere,c);
        }
        body.isKinematic=false;body.detectCollisions=true;body.linearVelocity=initialVelocity;
        body.angularVelocity=new Vector3(5,2,3);body.WakeUp();
    }
    public static int Predict(Vector3 origin,Vector3 velocity,Transform owner,Vector3[] points,out Vector3 surface,out Vector3 normal,out bool hit)
    {
        points[0]=origin;surface=origin;normal=Vector3.up;hit=false;
        for(int i=1;i<points.Length;i++)
        {
            Vector3 next=Position(origin,velocity,i*Step);
            if(Contact(points[i-1],next,owner,out var center,out surface,out normal,out _)){points[i]=center;hit=true;return i+1;}
            points[i]=next;
        }
        surface=points[points.Length-1];return points.Length;
    }
    static bool Ignored(Collider c,Transform owner)=>c==null||(owner!=null&&c.transform.IsChildOf(owner));
    static bool Contact(Vector3 from,Vector3 to,Transform owner,out Vector3 center,out Vector3 surface,out Vector3 normal,out Collider collider)
    {
        center=to;surface=to;normal=Vector3.up;collider=null;
        int count=Physics.OverlapSphereNonAlloc(from,Radius,overlaps,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            var c=overlaps[i];if(Ignored(c,owner))continue;
            surface=c.ClosestPoint(from);normal=(from-surface).sqrMagnitude>.00001f?(from-surface).normalized:Vector3.up;center=from;collider=c;return true;
        }
        var delta=to-from;float distance=delta.magnitude;if(distance<.00001f)return false;
        count=Physics.SphereCastNonAlloc(from,Radius,delta/distance,hits,distance,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;int chosen=-1;
        for(int i=0;i<count;i++)if(!Ignored(hits[i].collider,owner)&&hits[i].distance<nearest){nearest=hits[i].distance;chosen=i;}
        if(chosen<0)return false;
        collider=hits[chosen].collider;surface=hits[chosen].point;normal=hits[chosen].normal;center=from+delta/distance*nearest;return true;
    }
}
