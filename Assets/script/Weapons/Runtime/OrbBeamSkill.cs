using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>E while the orb is selected: cast a finite, obstacle-clipped piercing magic beam.</summary>
[DisallowMultipleComponent,RequireComponent(typeof(CharacterMotor)),DefaultExecutionOrder(-25)]
public sealed class OrbBeamSkill : MonoBehaviour
{
    public const string State="OrbBeamCast";
    public const float DamageInterval=.3f;
    public const float RedBeamDuration=2f;
    public CharacterMotor driver;
    public CharacterWeaponEquipment equipment;
    public AnimationClip castClip;
    public Material coreMaterial,glowMaterial,ringMaterial,spriteMaterial,windMaterial,wrapMaterial;
    [Range(0,1)]public float releaseProgress=1.8f/10.58f;
    [Min(.1f)]public float playbackSpeed=1f,beamDuration=8f,beamRange=30f,cooldownSeconds=16f;
    [Min(1)]public float beamTurnSpeed=40f;
    [Min(.01f)]public float beamRadius=.16f,animationBlend=.08f;
    public bool IsCasting{get;private set;}
    public bool OwnsBody=>IsCasting;
    public Vector3 ActionForward{get;private set;}
    public float CooldownRemaining=>Mathf.Max(0,readyAt-Time.time);
    public void ReduceCooldown(float seconds)
    {
        if(!IsCasting)readyAt=Mathf.Max(Time.time,readyAt-Mathf.Max(0,seconds));
    }
    public int CastCount{get;private set;}
    public int HitCount{get;private set;}
    CharacterComboAttack combo;
    PlayerOrbitCameraInput orbitInput;
    Vector3 aimDirection;
    Vector3 recoilDirection;
    float recoilTravel;
    float started,readyAt,releasedAt,yawVelocity,unexpectedStateSince=-1,nextDamageAt;
    bool released;
    bool empowered,redRecovering;
    float redStarted;
    const float RedStarSeconds=OrbBeamEffect.StarGrowthSeconds;
    float ActiveBeamDuration=>empowered?RedBeamDuration:beamDuration;
    OrbBeamEffect effect;
    readonly HashSet<int> hit=new HashSet<int>();

    void Awake(){if(driver==null)driver=GetComponent<CharacterMotor>();combo=GetComponent<CharacterComboAttack>();if(equipment==null)equipment=FindAnyObjectByType<CharacterWeaponEquipment>();}
    bool IsOwner=>driver!=null&&driver.enabled&&driver.acceptPlayerInput&&equipment!=null&&equipment.selection!=null
        &&equipment.selection.ActiveCharacter==driver&&equipment.HasOrb&&equipment.SelectedSlot==2;
    void Update()
    {
        if(IsCasting)
        {
            if(!IsOwner||driver.IsActionBlocked||driver.animator==null||!driver.animator.isActiveAndEnabled){Cancel(true);return;}
            if(Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)RequestEmpower();
            TrackFacing();return;
        }
        if(Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)RequestCast();
    }
    public bool RequestCast()
    {
        if(IsCasting||!IsOwner||driver.BallInputBlocked||castClip==null||driver.animator==null||driver.IsActionBlocked||driver.IsDodging||driver.JumpPreparing
            ||!driver.IsGrounded||driver.equipmentBusy||combo!=null&&combo.IsAttacking||CooldownRemaining>0
            ||!driver.animator.HasState(0,Animator.StringToHash(State)))return false;
        combo?.CancelForControlChange();hit.Clear();released=empowered=redRecovering=false;unexpectedStateSince=-1;IsCasting=true;started=Time.time;readyAt=Time.time+cooldownSeconds;CastCount++;
        yawVelocity=0;aimDirection=BodyForward();ActionForward=aimDirection;Face(ActionForward,10000);
        equipment.selection.GetComponent<CombatLockOn>()?.Unlock();
        orbitInput=equipment.selection.followCamera!=null?equipment.selection.followCamera.GetComponent<PlayerOrbitCameraInput>():null;
        driver.equipmentBusy=true;driver.SetAttackActive(true);driver.animator.SetBool("IsAttacking",true);
        int weapon=driver.animator.GetLayerIndex("WeaponAction");if(weapon>=0)driver.animator.SetLayerWeight(weapon,0);
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, State,animationBlend,0,0);
        effect=OrbBeamEffect.BeginCharge(ChargeOrigin(0),aimDirection,GroundPoint(),coreMaterial,glowMaterial,ringMaterial,spriteMaterial,windMaterial,castClip.length*releaseProgress/playbackSpeed,wrapMaterial);
        GetComponent<CharacterActionAudio>()?.Magic(false);
        return true;
    }
    public bool RequestEmpower()
    {
        if(!IsCasting||!IsOwner||!released||empowered||driver.IsActionBlocked||Time.time-releasedAt>=5)return false;
        effect?.EndEmission(.55f);empowered=true;released=false;redRecovering=false;redStarted=Time.time;unexpectedStateSince=-1;
        hit.Clear();
        float releaseSeconds=castClip.length*releaseProgress/playbackSpeed;
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, State,.04f,0,(releaseSeconds-RedStarSeconds)*playbackSpeed);
        effect=OrbBeamEffect.BeginCharge(BeamOrigin(),aimDirection,GroundPoint(),coreMaterial,glowMaterial,ringMaterial,spriteMaterial,windMaterial,releaseSeconds,wrapMaterial,true);
        GetComponent<CharacterActionAudio>()?.Magic(false);
        effect.SetCharge(BeamOrigin(),aimDirection,OrbBeamEffect.StarStartProgress(releaseSeconds));
        return true;
    }
    void LateUpdate()
    {
        if(!IsCasting||Time.deltaTime<=0)return;
        float elapsed=Time.time-started;
        float castSeconds=castClip.length/Mathf.Max(.01f,playbackSpeed);
        float releaseSeconds=castSeconds*releaseProgress;
        var animator=driver.animator;var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
        if(!state.IsName(State))
        {
            if(unexpectedStateSince<0)unexpectedStateSince=Time.time;
            // A locomotion transition must not cancel an accepted cast. Rejoin its current pose.
            if(Time.time-unexpectedStateSince>animationBlend+.1f)
            {
                float poseSeconds=empowered?(released?releaseSeconds+Time.time-releasedAt:releaseSeconds-RedStarSeconds+Time.time-redStarted):elapsed;
                if(empowered&&redRecovering)poseSeconds=castSeconds-.78f+Time.time-releasedAt-RedBeamDuration;
                driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, State,animationBlend,0,Mathf.Min(poseSeconds*playbackSpeed,castClip.length-.01f));
                unexpectedStateSince=Time.time;
            }
        }
        else unexpectedStateSince=-1;
        if(!released)
        {
            float charge=empowered?Mathf.Lerp(OrbBeamEffect.StarStartProgress(releaseSeconds),1,Mathf.Clamp01((Time.time-redStarted)/RedStarSeconds)):Mathf.Clamp01(elapsed/Mathf.Max(.001f,releaseSeconds));
            effect?.SetCharge(ChargeOrigin(charge),aimDirection,charge);
            if(empowered?Time.time-redStarted>=RedStarSeconds:elapsed>=releaseSeconds)Release();
        }
        else if(Time.time-releasedAt<=ActiveBeamDuration)
        {
            float recoilTarget=1-Mathf.Pow(1-Mathf.Clamp01((Time.time-releasedAt)/.35f),3);
            driver.ApplyAttackDisplacement(recoilDirection*Mathf.Max(0,recoilTarget-recoilTravel));
            recoilTravel=recoilTarget;
            UpdateBeam();
        }
        if(empowered&&released&&!redRecovering&&Time.time-releasedAt>=RedBeamDuration)
        {
            redRecovering=true;driver.Animation.CrossFade(CharacterAnimationDirector.Action.Attack, State,.06f,0,castClip.length-.78f*playbackSpeed);
        }
        if(released&&(empowered?Time.time-releasedAt>=RedBeamDuration+.78f:elapsed>=castSeconds*.98f&&Time.time-releasedAt>=beamDuration+OrbBeamEffect.ThinSeconds))Finish(true,false);
    }
    void TrackFacing()
    {
        if(Time.deltaTime<=0)return;
        if(released&&Time.time-releasedAt<=ActiveBeamDuration&&driver.movementCamera!=null)
        {
            var cameraForward=Vector3.ProjectOnPlane(driver.movementCamera.transform.forward,Vector3.up);
            if(cameraForward.sqrMagnitude>.001f)
            {
                float yaw=Mathf.Atan2(aimDirection.x,aimDirection.z)*Mathf.Rad2Deg;
                // Read the player-controlled orbit, not the rendered camera's tracking corrections.
                float target=orbitInput!=null&&orbitInput.orbit!=null?orbitInput.orbit.HorizontalAxis.Value:Mathf.Atan2(cameraForward.x,cameraForward.z)*Mathf.Rad2Deg;
                yaw=Mathf.SmoothDampAngle(yaw,target,ref yawVelocity,empowered?.16f:.25f,beamTurnSpeed*(empowered?1.8f:1),Time.deltaTime);
                aimDirection=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            }
        }
        ActionForward=Planar(aimDirection);Face(ActionForward,10000);
    }
    static Vector3 Planar(Vector3 direction){var value=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;return value.sqrMagnitude>.001f?value:Vector3.forward;}
    void Face(Vector3 direction,float degrees)
    {
        if(direction.sqrMagnitude<.001f)return;var body=driver.visualRoot!=null?driver.visualRoot:transform;
        var planar=Vector3.ProjectOnPlane(direction,Vector3.up);if(planar.sqrMagnitude>.001f)body.rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(planar),degrees);
    }
    Vector3 BeamOrigin()
    {
        var animator=driver.animator;var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
        var direction=aimDirection.sqrMagnitude>.001f?aimDirection:ActionForward;
        if(left!=null&&right!=null)return(left.position+right.position)*.5f+direction*.13f;
        return transform.position+Vector3.up*1.2f+direction*.35f;
    }
    Vector3 ChargeOrigin(float progress)
    {
        var chest=driver.animator.GetBoneTransform(HumanBodyBones.Chest);
        var center=(chest!=null?chest.position:transform.position+Vector3.up*1.15f)+Vector3.up*.12f+aimDirection*.72f;
        return Vector3.Lerp(center,BeamOrigin(),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,progress)));
    }
    Vector3 GroundPoint()
    {
        var ground=transform.position;
        var contacts=Physics.RaycastAll(ground+Vector3.up,Vector3.down,3,~0,QueryTriggerInteraction.Ignore);
        Array.Sort(contacts,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var contact in contacts)if(!contact.collider.transform.IsChildOf(transform)){ground=contact.point;break;}
        return ground;
    }
    Vector3 BodyForward()
    {
        var body=driver.visualRoot!=null?driver.visualRoot:transform;var forward=Vector3.ProjectOnPlane(body.forward,Vector3.up).normalized;
        return forward.sqrMagnitude>.001f?forward:Vector3.forward;
    }
    void Release()
    {
        GetComponent<CharacterActionAudio>()?.Magic(true);
        released=true;releasedAt=Time.time;nextDamageAt=releasedAt;aimDirection=BodyForward();ActionForward=aimDirection;
        // Each discharge, including the red upgrade, starts its own one-metre recoil.
        recoilDirection=-Planar(aimDirection);recoilTravel=0;
        var origin=BeamOrigin();effect?.Fire(origin,origin+aimDirection*beamRange,ActiveBeamDuration);UpdateBeam();
    }
    void UpdateBeam()
    {
        var origin=BeamOrigin();float stop=beamRange;
        bool damageTick=Time.time>=nextDamageAt;
        if(damageTick)
        {
            hit.Clear();
            nextDamageAt=Time.time+DamageInterval;
        }
        var contacts=Physics.SphereCastAll(origin,beamRadius*(empowered?1.7f:1),aimDirection,beamRange,~0,QueryTriggerInteraction.Ignore);
        Array.Sort(contacts,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var contact in contacts)
        {
            if(contact.collider==null||contact.collider.transform.IsChildOf(transform))continue;
            var reaction=contact.collider.GetComponentInParent<CharacterHitReaction>();
            Transform tagged=null;for(var t=contact.collider.transform;t!=null;t=t.parent)if(t.CompareTag("Enemy")){tagged=t;break;}
            bool target=reaction!=null||tagged!=null;
            if(!target){stop=Mathf.Min(stop,contact.distance);break;}
            if(contact.distance>stop)break;
            if(!damageTick)continue;
            int id=reaction!=null?reaction.GetInstanceID():tagged.GetInstanceID();if(!hit.Add(id))continue;
            bool accepted=true;if(reaction!=null){if(reaction.driver==driver)continue;accepted=reaction.ReceiveHit(origin,attacker:driver,special:true,attackType:PlayerProgression.AttackType(driver,2),weaponSlot:2,damageMultiplier:3f);}
            else tagged.GetComponent<CombatDummy>()?.RegisterHit();
            if(!accepted)continue;HitCount++;
            CombatHitFeedback.Play(contact.point,driver.movementCamera,false,aimDirection,contact.collider,false,false);
        }
        var end=origin+aimDirection*stop;
        effect?.SetBeam(origin,end);
    }
    public void InterruptForHit(){if(IsCasting)Cancel(false);}
    public void Cancel(bool recover)
    {Finish(recover,true);}
    void Finish(bool recover,bool interrupted)
    {
        if(!IsCasting)return;readyAt=Time.time+cooldownSeconds;IsCasting=false;driver.equipmentBusy=false;driver.SetAttackActive(false);
        GetComponent<CharacterActionAudio>()?.StopMagic();
        if(driver.animator!=null){driver.animator.SetBool("IsAttacking",false);if(recover&&!driver.IsActionBlocked)driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, driver.IsGrounded?"Locomotion":"Fall",.16f,0,0);}
        if(interrupted&&effect!=null)effect.Cancel();
        effect=null;hit.Clear();
    }
    void OnDisable(){Cancel(true);}
}
