using System;
using UnityEngine;

[DefaultExecutionOrder(10010), DisallowMultipleComponent]
public sealed class TealWitchClothDynamics : MonoBehaviour
{
    [Serializable] public class Point
    {
        public Transform bone;
        public Vector3 restPosition;
        public bool sleeve;
        public float mobility=1;
        [NonSerialized] public Vector3 position, velocity;
        [NonSerialized] internal Vector3 offset, oldAnchor;
    }
    public Point[] points;
    public Transform hips,leftThigh,leftKnee,rightThigh,rightKnee;
    public float skirtFrequency=3.8f,sleeveFrequency=3.2f,damping=1.1f;
    public float MaximumSkirtOffset {get;private set;}
    public float MaximumSleeveOffset {get;private set;}
    void OnEnable(){ResetSimulation();}
    public void ResetSimulation()
    {
        if(points==null)return;
        foreach(var p in points){p.bone.localPosition=p.restPosition;p.position=p.oldAnchor=p.bone.position;p.offset=p.velocity=Vector3.zero;}
        MaximumSkirtOffset=MaximumSleeveOffset=0;
    }
    void LateUpdate()
    {
        if(points==null)return;float dt=Mathf.Min(Time.deltaTime,.05f);if(dt<=0)return;
        foreach(var p in points){
            var anchor=p.bone.parent.TransformPoint(p.restPosition);var speed=(anchor-p.oldAnchor)/Mathf.Max(Time.deltaTime,.001f);p.oldAnchor=anchor;
            var target=Vector3.ClampMagnitude(-speed*(p.sleeve?.009f:.003f)*p.mobility,p.sleeve?.023f:.007f);
            if(!p.sleeve)target=Vector3.ProjectOnPlane(target,hips.up);
            float omega=p.sleeve?18:22;var delta=p.offset-target;var j=p.velocity+omega*delta;float decay=Mathf.Exp(-omega*dt);
            p.offset=target+(delta+j*dt)*decay;p.velocity=(p.velocity-omega*j*dt)*decay;
            p.position=anchor+p.offset;p.bone.position=p.position;
            if(p.sleeve)MaximumSleeveOffset=Mathf.Max(MaximumSleeveOffset,p.offset.magnitude);else MaximumSkirtOffset=Mathf.Max(MaximumSkirtOffset,p.offset.magnitude);
        }
    }
    void OnDisable(){if(points!=null)foreach(var p in points)if(p.bone!=null)p.bone.localPosition=p.restPosition;}
}
