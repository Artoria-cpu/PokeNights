using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(8000)]
public sealed class CharacterIdlePose : MonoBehaviour
{
    public Animator animator;
    public CharacterMotor driver;
    public float forwardLean = 8f;
    public float handClearance = .265f;
    private Transform spine, chest;
    private Transform[] upper = new Transform[2], lower = new Transform[2], hand = new Transform[2];
    private float blend;
    private CharacterHitReaction hitReaction;
    void OnEnable()
    {
        if(animator==null)animator=GetComponentInChildren<Animator>();
        if(driver==null)driver=GetComponent<CharacterMotor>();
        hitReaction=GetComponent<CharacterHitReaction>();
        if(animator==null||!animator.isHuman)return;
        spine=animator.GetBoneTransform(HumanBodyBones.Spine);chest=animator.GetBoneTransform(HumanBodyBones.Chest);
        upper[0]=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);upper[1]=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        lower[0]=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);lower[1]=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand[0]=animator.GetBoneTransform(HumanBodyBones.LeftHand);hand[1]=animator.GetBoneTransform(HumanBodyBones.RightHand);
    }
    void LateUpdate()
    {
        if(animator==null||driver==null||spine==null)return;
        if(driver.IsDodging||driver.IsBallThrowing){blend=0;return;}
        var state=animator.GetCurrentAnimatorStateInfo(0);var next=animator.GetNextAnimatorStateInfo(0);
        bool sad=state.IsName("SadIdle") || (animator.IsInTransition(0)&&next.IsName("SadIdle"));
        bool eligible=(sad||state.IsName("Locomotion"))&&driver.IsGrounded&&!driver.JumpPreparing&&!driver.RunStopping;
        float desired=eligible?1f-Mathf.InverseLerp(.03f,.4f,driver.PlanarSpeed):0;
        blend=Mathf.MoveTowards(blend,desired,Time.deltaTime*8f);
        float hands=hitReaction!=null?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(hitReaction.Weight/.65f)):1;
        ApplyPose(blend,sad?0f:blend,blend*hands);
    }
    public void ApplyPose(float amount,float upright,float handAmount=-1)
    {
        if(spine==null)OnEnable();if(spine==null||amount<=0)return;
        var model=animator.transform;
        spine.rotation=Quaternion.AngleAxis(forwardLean*.7f*upright,model.right)*spine.rotation;
        chest.rotation=Quaternion.AngleAxis(forwardLean*.3f*upright,model.right)*chest.rotation;
        var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var hp=model.InverseTransformPoint(hips.position);
        if(handAmount<0)handAmount=amount;
        if(handAmount<=0)return;
        for(int i=0;i<2;i++)
        {
            float side=i==0?-1f:1f;var local=model.InverseTransformPoint(hand[i].position);
            float sway=Mathf.Sin(Time.time*2.1f+i*.9f)*.007f;
            local.x=hp.x+side*Mathf.Max(side*(local.x-hp.x)+.045f,handClearance);
            local.z=Mathf.Max(local.z+.02f+sway,hp.z-.005f);
            Vector3 target=Vector3.Lerp(hand[i].position,model.TransformPoint(local),handAmount);
            Solve(upper[i],lower[i],hand[i],target,model.forward);
        }
    }
    static void Solve(Transform a,Transform b,Transform c,Vector3 target,Vector3 fallback)
    {
        Vector3 origin=a.position,ab=b.position-origin,bc=c.position-b.position;
        float l1=ab.magnitude,l2=bc.magnitude;if(l1<.001f||l2<.001f)return;
        Vector3 axis=target-origin;float distance=Mathf.Clamp(axis.magnitude,Mathf.Abs(l1-l2)+.001f,(l1+l2)*.995f);axis.Normalize();target=origin+axis*distance;
        Vector3 pole=Vector3.ProjectOnPlane(ab,axis).normalized;if(pole.sqrMagnitude<.1f)pole=Vector3.ProjectOnPlane(-fallback,axis).normalized;
        float along=(l1*l1-l2*l2+distance*distance)/(2f*distance);float height=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        Vector3 elbow=origin+axis*along+pole*height;Quaternion wrist=c.rotation;
        a.rotation=Quaternion.FromToRotation(ab,elbow-origin)*a.rotation;
        b.rotation=Quaternion.FromToRotation(c.position-b.position,target-b.position)*b.rotation;c.rotation=wrist;
    }
}
