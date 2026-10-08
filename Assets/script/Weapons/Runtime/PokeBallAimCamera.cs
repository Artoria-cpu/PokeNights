using UnityEngine;
using Unity.Cinemachine;

/// <summary>Aiming looks along mouse-controlled yaw/pitch instead of back down at the character.</summary>
[DisallowMultipleComponent]
public sealed class PokeBallAimCamera : CinemachineExtension
{
    public Vector3 cameraOffset=new Vector3(.65f,1.65f,-3.2f);
    [Range(30,90)] public float fieldOfView=60f;
    PlayerOrbitCameraInput input;
    public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam,ref CameraState state,float deltaTime)
    {
        if(input==null)input=GetComponent<PlayerOrbitCameraInput>();
        if(input!=null&&input.isActiveAndEnabled)input.ApplyAimFraming();
    }
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,CinemachineCore.Stage stage,ref CameraState state,float deltaTime)
    {
        if(stage!=CinemachineCore.Stage.Finalize)return;
        if(input==null)input=GetComponent<PlayerOrbitCameraInput>();
        if(input==null||!input.isActiveAndEnabled||input.orbit==null||input.AimBlend<=.001f)return;
        if(vcam.Follow==null)return;
        float blend=input.AimBlend;
        var heading=Quaternion.Euler(0,input.orbit.HorizontalAxis.Value,0);
        // Absolute framing excludes the orbit radius, vertical axis and accumulated damping.
        state.RawPosition=Vector3.Lerp(state.RawPosition,vcam.Follow.position+heading*cameraOffset,blend);
        state.PositionCorrection*=1-blend;
        state.RawOrientation=Quaternion.Slerp(state.RawOrientation,Quaternion.Euler(input.AimPitch,input.orbit.HorizontalAxis.Value,0),blend);
        state.OrientationCorrection=Quaternion.Slerp(state.OrientationCorrection,Quaternion.identity,blend);
        var lens=state.Lens;lens.FieldOfView=Mathf.Lerp(lens.FieldOfView,fieldOfView,blend);lens.Dutch=Mathf.Lerp(lens.Dutch,0,blend);state.Lens=lens;
    }
}

