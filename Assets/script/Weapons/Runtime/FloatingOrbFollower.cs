using UnityEngine;

/// <summary>One party-owned orb floats beside the active character, with soft positional lag.</summary>
[DisallowMultipleComponent,DefaultExecutionOrder(10500)]
public sealed class FloatingOrbFollower : MonoBehaviour
{
    public CharacterWeaponEquipment equipment;
    public Vector3 stowedOffset=new Vector3(-.85f,.18f,-.6f);
    public Vector3 equippedOffset=new Vector3(.65f,.27f,.08f);
    public float followSeconds=.22f;
    Vector3 velocity;
    CharacterMotor previousActor;
    Renderer[] visuals;
    void Awake(){visuals=GetComponentsInChildren<Renderer>(true);}
    void LateUpdate()
    {
        var actor=equipment!=null&&equipment.selection!=null?equipment.selection.ActiveCharacter:null;
        bool visible=actor!=null&&!actor.IsBallThrowing&&equipment.HasOrb&&equipment.isActiveAndEnabled;
        foreach(var renderer in visuals)if(renderer!=null)renderer.enabled=visible;
        if(!visible||Time.deltaTime<=0)return;
        var body=actor.visualRoot!=null?actor.visualRoot:actor.transform;
        var chest=actor.animator!=null&&actor.animator.isHuman?actor.animator.GetBoneTransform(HumanBodyBones.Chest):null;
        var anchor=chest!=null?chest.position:actor.transform.position+Vector3.up*1.25f;
        var rotation=Quaternion.Euler(0,body.eulerAngles.y,0);
        var offset=equipment.SelectedSlot==2?equippedOffset:stowedOffset;
        offset+=new Vector3(Mathf.Sin(Time.time*1.25f)*.035f,Mathf.Sin(Time.time*1.8f)*.045f,Mathf.Cos(Time.time*1.25f)*.025f);
        var target=anchor+rotation*offset;
        // Party switches and teleports should not drag the orb across the whole arena.
        if(previousActor!=actor||(target-transform.position).sqrMagnitude>25)
        {transform.position=target;velocity=Vector3.zero;transform.rotation=rotation;previousActor=actor;}
        else transform.position=Vector3.SmoothDamp(transform.position,target,ref velocity,Mathf.Max(.05f,followSeconds),Mathf.Infinity,Time.deltaTime);
        transform.rotation=Quaternion.Slerp(transform.rotation,rotation,1-Mathf.Exp(-5*Time.deltaTime));
    }
}
