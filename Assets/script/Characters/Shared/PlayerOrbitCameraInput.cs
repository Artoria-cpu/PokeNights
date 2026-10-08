using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent, RequireComponent(typeof(CinemachineOrbitalFollow))]
[DefaultExecutionOrder(-50)]
public sealed class PlayerOrbitCameraInput : MonoBehaviour
{
    public CinemachineOrbitalFollow orbit;
    public float sensitivity = .18f;
    public float verticalSensitivity = .0025f;
    public float zoomStep = .10f;
    [Tooltip("Resting orbit scale. 1 is the authored radius; above 1 sits further back.")]
    public float defaultZoom = 1.35f;
    [Header("Ball aiming")]
    public float aimZoom = 1.15f;
    [Range(0,90)] public float aimYawRange = 40f;
    public Vector3 aimOffset = new Vector3(.95f, .85f, .4f);
    public Vector2 aimScreenPosition = new Vector2(-.18f, -.06f);
    CinemachineCamera followCamera;
    CinemachineRotationComposer composer;
    Vector3 restingOffset;
    Vector2 restingScreenPosition;
    float aimBlend;
    public float AimBlend=>aimBlend;
    public float AimPitch {get;private set;}
    bool wasAiming;
    float preAimVertical;
    float aimHeading;
    [System.NonSerialized] public bool lockOnActive;
    [Header("Lock-on orbit")]
    public float lockYawRange=18f;
    public float lockVerticalRange=.35f;
    public float lockRecenterDuration=.28f;
    public Vector2 LockOffset {get;private set;}
    public bool IsLockRecentering=>lockOnActive&&Time.unscaledTime-lockStarted<lockRecenterDuration;
    float lockHeading,lockStarted,lockStartYaw,lockStartVertical,zoomTarget;
    private bool released;
    private void OnEnable() {
        if (orbit == null) orbit=GetComponent<CinemachineOrbitalFollow>();
        followCamera=GetComponent<CinemachineCamera>();composer=GetComponent<CinemachineRotationComposer>();
        restingOffset=orbit.TargetOffset;
        if(composer!=null)restingScreenPosition=composer.Composition.ScreenPosition;
        aimBlend=0;wasAiming=false;released=false;zoomTarget=orbit.RadialAxis.ClampValue(defaultZoom);orbit.RadialAxis.Value=zoomTarget;
    }
    private void OnDisable() {
        if(orbit!=null){orbit.TargetOffset=restingOffset;orbit.RadialAxis.Value=zoomTarget;if(wasAiming||aimBlend>.001f)orbit.VerticalAxis.Value=preAimVertical;}
        aimBlend=0;wasAiming=false;
        if(composer!=null)composer.Composition.ScreenPosition=restingScreenPosition;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }
    private void Update()
    {
        if (orbit == null) return;
        var mouse=Mouse.current; var keyboard=Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) released=true;
        if (released && mouse != null && mouse.leftButton.wasPressedThisFrame && Application.isFocused
            && !(UnityEngine.EventSystems.EventSystem.current!=null&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) released=false;
        bool capture=!released && Application.isFocused;
        Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None; Cursor.visible=!capture;
        var target=followCamera!=null?followCamera.Follow:null;
        var actor=target!=null?target.GetComponent<CharacterMotor>():null;
        bool aiming=actor!=null&&actor.IsBallThrowing;
        float blend=1-Mathf.Exp(-14*Time.unscaledDeltaTime);
        if(aiming&&!wasAiming){var facing=actor.visualRoot!=null?actor.visualRoot:actor.transform;orbit.HorizontalAxis.Value=orbit.HorizontalAxis.ClampValue(facing.eulerAngles.y);}
        UpdateAimState(aiming,blend);
        if(!aiming&&capture&&mouse!=null)zoomTarget=orbit.RadialAxis.ClampValue(zoomTarget-mouse.scroll.ReadValue().y/120f*zoomStep);
        orbit.RadialAxis.Value=Mathf.Lerp(orbit.RadialAxis.Value,aiming?orbit.RadialAxis.ClampValue(aimZoom):zoomTarget,blend);
        if(lockOnActive){UpdateLockedView(mouse,keyboard);return;}
        if (!capture) return;
        if (mouse != null)
        {
            var delta=mouse.delta.ReadValue();
            float yaw=orbit.HorizontalAxis.Value+delta.x*sensitivity;
            if(aiming)yaw=aimHeading+Mathf.Clamp(Mathf.DeltaAngle(aimHeading,yaw),-aimYawRange,aimYawRange);
            orbit.HorizontalAxis.Value=orbit.HorizontalAxis.ClampValue(yaw);
            if(aiming)AimPitch=Mathf.Clamp(AimPitch-delta.y*sensitivity,-65,45);
            else if(aimBlend>.001f)preAimVertical=orbit.VerticalAxis.ClampValue(preAimVertical-delta.y*verticalSensitivity);
            else orbit.VerticalAxis.Value=orbit.VerticalAxis.ClampValue(orbit.VerticalAxis.Value-delta.y*verticalSensitivity);
        }

    }
    void UpdateAimState(bool aiming,float blend)
    {
        if(aiming&&!wasAiming){AimPitch=0;if(aimBlend<=.001f)preAimVertical=orbit.VerticalAxis.Value;aimHeading=orbit.HorizontalAxis.Value;}
        wasAiming=aiming;
        bool restoring=aimBlend>.001f;
        aimBlend=Mathf.Lerp(aimBlend,aiming?1:0,blend);
        if(!aiming&&aimBlend<=.001f)aimBlend=0;
        if(aiming||restoring)orbit.VerticalAxis.Value=Mathf.Lerp(preAimVertical,.5f,aimBlend);
    }
    // Run in Cinemachine's pipeline after character movement and this frame's mouse input.
    public void ApplyAimFraming()
    {
        if(orbit==null)return;
        var target=followCamera!=null?followCamera.Follow:null;
        var shoulder=Quaternion.Euler(0,orbit.HorizontalAxis.Value,0)*aimOffset;
        if(target!=null)shoulder=Quaternion.Inverse(target.rotation)*shoulder;
        orbit.TargetOffset=restingOffset+shoulder*aimBlend;
        if(composer!=null)composer.Composition.ScreenPosition=Vector2.Lerp(restingScreenPosition,aimScreenPosition,aimBlend);
    }
    public void BeginLockView(float heading)
    {
        if(orbit==null)orbit=GetComponent<CinemachineOrbitalFollow>();
        lockOnActive=true;LockOffset=Vector2.zero;lockHeading=heading;lockStarted=Time.unscaledTime;
        lockStartYaw=orbit.HorizontalAxis.Value;lockStartVertical=orbit.VerticalAxis.Value;
    }
    public void SetLockHeading(float heading){lockHeading=heading;}
    public void EndLockView(){lockOnActive=false;LockOffset=Vector2.zero;}
    void UpdateLockedView(Mouse mouse,Keyboard keyboard)
    {

        if(IsLockRecentering)
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-lockStarted)/Mathf.Max(.01f,lockRecenterDuration)));
            orbit.HorizontalAxis.Value=orbit.HorizontalAxis.ClampValue(Mathf.LerpAngle(lockStartYaw,lockHeading,t));
            orbit.VerticalAxis.Value=orbit.VerticalAxis.ClampValue(Mathf.Lerp(lockStartVertical,.5f,t));
            return;
        }
        if(!released&&Application.isFocused&&mouse!=null)
        {
            var delta=mouse.delta.ReadValue();
            LockOffset=new Vector2(Mathf.Clamp(LockOffset.x+delta.x*sensitivity*.65f,-lockYawRange,lockYawRange),Mathf.Clamp(LockOffset.y-delta.y*verticalSensitivity*.6f,-lockVerticalRange,lockVerticalRange));
        }
        float blend=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
        orbit.HorizontalAxis.Value=orbit.HorizontalAxis.ClampValue(Mathf.LerpAngle(orbit.HorizontalAxis.Value,lockHeading+LockOffset.x,blend));
        orbit.VerticalAxis.Value=orbit.VerticalAxis.ClampValue(Mathf.Lerp(orbit.VerticalAxis.Value,.5f+LockOffset.y,blend));
    }
    public void ResetView() { if(orbit==null)return;zoomTarget=orbit.RadialAxis.ClampValue(defaultZoom);if(lockOnActive){BeginLockView(lockHeading);return;} orbit.HorizontalAxis.Value=180f;orbit.VerticalAxis.Value=.5f;orbit.RadialAxis.Value=zoomTarget; }
}

