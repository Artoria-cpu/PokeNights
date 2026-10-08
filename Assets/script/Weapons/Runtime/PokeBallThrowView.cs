using UnityEngine;

// Hand sockets and preview origins must follow the final corrected pose.
[DisallowMultipleComponent, RequireComponent(typeof(PokeBallThrow)), DefaultExecutionOrder(21000)]
public sealed class PokeBallThrowView : MonoBehaviour
{
    PokeBallThrow owner;
    CharacterMotor driver;
    Transform spine;
    Transform leftShoulder,rightShoulder,chest,neck;
    Quaternion originalSpineRotation;
    bool adjusted;
    void Awake(){owner=GetComponent<PokeBallThrow>();driver=GetComponent<CharacterMotor>();}
    void Update(){RestorePose();}
    void RestorePose(){if(adjusted&&spine!=null)spine.localRotation=originalSpineRotation;adjusted=false;}
    void LateUpdate()
    {
        if(owner==null||!owner.isActiveAndEnabled||!owner.IsActive)return;
        if(spine==null&&driver.animator!=null)
        {
            spine=driver.animator.GetBoneTransform(HumanBodyBones.Spine);
            leftShoulder=driver.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightShoulder=driver.animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            chest=driver.animator.GetBoneTransform(HumanBodyBones.Chest);
            neck=driver.animator.GetBoneTransform(HumanBodyBones.Neck);
        }
        var camera=driver.movementCamera!=null?driver.movementCamera:Camera.main;
        if(spine!=null&&camera!=null)
        {
            var body=driver.visualRoot!=null?driver.visualRoot:driver.transform;
            // Measure the animated torso, so the source throw's twist is corrected too.
            var forward=body.forward;
            if(leftShoulder!=null&&rightShoulder!=null&&chest!=null&&neck!=null)
            {
                var torsoForward=Vector3.Cross(rightShoulder.position-leftShoulder.position,neck.position-chest.position);
                if(torsoForward.sqrMagnitude>.000001f)forward=torsoForward.normalized;
            }
            // Keep the throwing torso 45 degrees to the right of the camera's facing.
            var aimForward=Quaternion.AngleAxis(45f,Vector3.up)*camera.transform.forward;
            var correction=Quaternion.FromToRotation(forward,aimForward);
            originalSpineRotation=spine.localRotation;adjusted=true;
            spine.rotation=Quaternion.Slerp(Quaternion.identity,correction,owner.PoseWeight)*spine.rotation;
        }
        owner.Present();
    }
    void OnDisable(){RestorePose();}
}
