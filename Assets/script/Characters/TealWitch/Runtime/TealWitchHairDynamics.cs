using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>Inertial particle strands with fixed-step length/bend constraints and swept body contacts.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10020)]
public sealed class TealWitchHairDynamics : MonoBehaviour
{
    [Serializable] public class Chain
    {
        public Transform[] joints;
        public Vector3 localTip = new Vector3(0, -.035f, 0);
        [NonSerialized] internal Quaternion[] rotations, referenceRotations;
        [NonSerialized] internal Vector3[] points, previous, velocities, restVectors, localPositions;
        [NonSerialized] internal Vector3[] parentRestPoints, goals, referencePoints, offsets, offsetVelocities;
        [NonSerialized] internal Vector3 lastAnchor, filteredVelocity;
        [NonSerialized] internal Vector3[] lastGuides, guideVelocities;
        [NonSerialized] internal Vector3 wholeBend, wholeBendVelocity;
        [NonSerialized] internal Vector3[] segmentDirections, contactNormals, contactVelocities;
        [NonSerialized] internal float[] lengths, radii;
        [NonSerialized] internal Vector3 anchor, frameAnchor, rootDirection, frameDirection;
        [NonSerialized] internal float scale = 1f;
    }
    [HideInInspector] public float[] hatUnderside = Array.Empty<float>();
    [HideInInspector] public Vector4 hatDomain;
    [HideInInspector] public int hatGridSize;
    internal TealWitchHatContact HatContact;
    Transform hatBone;
    Matrix4x4 hatBind, lastHatFrame;
    public Chain[] tails;
    public Transform chest, hips;
    [HideInInspector] public float motionAmount=1;
    [HideInInspector] public float translationLag=.035f, rotationLag=.065f, tipResponse=8f;
    [HideInInspector] public float runningLift=58f;
    [HideInInspector] public float liftResponse=7f;
    [HideInInspector] public float rootFollow=28f, endFollow=5.5f;
    [HideInInspector] public float endDroop=.65f;
    public float CurrentLiftDegrees {get;private set;}
    public float MaximumInertialOffset {get;private set;}
    [Min(0)] public float gravity = 7f;
    [Min(0)] public float damping = 4.5f;
    [Range(0,1)] public float rootStiffness = .28f;
    [Range(0,1)] public float bendStiffness = .045f;
    [Min(0)] public float strandRadius = .035f;
    [Min(0)] public float shapeSpring = 8f;
    [HideInInspector] public float maximumLag = .12f;
    [Range(4,24)] public int constraintIterations = 12;
    const float Step = 1f / 120f;
    sealed class Volume
    {
        public Transform bone;
        public Vector3 center, radii, worldRadii;
        public Quaternion localRotation;
        public Vector3 lastCenter, currentCenter;
        public Quaternion lastRotation, currentRotation, inverseRotation, solverRotation, solverInverse;
        public Vector3 solverCenter;
    }
    readonly List<Chain> chains = new List<Chain>();
    readonly List<Volume> volumes = new List<Volume>();
    Vector3 oldRoot;
    Quaternion oldRotation;
    float accumulator;
    bool ready;
    SkinnedMeshRenderer skin;
    TealWitchHairConstraintJob constraints;
    public float MaximumTipLag { get; private set; }
    public int CollisionCorrections { get; private set; }

    void OnEnable() { Rebuild(); }
    public void Rebuild()
    {
        Restore(); chains.Clear(); volumes.Clear();
        foreach(var s in GetComponentsInChildren<SkinnedMeshRenderer>())if(s.name=="Hair"){skin=s;break;}
        if(skin==null||tails==null)return;
        var indices=new Dictionary<Transform,int>();for(int i=0;i<skin.bones.Length;i++)indices[skin.bones[i]]=i;
        var bind=skin.sharedMesh.bindposes;
        
        foreach(var c in tails)if(c!=null&&c.joints!=null&&c.joints.Length>=2){chains.Add(c);}
        
        foreach(var c in chains){c.scale=StrandScale(c);int n=c.joints.Length;c.rotations=new Quaternion[n];c.referenceRotations=new Quaternion[n];c.contactNormals=new Vector3[n+1];c.contactVelocities=new Vector3[n+1];c.points=new Vector3[n+1];c.previous=new Vector3[n+1];c.velocities=new Vector3[n+1];c.restVectors=new Vector3[n];c.localPositions=new Vector3[n];c.lengths=new float[n];c.radii=new float[n+1];c.parentRestPoints=new Vector3[n+1];c.goals=new Vector3[n+1];c.referencePoints=new Vector3[n+1];c.offsets=new Vector3[n+1];c.offsetVelocities=new Vector3[n+1];
            for(int i=0;i<n;i++){var joint=c.joints[i];int index=indices[joint];var rest=bind[index].inverse;c.rotations[i]=indices.TryGetValue(joint.parent,out int p)?(bind[p]*rest).rotation:joint.localRotation;
                Vector3 delta=i+1<n?bind[indices[c.joints[i+1]]].inverse.MultiplyPoint3x4(Vector3.zero)-rest.MultiplyPoint3x4(Vector3.zero):rest.MultiplyVector(c.localTip);
                c.localPositions[i]=indices.TryGetValue(joint.parent,out int parent)?(bind[parent]*rest).MultiplyPoint3x4(Vector3.zero):joint.localPosition;
                c.restVectors[i]=delta;c.lengths[i]=skin.transform.TransformVector(delta).magnitude*c.scale;
                c.referenceRotations[i]=(bind[indices[c.joints[0].parent]]*rest).rotation;c.referencePoints[i]=rest.MultiplyPoint3x4(Vector3.zero);c.parentRestPoints[i]=bind[indices[c.joints[0].parent]].MultiplyPoint3x4(c.referencePoints[i]);
                if(i==n-1){c.referencePoints[n]=rest.MultiplyPoint3x4(c.localTip);c.parentRestPoints[n]=bind[indices[c.joints[0].parent]].MultiplyPoint3x4(c.referencePoints[n]);}
            }
        }
        AddVolume("Head",new Vector3(0,1.49f,-.015f),new Vector3(.125f,.145f,.115f),indices,bind);
        AddVolume("UpperChest",new Vector3(0,1.26f,.008f),new Vector3(.132f,.13f,.095f),indices,bind);
        AddVolume("Spine",new Vector3(0,1.11f,.005f),new Vector3(.108f,.12f,.08f),indices,bind);
        AddVolume("Hips",new Vector3(0,.975f,.008f),new Vector3(.148f,.105f,.108f),indices,bind);
        foreach(string side in new[]{"Left","Right"}){float sign=side=="Left"?-1:1;AddVolume(side+"UpperArm",new Vector3(sign*.245f,1.29f,.005f),new Vector3(.105f,.079f,.079f),indices,bind);AddVolume(side+"LowerArm",new Vector3(sign*.42f,1.28f,.005f),new Vector3(.09f,.09f,.085f),indices,bind);}
        constraints.Dispose();constraints=default;int particles=0;foreach(var c in chains)particles+=c.points.Length;constraints.Create(particles,chains.Count,volumes.Count);
        if(HatContact.heights.IsCreated)HatContact.heights.Dispose();
        HatContact=new TealWitchHatContact{heights=new Unity.Collections.NativeArray<float>(hatUnderside,Unity.Collections.Allocator.Persistent),size=hatGridSize,domain=hatDomain};
        HatContact.minimumHeight=float.PositiveInfinity;foreach(float height in hatUnderside)if(height>=0)HatContact.minimumHeight=Mathf.Min(HatContact.minimumHeight,height);
        hatBone=volumes[0].bone;hatBind=bind[indices[hatBone]];
        ready=true;ResetSimulation();
    }
    void AddVolume(string name,Vector3 center,Vector3 radii,Dictionary<Transform,int> indices,Matrix4x4[] bind)
    {
        foreach(var b in skin.bones)if(b.name==name){int i=indices[b];volumes.Add(new Volume{bone=b,center=bind[i].MultiplyPoint3x4(center),radii=radii,localRotation=bind[i].rotation});break;}
    }
    void Restore(){foreach(var c in chains)if(c.rotations!=null)for(int i=0;i<c.joints.Length;i++)if(c.joints[i]!=null){c.joints[i].localRotation=c.rotations[i];c.joints[i].localPosition=c.localPositions[i];}}
    void SampleVolumes(bool reset){HatContact.toWorld=hatBone.localToWorldMatrix*hatBind;HatContact.toLocal=math.inverse(HatContact.toWorld);if(reset)lastHatFrame=HatContact.toWorld;var bones=skin.bones;var bind=skin.sharedMesh.bindposes;int ci=Array.IndexOf(bones,chest),hi=Array.IndexOf(bones,hips);chestFrame=chest.localToWorldMatrix*bind[ci];hipFrame=hips.localToWorldMatrix*bind[hi];for(int i=0;i<8;i++){float y=1+i*.06f;backPoints[i]=TorsoPoint(new Vector3(0,y,Mathf.Lerp(-.15f,-.063f,Mathf.InverseLerp(1.02f,1.18f,y))));backNormals[i]=Vector3.Lerp(hipFrame.MultiplyVector(Vector3.back),chestFrame.MultiplyVector(Vector3.back),Mathf.InverseLerp(1.02f,1.32f,y)).normalized;}foreach(var v in volumes){v.currentCenter=v.bone.TransformPoint(v.center);v.currentRotation=v.bone.rotation*v.localRotation;v.inverseRotation=Quaternion.Inverse(v.currentRotation);v.worldRadii=Vector3.Scale(v.radii,v.bone.lossyScale);if(reset){v.lastCenter=v.currentCenter;v.lastRotation=v.currentRotation;}}}
    public void ResetSimulation()
    {
        Restore();accumulator=0;oldRoot=transform.position;oldRotation=transform.rotation;MaximumTipLag=0;CollisionCorrections=0;
        foreach(var c in chains){int n=c.joints.Length;for(int i=0;i<=n;i++){c.points[i]=i<n?c.joints[i].position:c.joints[n-1].TransformPoint(c.localTip);c.previous[i]=c.points[i];c.velocities[i]=Vector3.zero;}c.anchor=c.frameAnchor=c.lastAnchor=c.points[0];c.filteredVelocity=Vector3.zero;Array.Clear(c.offsets,0,c.offsets.Length);Array.Clear(c.offsetVelocities,0,c.offsetVelocities.Length);c.rootDirection=c.frameDirection=(c.points[1]-c.points[0]).normalized;}
        SampleVolumes(true);MaximumInertialOffset=0;
        CurrentLiftDegrees=0;
        foreach(var c in chains){c.wholeBend=c.wholeBendVelocity=Vector3.zero;c.lastGuides=new Vector3[c.points.Length];c.guideVelocities=new Vector3[c.points.Length];c.segmentDirections=new Vector3[c.joints.Length];for(int i=1;i<c.points.Length;i++){c.lastGuides[i]=Goal(c,i);c.segmentDirections[i-1]=(c.points[i]-c.points[i-1]).normalized;}}
    }
    float StrandScale(Chain c) => Mathf.Abs(c.joints[0].lossyScale.x / Mathf.Max(.001f,Mathf.Abs(skin.transform.lossyScale.x)));
    void UpdateStrandScales()
    {
        bool changed=false;
        foreach(var c in chains){float scale=StrandScale(c);if(Mathf.Abs(scale-c.scale)<.0001f)continue;float ratio=scale/Mathf.Max(.001f,c.scale);for(int i=0;i<c.lengths.Length;i++)c.lengths[i]*=ratio;c.scale=scale;changed=true;}
        if(changed)ResetSimulation();
    }
    void LateUpdate(){Simulate(Time.deltaTime);}
    public void Simulate(float dt)
    {
        if(!ready)Rebuild();if(!ready||dt<=0)return;UpdateStrandScales();
        if(dt>.2f||Vector3.Distance(transform.position,oldRoot)>1f||Quaternion.Angle(transform.rotation,oldRotation)>120f)ResetSimulation();
        oldRoot=transform.position;oldRotation=transform.rotation;dt=Mathf.Min(dt,.1f);Restore();
        Array.Copy(backPoints,oldBackPoints,8);Array.Copy(backNormals,oldBackNormals,8);SampleVolumes(false);
        foreach(var c in chains){c.frameAnchor=c.joints[0].position;c.frameDirection=(c.joints[1].position-c.joints[0].position).normalized;for(int i=1;i<c.points.Length;i++)c.goals[i]=Goal(c,i);}
        float before=accumulator;accumulator+=dt;int steps=Mathf.FloorToInt(accumulator/Step);accumulator-=steps*Step;
        for(int step=0;step<steps;step++){
            float alpha=Mathf.Clamp01(((step+1)*Step-before)/dt);SampleContacts(alpha);
            foreach(var c in chains)Predict(c,Vector3.Lerp(c.anchor,c.frameAnchor,alpha),Vector3.Slerp(c.rootDirection,c.frameDirection,alpha));
            int start=0;
            for(int k=0;k<chains.Count;k++){
                var c=chains[k];int n=c.joints.Length;constraints.chains[k]=new TealWitchHairConstraintJob.Chain{start=start,count=n+1,rootVelocity=(c.frameAnchor-c.anchor)/Mathf.Max(.001f,dt)};
                for(int i=0;i<=n;i++){
                    int p=start+i;constraints.points[p]=c.points[i];constraints.previous[p]=c.previous[i];constraints.velocities[p]=c.velocities[i];
                    constraints.lengths[p]=i>0?c.lengths[i-1]:0;constraints.bends[p]=i<n-1?(c.restVectors[i]+c.restVectors[i+1]).magnitude*c.scale:0;
                    constraints.radii[p]=n<=2||c.referencePoints[i].y>1.50f?-1:strandRadius*Mathf.Lerp(.55f,1,Mathf.Sin((float)i/n*Mathf.PI));
                    if(k+1<chains.Count&&chains[k+1].points.Length==c.points.Length)constraints.widths[p]=skin.transform.TransformVector(chains[k+1].referencePoints[i]-c.referencePoints[i]).magnitude;
                }
                start+=n+1;
            }
            for(int i=0;i<volumes.Count;i++){
                var v=volumes[i];var velocity=Matrix4x4.TRS(v.currentCenter,v.currentRotation,Vector3.one);var old=Matrix4x4.TRS(v.lastCenter,v.lastRotation,Vector3.one);for(int j=0;j<16;j++)velocity[j]=(velocity[j]-old[j])/Mathf.Max(.001f,dt);
                constraints.volumes[i]=new TealWitchHairConstraintJob.Volume{center=v.solverCenter,radii=v.worldRadii,rotation=v.solverRotation,inverse=v.solverInverse,velocity=velocity};
            }
            for(int i=0;i<8;i++){constraints.backPoints[i]=solverBackPoints[i];constraints.backNormals[i]=solverBackNormals[i];}
            var headFrame=Matrix4x4.TRS(Vector3.Lerp(lastHatFrame.GetColumn(3),(Vector3)HatContact.toWorld.c3.xyz,alpha),Quaternion.Slerp(lastHatFrame.rotation,((Matrix4x4)HatContact.toWorld).rotation,alpha),Vector3.one);
            var hat=HatContact;hat.toWorld=headFrame;hat.toLocal=math.inverse(hat.toWorld);hat.velocity=(HatContact.toWorld-(float4x4)lastHatFrame)/Mathf.Max(.001f,dt);constraints.hat=hat;
            constraints.iterations=constraintIterations;constraints.bendStrength=bendStiffness;constraints.Run();
            start=0;foreach(var c in chains){for(int i=0;i<c.points.Length;i++){c.points[i]=constraints.points[start+i];c.velocities[i]=constraints.velocities[start+i];if(math.lengthsq(constraints.normals[start+i])>.01f)CollisionCorrections++;}start+=c.points.Length;}

        }
        CurrentLiftDegrees=0;
        foreach(var c in chains){c.anchor=c.frameAnchor;c.rootDirection=c.frameDirection;Apply(c);if(c.joints.Length>2)CurrentLiftDegrees=Mathf.Max(CurrentLiftDegrees,Vector3.Angle(Vector3.down,c.points[c.points.Length-1]-c.points[0]));}
        lastHatFrame=HatContact.toWorld;foreach(var v in volumes){v.lastCenter=v.currentCenter;v.lastRotation=v.currentRotation;}
    }
    readonly Vector3[] oldBackPoints=new Vector3[8],oldBackNormals=new Vector3[8],solverBackPoints=new Vector3[8],solverBackNormals=new Vector3[8];
    void SampleContacts(float alpha)
    {
        foreach(var v in volumes){v.solverCenter=Vector3.Lerp(v.lastCenter,v.currentCenter,alpha);v.solverRotation=Quaternion.Slerp(v.lastRotation,v.currentRotation,alpha);v.solverInverse=Quaternion.Inverse(v.solverRotation);}
        for(int i=0;i<8;i++){solverBackPoints[i]=Vector3.Lerp(oldBackPoints[i],backPoints[i],alpha);solverBackNormals[i]=Vector3.Slerp(oldBackNormals[i],backNormals[i],alpha);}
    }
    void Predict(Chain c,Vector3 anchor,Vector3 direction)
    {
        int n=c.joints.Length;bool fringe=n<=2;float drag=Mathf.Exp(-damping*Step);
        for(int i=0;i<=n;i++){c.previous[i]=c.points[i];c.contactNormals[i]=c.contactVelocities[i]=Vector3.zero;}
        c.points[0]=anchor;
        for(int i=1;i<=n;i++){
            float t=(float)i/n;var shape=(c.goals[i]-c.points[i])*shapeSpring*Mathf.Lerp(1,.12f,t);
            c.velocities[i]=c.velocities[i]*drag+(Vector3.down*gravity+Vector3.ClampMagnitude(shape,12))*Step;
            c.points[i]+=Vector3.ClampMagnitude(c.velocities[i],10)*Step;
        }
        c.points[1]=Vector3.Lerp(c.points[1],anchor+direction*c.lengths[0],fringe?.85f:rootStiffness);
    }
    Vector3 Goal(Chain c,int i)
    {
        var attached=c.joints[0].parent.TransformPoint(c.parentRestPoints[i]);
        if(c.joints.Length<=2)return attached;
        var p=c.referencePoints[i];var torso=TorsoPoint(p);
        float follow=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.52f,1.20f,p.y));
        return Vector3.Lerp(attached,torso,follow*.85f);
    }
    Matrix4x4 chestFrame,hipFrame;
    readonly Vector3[] backPoints=new Vector3[8],backNormals=new Vector3[8];
    internal int ContactVolumeCount {get {if(!ready)Rebuild();return volumes.Count;}}
    internal void CopyContacts(Unity.Collections.NativeArray<TealWitchSurfaceJob.Volume> destination,Unity.Collections.NativeArray<Unity.Mathematics.float3> points,Unity.Collections.NativeArray<Unity.Mathematics.float3> normals)
    {
        for(int i=0;i<volumes.Count;i++){var v=volumes[i];destination[i]=new TealWitchSurfaceJob.Volume{center=v.currentCenter,radii=v.worldRadii,rotation=v.currentRotation,inverse=v.inverseRotation};}
        for(int i=0;i<8;i++){points[i]=backPoints[i];normals[i]=backNormals[i];}
    }
    Vector3 TorsoPoint(Vector3 p)=>Vector3.Lerp(hipFrame.MultiplyPoint3x4(p),chestFrame.MultiplyPoint3x4(p),Mathf.InverseLerp(1.02f,1.32f,p.y));
    public Vector3 ResolveSurfacePoint(Vector3 point,float clearance=.006f,bool stayBehind=false)
    {
        for(int pass=0;pass<16;pass++){bool changed=false;if(stayBehind){var rear=ResolveBack(point,clearance);changed=(rear-point).sqrMagnitude>1e-10f;point=rear;}foreach(var v in volumes){var r=v.worldRadii+Vector3.one*clearance;float bound=Mathf.Max(r.x,Mathf.Max(r.y,r.z));if((point-v.currentCenter).sqrMagnitude>bound*bound)continue;var local=v.inverseRotation*(point-v.currentCenter);var q=new Vector3(local.x/r.x,local.y/r.y,local.z/r.z);if(q.sqrMagnitude>=.99999f)continue;q=q.sqrMagnitude<1e-8f?Vector3.back:q.normalized;point=v.currentCenter+v.currentRotation*Vector3.Scale(q,r);changed=true;}var hatPoint=(Vector3)HatContact.Resolve(point,clearance);changed|=(hatPoint-point).sqrMagnitude>1e-10f;point=hatPoint;if(!changed)break;}
        return point;
    }
    Vector3 ResolveBack(Vector3 point,float clearance)=>ResolveBackAt(point,clearance,backPoints,backNormals);
    Vector3 ResolveBackAt(Vector3 point,float clearance,Vector3[] backPoints,Vector3[] backNormals)
    {
        // A rear lock must stay on the back side when a fast turn carries it through the torso.
        // Nearest-side ellipsoid projection alone can incorrectly release it through the front.
        float best=float.PositiveInfinity;Vector3 bestPoint=point;
        for(int i=0;i<7;i++){
            float y=1.00f+i*.06f;var a=backPoints[i];var b=backPoints[i+1];
            var axis=b-a;float raw=Vector3.Dot(point-a,axis)/Mathf.Max(1e-8f,axis.sqrMagnitude);if(raw<0||raw>1)continue;
            var q=Vector3.Lerp(a,b,raw);var normal=Vector3.Lerp(backNormals[i],backNormals[i+1],raw).normalized;
            var delta=point-q;float gap=Vector3.Dot(delta,normal);var lateral=delta-normal*gap;
            float width=Mathf.Lerp(.185f,.13f,Mathf.InverseLerp(1.02f,1.20f,y));
            if(lateral.magnitude>width||gap<-.32f||gap>=clearance)continue;
            float distance=lateral.sqrMagnitude;if(distance<best){best=distance;bestPoint=point+normal*(clearance-gap);}
        }
        return bestPoint;
    }
    void Apply(Chain c)
    {
        
        Vector3 offset=c.joints[0].position-c.points[0];
        for(int i=0;i<c.joints.Length;i++){
            Transform joint=c.joints[i];var parent=c.joints[0].parent;
            var restDirection=parent.TransformVector(c.parentRestPoints[i+1]-c.parentRestPoints[i]);
            var desired=c.points[i+1]+offset-joint.position;
            if(restDirection.sqrMagnitude>1e-8f&&desired.sqrMagnitude>1e-8f)joint.rotation=Quaternion.FromToRotation(restDirection,desired)*parent.rotation*c.referenceRotations[i];

        }
        var tip=c.joints[c.joints.Length-1].TransformPoint(c.localTip);Vector3 straight=c.frameAnchor;foreach(var d in c.restVectors)straight+=skin.transform.TransformVector(d)*c.scale;MaximumTipLag=Mathf.Max(MaximumTipLag,Vector3.Distance(tip,straight));
    }


    void OnDisable(){Restore();constraints.Dispose();constraints=default;if(HatContact.heights.IsCreated)HatContact.heights.Dispose();HatContact=default;ready=false;}
}
