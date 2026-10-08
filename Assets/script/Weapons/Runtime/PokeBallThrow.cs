using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Upper-body throw, sampled explicitly so aiming can hold and cancellation can reverse.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor)), DefaultExecutionOrder(-80)]
public sealed class PokeBallThrow : MonoBehaviour
{
    public const string LayerName="PokeBallUpper", StateName="PokeBallUpper.Throw", TimeParameter="PokeBallTime";
    public enum ThrowPhase { None, Raising, Aiming, Throwing, Reversing, Fading }
    public GameObject ballPrefab;
    public AnimationClip throwClip;
    public Material trajectoryMaterial;
    public Vector3 handOffset;
    public Vector3 handEuler;
    [Min(0)] public float aimTime=.4f, releaseTime=.5666667f;
    [Min(.1f)] public float animationSpeed=1.25f, throwSpeed=20f;
    [Min(1)] public float aimSpeedMultiplier=1.8f, recoverySpeedMultiplier=2f;
    public float upwardBoost=2f;
    public ThrowPhase Phase {get;private set;}
    public bool IsActive=>Phase!=ThrowPhase.None;
    public float PoseWeight=>weight;
    public int ThrowCount {get;private set;}
    public Vector3 PredictedLanding {get;private set;}
    public bool HasPredictedLanding {get;private set;}
    CharacterMotor driver;
    CharacterComboAttack combo;
    Transform hand;
    GameObject heldBall;
    LineRenderer arc,landing;
    readonly Vector3[] points=new Vector3[251],ring=new Vector3[49];
    int layer=-1;
    float clipTime,weight;
    bool released,queuedThrow,cancelAfterThrow;
    Vector3 committedVelocity,previewCenter,committedCenter;
    float previewFlightTime,committedFlightTime;
    bool previewValid,committedTarget;
    void Awake(){driver=GetComponent<CharacterMotor>();combo=GetComponent<CharacterComboAttack>();}
    void Update()
    {
        if(IsActive&&(driver==null||!driver.acceptPlayerInput||driver.IsActionBlocked||CharacterCombatStats.Dead(driver)||!driver.isActiveAndEnabled))
        {Interrupt();return;}
        if(!IsActive)
        {
            if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame)BeginAim();
            return;
        }
        if(Time.deltaTime<=0)return;
        var mouse=Mouse.current;
        bool cancel=(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame)||(mouse!=null&&mouse.rightButton.wasPressedThisFrame);
        if(cancel)CancelAim();
        if(Phase==ThrowPhase.Raising||Phase==ThrowPhase.Aiming)
        {
            if(!cancel&&mouse!=null&&mouse.leftButton.wasPressedThisFrame)queuedThrow=true;
        }
        AdvanceAnimation(Time.deltaTime);
    }
    public void CancelAim()
    {
        queuedThrow=false;
        if(Phase==ThrowPhase.Throwing&&released){cancelAfterThrow=true;return;}
        if(Phase==ThrowPhase.Raising||Phase==ThrowPhase.Aiming||Phase==ThrowPhase.Throwing)Phase=ThrowPhase.Reversing;
    }
    void AdvanceAnimation(float deltaTime)
    {
        if(Phase==ThrowPhase.Raising)
        {
            clipTime=Mathf.MoveTowards(clipTime,aimTime,deltaTime*animationSpeed*aimSpeedMultiplier);
            if(clipTime>=aimTime)Phase=ThrowPhase.Aiming;
        }
        if(Phase==ThrowPhase.Aiming&&queuedThrow)
        {Phase=ThrowPhase.Throwing;queuedThrow=false;committedVelocity=AimVelocity();committedTarget=previewValid;committedCenter=previewCenter;committedFlightTime=previewFlightTime;}
        if(Phase==ThrowPhase.Throwing)clipTime=Mathf.Min(throwClip.length,clipTime+deltaTime*animationSpeed*(released?recoverySpeedMultiplier:1));
        if(Phase==ThrowPhase.Reversing)
        {
            clipTime=Mathf.MoveTowards(clipTime,0,deltaTime*animationSpeed*recoverySpeedMultiplier);
            if(clipTime<=0)Phase=ThrowPhase.Fading;
        }
        if(Phase==ThrowPhase.Throwing&&clipTime>=throwClip.length&&released)
        {
            // Reload at the recovered pose, keeping camera, walking and input ownership throughout.
            if(cancelAfterThrow||GameSession.Ensure().PokeBalls<=0)Phase=ThrowPhase.Fading;
            else
            {
                Phase=ThrowPhase.Raising;clipTime=0;
                released=queuedThrow=previewValid=committedTarget=false;
                heldBall.SetActive(true);
            }
        }
        weight=Mathf.MoveTowards(weight,Phase==ThrowPhase.Fading?0:1,deltaTime/ .08f);
        driver.animator.SetFloat(TimeParameter,clipTime/throwClip.length);
        driver.animator.SetLayerWeight(layer,weight);
        driver.ResetIdleTimer();
        if(Phase==ThrowPhase.Fading&&weight<=0)Interrupt();
    }
    public bool BeginAim()
    {
        if(GameSession.Ensure().PokeBalls<=0)return false;
        if(IsActive||driver==null||!driver.acceptPlayerInput||driver.IsActionBlocked||driver.IsDodging||driver.JumpPreparing||!driver.IsGrounded||driver.IsAttacking||driver.equipmentBusy
            ||ballPrefab==null||throwClip==null||driver.animator==null)return false;
        var sword=GetComponent<SwordThrowSkill>();if(sword!=null&&(sword.IsCasting||sword.IsPreparingCast))return false;
        layer=driver.animator.GetLayerIndex(LayerName);
        if(layer<0||!driver.animator.HasState(layer,Animator.StringToHash(StateName)))return false;
        hand=driver.animator.GetBoneTransform(HumanBodyBones.RightHand);if(hand==null)return false;
        combo?.CancelForControlChange();
        FindFirstObjectByType<CombatLockOn>()?.Unlock();
        driver.SetBallThrowActive(true);
        Phase=ThrowPhase.Raising;clipTime=weight=0;released=queuedThrow=previewValid=committedTarget=cancelAfterThrow=false;
        driver.animator.SetFloat(TimeParameter,0);driver.Animation.Play(CharacterAnimationDirector.Action.Overlay, StateName,layer,0);
        heldBall=Instantiate(ballPrefab,hand,false);heldBall.name="Held Poke Ball";
        heldBall.transform.localPosition=handOffset;heldBall.transform.localRotation=Quaternion.Euler(handEuler);
        EnsurePreview();return true;
    }
    Vector3 AimVelocity()
    {
        var camera=driver.movementCamera!=null?driver.movementCamera:Camera.main;
        return (camera!=null?camera.transform.forward:transform.forward)*throwSpeed+Vector3.up*upwardBoost;
    }
    // Called after pose blending and the final camera update by PokeBallThrowView.
    public void Present()
    {
        if(!IsActive||heldBall==null)return;
        Vector3 origin=PokeBallFlight.SafeOrigin(driver.transform,heldBall.transform.position);
        Vector3 velocity=Phase==ThrowPhase.Throwing?
            (committedTarget?PokeBallFlight.VelocityTo(origin,committedCenter,committedFlightTime):committedVelocity):AimVelocity();
        if(Phase==ThrowPhase.Throwing&&!released&&clipTime>=releaseTime&&Time.deltaTime>0)
        {
            if(!GameSession.Ensure().UsePokeBall()){Interrupt();return;}
            released=true;ThrowCount++;
            GameplayAudio.Play(GameplayCue.FistSwing,origin,.35f);
            PokeBallFlight.Launch(ballPrefab,origin,heldBall.transform.rotation,velocity,driver.transform);
            heldBall.SetActive(false);
        }
        bool preview=Phase==ThrowPhase.Raising||Phase==ThrowPhase.Aiming||(Phase==ThrowPhase.Throwing&&!released);
        arc.enabled=preview;landing.enabled=false;HasPredictedLanding=false;
        if(!preview)return;
        int count=PokeBallFlight.Predict(origin,velocity,driver.transform,points,out var surface,out var normal,out bool hit);
        if(Phase!=ThrowPhase.Throwing){previewCenter=points[count-1];previewFlightTime=(count-1)*PokeBallFlight.Step;previewValid=true;}
        arc.positionCount=count;for(int i=0;i<count;i++)arc.SetPosition(i,points[i]);
        HasPredictedLanding=hit;PredictedLanding=surface;
        if(!hit)return;
        Vector3 tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.right:Vector3.up).normalized;
        Vector3 bitangent=Vector3.Cross(normal,tangent);
        for(int i=0;i<ring.Length;i++){float angle=i*2*Mathf.PI/(ring.Length-1);ring[i]=surface+normal*.012f+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*.22f;}
        landing.positionCount=ring.Length;landing.SetPositions(ring);landing.enabled=true;
    }
    void EnsurePreview()
    {
        if(arc==null)arc=Line("Poke Ball Trajectory",.022f);
        if(landing==null)landing=Line("Poke Ball Landing",.028f);
    }
    LineRenderer Line(string name,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=trajectoryMaterial;line.useWorldSpace=true;line.widthMultiplier=width;
        line.numCapVertices=3;line.numCornerVertices=2;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
    }
    public void Interrupt()
    {
        if(!IsActive)return;
        Phase=ThrowPhase.None;queuedThrow=false;
        if(driver!=null){driver.SetBallThrowActive(false);if(layer>=0&&driver.animator!=null)driver.animator.SetLayerWeight(layer,0);}
        if(heldBall!=null)Destroy(heldBall);heldBall=null;
        if(arc!=null)arc.enabled=false;if(landing!=null)landing.enabled=false;
        HasPredictedLanding=false;
    }
    void OnDisable(){Interrupt();}
}
