using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DefaultExecutionOrder(-60), DisallowMultipleComponent]
public sealed class CombatLockOn : MonoBehaviour
{
    public PlayerRoster selection;
    public Transform Target {get;private set;}
    public float range=20;
    [Range(1,180)] public float facingConeDegrees=120;
    [Header("Lock framing")]
    [Min(0)] public float aimHeight=1.15f;
    [Min(1)] public float minimumAimDistance=3.5f;
    [Min(1)] public float aimSmoothing=12;
    TargetOutline outlined;
    CharacterMotor framedActor;
    PlayerOrbitCameraInput input;
    CinemachineOrbitalFollow orbit;
    Transform lockFocus;
    readonly List<Transform> candidates=new List<Transform>();

    void Awake()
    {
        if(selection==null)selection=GetComponent<PlayerRoster>();
        input=selection.followCamera.GetComponent<PlayerOrbitCameraInput>();
        orbit=selection.followCamera.GetComponent<CinemachineOrbitalFollow>();
    }
    void Update()
    {
        if(selection.ActiveCharacter!=null&&selection.ActiveCharacter.IsBallThrowing){if(Target!=null)Unlock();return;}
        var beam=selection.ActiveCharacter!=null?selection.ActiveCharacter.GetComponent<OrbBeamSkill>():null;
        if(beam!=null&&beam.OwnsBody)
        {if(Target!=null||input!=null&&input.lockOnActive)Unlock();return;}
        if(Mouse.current!=null&&Mouse.current.middleButton.wasPressedThisFrame)Toggle();
        var actor=selection.ActiveCharacter;if(actor==null)return;
        if(Target!=null&&!ValidTarget(Target,actor))Unlock();
        if(Target!=null&&Keyboard.current!=null)
        {
            var keys=Keyboard.current;
            if(keys.leftArrowKey.wasPressedThisFrame)SwitchTarget(Vector2.left);
            else if(keys.rightArrowKey.wasPressedThisFrame)SwitchTarget(Vector2.right);
            else if(keys.upArrowKey.wasPressedThisFrame)SwitchTarget(Vector2.up);
            else if(keys.downArrowKey.wasPressedThisFrame)SwitchTarget(Vector2.down);
        }
        actor.combatLookTarget=Target;
        if(Target==null){if(input!=null&&input.lockOnActive)Unlock();return;}
        var highlight=Target.GetComponent<TargetOutline>();
        if(outlined!=highlight){if(outlined!=null)outlined.SetHighlighted(false);outlined=highlight;}
        if(outlined!=null)outlined.SetHighlighted(true);
        var direction=Vector3.ProjectOnPlane(Target.position-actor.transform.position,Vector3.up);
        // Aim at chest height, keeping the focus ahead at melee distance instead of at the feet.
        var forward=direction.sqrMagnitude>.001f?direction.normalized:(actor.visualRoot!=null?actor.visualRoot.forward:actor.transform.forward);
        var focus=actor.transform.position+forward*Mathf.Max(direction.magnitude,minimumAimDistance);
        focus.y=actor.transform.position.y+aimHeight+Mathf.Clamp(Target.position.y-actor.transform.position.y,-1.2f,1.2f);
        if(lockFocus==null)
        {
            var go=new GameObject("Lock camera focus"){hideFlags=HideFlags.DontSave};
            lockFocus=go.transform;lockFocus.SetParent(transform,false);lockFocus.position=focus;
        }
        if(framedActor!=actor)lockFocus.position=focus;
        else lockFocus.position=Vector3.Lerp(lockFocus.position,focus,1-Mathf.Exp(-aimSmoothing*Time.unscaledDeltaTime));
        selection.followCamera.LookAt=lockFocus;
        float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
        if(input!=null){if(!input.lockOnActive||framedActor!=actor)input.BeginLockView(yaw);input.SetLockHeading(yaw);}
        else if(orbit!=null)orbit.HorizontalAxis.Value=orbit.HorizontalAxis.ClampValue(yaw);
        framedActor=actor;
    }
    bool ValidTarget(Transform target,CharacterMotor actor)
    {
        if(target==null||target==actor.transform||!target.gameObject.activeInHierarchy||Vector3.Distance(actor.transform.position,target.position)>range)return false;
        var driver=target.GetComponent<CharacterMotor>();
        return driver==null||(driver.isActiveAndEnabled&&!CharacterCombatStats.Dead(driver));
    }
    void GatherTargets(CharacterMotor actor)
    {
        candidates.Clear();
        foreach(var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            if(ValidTarget(enemy.transform,actor)&&!candidates.Contains(enemy.transform))candidates.Add(enemy.transform);
        foreach(var enemy in selection.characters)
            if(enemy!=null&&ValidTarget(enemy.transform,actor)&&!candidates.Contains(enemy.transform))candidates.Add(enemy.transform);
    }
    void SetTarget(Transform target)
    {
        if(outlined!=null)outlined.SetHighlighted(false);
        outlined=null;Target=target;
    }
    /// <summary>
    /// Point the lock at a specific target, e.g. whoever just got parried.
    /// With engageIfUnlocked false this only RETARGETS an existing lock; it will not drag
    /// the player into lock-camera mode when they were playing free, which would otherwise
    /// pin the orbit heading to the enemy and stop the camera trailing the character.
    /// </summary>
    public bool SwitchLockTo(Transform target,bool engageIfUnlocked)
    {
        var actor=selection!=null?selection.ActiveCharacter:null;
        if(actor==null||target==null)return false;
        if(Target==null&&!engageIfUnlocked)return false;
        if(!ValidTarget(target,actor))return false;
        if(Target==target)return true;
        SetTarget(target);return true;
    }
    public void Toggle()
    {
        if(Target!=null){Unlock();return;}
        var actor=selection.ActiveCharacter;if(actor==null)return;
        GatherTargets(actor);float nearest=float.PositiveInfinity,nearestFacing=float.PositiveInfinity;Transform next=null,front=null;
        Vector3 forward=Vector3.ProjectOnPlane((actor.visualRoot!=null?actor.visualRoot:actor.transform).forward,Vector3.up).normalized;
        float facingThreshold=Mathf.Cos(facingConeDegrees*.5f*Mathf.Deg2Rad);
        foreach(var candidate in candidates)
        {
            Vector3 delta=candidate.position-actor.transform.position;
            float distance=delta.sqrMagnitude;
            if(distance<nearest){nearest=distance;next=candidate;}
            Vector3 planar=Vector3.ProjectOnPlane(delta,Vector3.up);
            if(distance<nearestFacing&&(planar.sqrMagnitude<.001f||Vector3.Dot(forward,planar.normalized)>=facingThreshold))
            {nearestFacing=distance;front=candidate;}
        }
        SetTarget(front!=null?front:next);
    }
    public bool SwitchTarget(Vector2 direction)
    {
        var actor=selection.ActiveCharacter;var camera=selection.outputCamera;
        if(Target==null||actor==null||camera==null||direction.sqrMagnitude<.01f)return false;
        GatherTargets(actor);direction.Normalize();
        Vector2 origin=camera.WorldToScreenPoint(Target.position+Vector3.up*aimHeight);
        Transform next=null,wrap=null;float best=float.PositiveInfinity,wrapScore=float.PositiveInfinity;
        foreach(var candidate in candidates)
        {
            if(candidate==Target)continue;
            Vector3 screen=camera.WorldToScreenPoint(candidate.position+Vector3.up*aimHeight);
            if(screen.z<=0)continue;
            Vector2 delta=(Vector2)screen-origin;
            float along=Vector2.Dot(delta,direction);
            float across=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
            float score=delta.magnitude+across*1.5f;
            if(along>1&&score<best){best=score;next=candidate;}
            // Wrap to the opposite side when there is no further target in this direction.
            if(along<wrapScore){wrapScore=along;wrap=candidate;}
        }
        if(next==null)next=wrap;
        if(next==null)return false;
        SetTarget(next);return true;
    }
    public void Unlock()
    {
        SetTarget(null);framedActor=null;if(input!=null)input.EndLockView();
        if(selection==null)return;
        var actor=selection.ActiveCharacter;
        if(actor!=null){actor.combatLookTarget=null;var focus=actor.transform.Find("CameraFocus");selection.followCamera.LookAt=focus!=null?focus:actor.transform;}
    }
    void OnDisable(){Unlock();}
    void OnDestroy(){if(lockFocus!=null)Destroy(lockFocus.gameObject);}
}
