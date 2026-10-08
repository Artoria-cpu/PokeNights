using UnityEngine;

/// <summary>Preserves the outgoing skeletal pose when an action or its blend-tree inputs are interrupted.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(60)]
public sealed class CharacterPoseTransitions : MonoBehaviour
{
    public CharacterMotor driver;
    [Min(.01f)] public float actionBlend=.14f,recoveryBlend=.28f,dodgeBlend=.12f;
    static readonly HumanBodyBones[] Bones={
        HumanBodyBones.Hips,
        HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes,
        HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand
    };
    const int LowerBodyCount=9;
    readonly Transform[] bones=new Transform[Bones.Length];
    readonly Vector3[] previousPositions=new Vector3[Bones.Length],fromPositions=new Vector3[Bones.Length];
    readonly Quaternion[] previousRotations=new Quaternion[Bones.Length],fromRotations=new Quaternion[Bones.Length];
    readonly Vector3[] heldPositions=new Vector3[LowerBodyCount];
    readonly Quaternion[] heldRotations=new Quaternion[LowerBodyCount];
    Animator animator;
    bool hasPose,hasState,blending,holding;
    int destination,lastBlendFrame=-1;
    float blendElapsed,blendDuration;

    void OnEnable()
    {
        if(driver==null)driver=GetComponent<CharacterMotor>();
        animator=driver.animator;
        hasPose=hasState=blending=holding=false;
        if(animator==null||!animator.isHuman)return;
        for(int i=0;i<bones.Length;i++)bones[i]=animator.GetBoneTransform(Bones[i]);
    }
    void CaptureIfNeeded()
    {
        if(hasPose)return;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null)
        {previousPositions[i]=bones[i].localPosition;previousRotations[i]=bones[i].localRotation;}
        hasPose=true;
    }
    public void HoldHitPose()
    {
        CaptureIfNeeded();
        for(int i=0;i<LowerBodyCount;i++)
        {heldPositions[i]=previousPositions[i];heldRotations[i]=previousRotations[i];}
        holding=true;
    }
    public void ReleaseHitPose()
    {
        if(!holding)return;
        holding=false;BeginBlend(recoveryBlend);
    }
    void BeginBlend(float seconds)
    {
        CaptureIfNeeded();
        System.Array.Copy(previousPositions,fromPositions,bones.Length);
        System.Array.Copy(previousRotations,fromRotations,bones.Length);
        blendElapsed=0;blendDuration=Mathf.Max(.01f,seconds);lastBlendFrame=Time.frameCount;blending=true;
    }
    float DurationFor(AnimatorStateInfo state)
    {
        if(state.IsName("Locomotion")||state.IsName("FightIdle")||state.IsName("Land")||state.IsName("RunningLand")
            ||state.IsName(EnemyPoise.FallState)||state.IsName(EnemyPoise.GetUpState))return recoveryBlend;
        if(driver.IsDodging)return dodgeBlend;
        return actionBlend;
    }
    void LateUpdate()
    {
        // A parry animates in real time while the rest of the world is stopped.
        // Its skeletal blend must use the same clock or it pins the animated bones to the old pose.
        EvaluatePose(animator!=null&&animator.updateMode==AnimatorUpdateMode.UnscaledTime
            ?Time.unscaledDeltaTime:Time.deltaTime);
    }
    void EvaluatePose(float deltaTime)
    {
        if(animator==null||!animator.isActiveAndEnabled||!animator.isHuman)return;
        if(animator.speed<=0&&hasPose)
        {
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].SetLocalPositionAndRotation(previousPositions[i],previousRotations[i]);
            return;
        }
        if(holding&&(!driver.IsHitStunned||driver.IsIncapacitated))ReleaseHitPose();
        var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
        if(hasState&&state.fullPathHash!=destination&&lastBlendFrame!=Time.frameCount)BeginBlend(DurationFor(state));
        destination=state.fullPathHash;hasState=true;
        if(blending)blendElapsed+=Mathf.Max(0,deltaTime);
        float t=blending?Mathf.Clamp01(blendElapsed/blendDuration):1;
        float weight=Mathf.SmoothStep(0,1,t);
        // Run before trails, hit queries, pose corrections and hair/cloth so they see the displayed skeleton.
        for(int i=0;i<bones.Length;i++)
        {
            var bone=bones[i];if(bone==null)continue;
            Vector3 position=bone.localPosition;Quaternion rotation=bone.localRotation;
            if(blending){position=Vector3.Lerp(fromPositions[i],position,weight);rotation=Quaternion.Slerp(fromRotations[i],rotation,weight);}
            if(holding&&i<LowerBodyCount){position=heldPositions[i];rotation=heldRotations[i];}
            bone.SetLocalPositionAndRotation(position,rotation);
            previousPositions[i]=position;previousRotations[i]=rotation;
        }
        hasPose=true;if(t>=1)blending=false;
    }
    void OnDisable(){hasPose=hasState=blending=holding=false;}
}
