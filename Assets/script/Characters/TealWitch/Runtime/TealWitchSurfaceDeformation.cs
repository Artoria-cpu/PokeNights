using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>Preserves sleeve volume and keeps the complete skirt outside the swept thigh surfaces.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10040)]
public sealed class TealWitchSurfaceDeformation : MonoBehaviour
{
    [Serializable] public class Surface
    {
        public SkinnedMeshRenderer renderer;
        public bool skirt;
        public bool hair;
        [NonSerialized] internal Mesh original, working;
        [NonSerialized] internal Vector3[] rest;
        [NonSerialized] internal BoneWeight[] weights;
        [NonSerialized] internal Matrix4x4[] bind;
        [NonSerialized] internal int[] triangles,weld;
        [NonSerialized] internal int weldCount;
        [NonSerialized] internal Transform[] bones;
        [NonSerialized] internal TealWitchSurfaceJob job;
    }
    public Surface[] surfaces;
    public Transform hips,leftThigh,leftKnee,rightThigh,rightKnee;
    public float thighRadius=.076f, kneeRadius=.060f, clearance=.016f;
    public float MaximumSkirtCorrection {get;private set;}
    public float MaximumSleeveCorrection {get;private set;}
    public int CollisionCorrections {get;private set;}
    public float MaximumPanelLengthRatio {get;private set;}
    public float MaximumEdgeRatio {get;private set;}
    TealWitchHairDynamics hairDynamics;
    NativeArray<float> jobAngles;
    NativeArray<TealWitchSurfaceJob.Volume> jobVolumes;
    NativeArray<float3> jobBackPoints,jobBackNormals;
    Matrix4x4 hipBind;
    struct LegSection {public float x,z,rx,rz;}
    LegSection[][] legSections;
    readonly Matrix4x4[] legToHip=new Matrix4x4[2],hipToLeg=new Matrix4x4[2];
    readonly int[] legBone=new int[2];
    const int PanelCount=24;
    struct PanelProbe {public Vector3 point;public float initialDepth;}
    List<PanelProbe>[] panelProbes;
    readonly float[] panelAngles=new float[PanelCount],panelVelocity=new float[PanelCount];
    bool bindReady;
    void OnEnable(){Initialize();}
    void Initialize()
    {
        if(surfaces==null)return;
        foreach(var s in surfaces){
            if(s.renderer==null||s.working!=null)continue;
            s.original=s.renderer.sharedMesh;s.working=Instantiate(s.original);s.working.name=s.original.name+" (deformed)";s.working.MarkDynamic();
            s.rest=s.original.vertices;s.weights=s.original.boneWeights;s.bind=s.original.bindposes;s.bones=s.renderer.bones;s.renderer.sharedMesh=s.working;
            if(s.hair){
                s.triangles=s.original.triangles;s.weld=new int[s.rest.Length];var groups=new Dictionary<Vector3Int,int>();
                for(int i=0;i<s.rest.Length;i++){var p=s.rest[i]*100000;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));if(!groups.TryGetValue(key,out int group)){group=groups.Count;groups.Add(key,group);}s.weld[i]=group;}
                s.weldCount=groups.Count;
            }
        }
        if(hairDynamics==null)hairDynamics=GetComponent<TealWitchHairDynamics>();
    }
    void LateUpdate(){Deform();}
    public void Deform()
    {
        if(surfaces==null||hips==null)return;Initialize();var hipWorld=hips.localToWorldMatrix;var worldHip=hips.worldToLocalMatrix;
        if(!bindReady){var s=surfaces[0];hipBind=s.bind[Array.IndexOf(s.bones,hips)];bindReady=true;}
        if(legSections==null)BuildLegSections();
        for(int side=0;side<2;side++){var bone=side==0?leftThigh:rightThigh;legToHip[side]=worldHip*bone.localToWorldMatrix*surfaces[0].bind[legBone[side]];hipToLeg[side]=legToHip[side].inverse;}
        SolveSkirtPanels();
        if(!jobAngles.IsCreated){jobAngles=new NativeArray<float>(PanelCount,Allocator.Persistent);jobVolumes=new NativeArray<TealWitchSurfaceJob.Volume>(hairDynamics!=null?hairDynamics.ContactVolumeCount:0,Allocator.Persistent);jobBackPoints=new NativeArray<float3>(8,Allocator.Persistent);jobBackNormals=new NativeArray<float3>(8,Allocator.Persistent);}
        jobAngles.CopyFrom(panelAngles);if(hairDynamics!=null)hairDynamics.CopyContacts(jobVolumes,jobBackPoints,jobBackNormals);
        foreach(var s in surfaces){
            if(s.working==null)continue;
            if(!s.job.rest.IsCreated)s.job=TealWitchSurfaceJob.Create(s);
            var local=s.renderer.transform.worldToLocalMatrix;
            if(s.bones==null)s.bones=s.renderer.bones;
            var job=s.job;
            for(int i=0;i<s.bind.Length;i++){
                var m=local*s.bones[i].localToWorldMatrix*s.bind[i];job.matrices[i]=m;
                var r=m.rotation;job.rotations[i]=new float4(r.x,r.y,r.z,r.w);
                var p=m.GetColumn(3);var dual=new Quaternion(p.x,p.y,p.z,0)*r;job.duals[i]=new float4(dual.x,dual.y,dual.z,dual.w)*.5f;
            }
            job.local=local;job.world=s.renderer.transform.localToWorldMatrix;job.hipWorld=hipWorld;job.worldHip=worldHip;job.hipBind=hipBind;
            job.angles=jobAngles;job.volumes=jobVolumes;job.backPoints=jobBackPoints;job.backNormals=jobBackNormals;job.contacts=hairDynamics!=null;
            job.hat=hairDynamics!=null?hairDynamics.HatContact:default;
            job.Run();s.working.SetVertices(job.output);
            var metrics=job.metrics[0];MaximumSkirtCorrection=Mathf.Max(MaximumSkirtCorrection,metrics.x);MaximumSleeveCorrection=Mathf.Max(MaximumSleeveCorrection,metrics.y);MaximumPanelLengthRatio=Mathf.Max(MaximumPanelLengthRatio,metrics.z);
        }
    }
    Vector3 PanelPoint(Vector3 p,float angle)
    {
        var radial=new Vector3(p.x,0,p.z-.012f).normalized;
        float scale=1/Mathf.Sqrt(radial.x*radial.x/(.112f*.112f)+radial.z*radial.z/(.09f*.09f));
        var anchor=new Vector3(radial.x*scale,1.075f,.012f+radial.z*scale);
        float free=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.055f,1.005f,p.y));
        var rotation=Quaternion.AngleAxis(angle*free,Vector3.Cross(radial,Vector3.up));
        return hipBind.MultiplyPoint3x4(anchor+rotation*(p-anchor));
    }
    float PanelPenetration(int index,float angle)
    {
        float max=0;
        foreach(var probe in panelProbes[index]){
            var point=PanelPoint(probe.point,angle);float depth=Contact(point,clearance).magnitude;
            max=Mathf.Max(max,depth-probe.initialDepth);
        }
        return max;
    }
    void SolveSkirtPanels()
    {
        if(panelProbes==null){panelProbes=new List<PanelProbe>[PanelCount];for(int k=0;k<PanelCount;k++)panelProbes[k]=new List<PanelProbe>();
            foreach(var s in surfaces)if(s.skirt)for(int i=0;i<s.rest.Length;i++){var p=s.rest[i];if(p.y>=1.055f)continue;float f=Mathf.Repeat(Mathf.Atan2(p.x/.18f,(p.z-.012f)/.14f)/(2*Mathf.PI),1)*PanelCount;int a=Mathf.FloorToInt(f);float initial=0;for(int side=0;side<2;side++)initial=Mathf.Max(initial,(LegContactRoot(side,p,clearance)-p).magnitude);var probe=new PanelProbe{point=p,initialDepth=initial};panelProbes[a].Add(probe);panelProbes[(a+1)%PanelCount].Add(probe);}
            for(int k=0;k<PanelCount;k++)if(panelProbes[k].Count>64){var original=panelProbes[k];var reduced=new List<PanelProbe>();for(int i=0;i<64;i++)reduced.Add(original[Mathf.RoundToInt(i*(original.Count-1)/63f)]);panelProbes[k]=reduced;}
        }
        for(int k=0;k<PanelCount;k++){
            float current=panelAngles[k],target=current;
            float currentDepth=PanelPenetration(k,current);
            // Reuse the current-angle query; a resting panel previously tested
            // the exact same probes three times in a single frame.
            if(currentDepth>.003f){float least=currentDepth;for(float a=current+3;a<=115;a+=3){float depth=PanelPenetration(k,a);if(depth<least){least=depth;target=a;}if(depth<=.003f)break;}}
            else {for(float a=current-3;a>=0;a-=3){if(PanelPenetration(k,a)>.003f)break;target=a;}if(target>0&&target<3&&PanelPenetration(k,0)<=.003f)target=0;}
            if(target>current)panelAngles[k]=target;else panelAngles[k]=Mathf.SmoothDamp(current,target,ref panelVelocity[k],.16f,360,Mathf.Max(.016f,Mathf.Min(Time.deltaTime,.05f)));
        }
    }
    void BuildLegSections()
    {
        var body=surfaces[0];var bones=body.renderer.bones;legBone[0]=Array.IndexOf(bones,leftThigh);legBone[1]=Array.IndexOf(bones,rightThigh);legSections=new LegSection[2][];
        for(int side=0;side<2;side++){legSections[side]=new LegSection[9];for(int k=0;k<9;k++){
            float y=.54f+k*.04625f,minX=10,maxX=-10,minZ=10,maxZ=-10;
            foreach(var p in body.rest)if((side==0?p.x<0:p.x>0)&&Mathf.Abs(p.y-y)<.026f){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
            legSections[side][k]=new LegSection{x=(minX+maxX)*.5f,z=(minZ+maxZ)*.5f,rx=(maxX-minX)*.5f,rz=(maxZ-minZ)*.5f};
        }}
    }
    Vector3 LegContactRoot(int side,Vector3 p,float margin)
    {
        float y=Mathf.Clamp(p.y,.54f,.91f),cap=Mathf.Abs(p.y-y);if(cap>.045f)return p;
        float f=(y-.54f)/.04625f;int a=Mathf.Clamp(Mathf.FloorToInt(f),0,8),b=Mathf.Min(8,a+1);float t=f-a;var l=legSections[side][a];var r=legSections[side][b];
        float taper=Mathf.Sqrt(Mathf.Max(0,1-cap*cap/(.045f*.045f)));float rx=Mathf.Lerp(l.rx,r.rx,t)*taper+margin,rz=Mathf.Lerp(l.rz,r.rz,t)*taper+margin;
        float cx=Mathf.Lerp(l.x,r.x,t),cz=Mathf.Lerp(l.z,r.z,t);var q=new Vector2((p.x-cx)/rx,(p.z-cz)/rz);if(q.sqrMagnitude>=1)return p;
        q=q.sqrMagnitude<1e-8f?Vector2.up:q.normalized;p.x=cx+q.x*rx;p.z=cz+q.y*rz;return p;
    }
    Vector3 Contact(Vector3 point,float margin)
    {
        var result=point;for(int side=0;side<2;side++){var p=hipToLeg[side].MultiplyPoint3x4(result);result=legToHip[side].MultiplyPoint3x4(LegContactRoot(side,p,margin));}return result-point;
    }
    void OnDisable()
    {
        if(surfaces==null)return;foreach(var s in surfaces){if(s.original!=null&&s.renderer!=null)s.renderer.sharedMesh=s.original;if(s.working!=null){if(Application.isPlaying)Destroy(s.working);else DestroyImmediate(s.working);}s.working=null;s.job.Dispose();s.job=default;}
        if(jobAngles.IsCreated){jobAngles.Dispose();jobVolumes.Dispose();jobBackPoints.Dispose();jobBackNormals.Dispose();}
    }
}

