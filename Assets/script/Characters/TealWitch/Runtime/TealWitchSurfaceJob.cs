using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

// Same skinning and contact rules as the character's original CPU solver.
// A synchronous Burst job avoids managed per-vertex math without delaying contacts.
[BurstCompile(CompileSynchronously = true)]
internal struct TealWitchSurfaceJob : IJob
{
    internal struct Volume { public float3 center, radii; public quaternion rotation, inverse; }
    [ReadOnly] public NativeArray<Vector3> rest;
    [ReadOnly] public NativeArray<BoneWeight> weights;
    [ReadOnly] public NativeArray<float4x4> matrices;
    [ReadOnly] public NativeArray<float4> rotations, duals;
    [ReadOnly] public NativeArray<int> triangles, weld;
    [ReadOnly] public NativeArray<float> angles;
    [ReadOnly] public NativeArray<Volume> volumes;
    [ReadOnly] public NativeArray<float3> backPoints, backNormals;
    public NativeArray<Vector3> output;
    public NativeArray<float3> posed, corrections;
    public NativeArray<int> counts;
    public NativeArray<float4x4> inverse;
    public NativeArray<float3> metrics;
    public float4x4 local, world, hipWorld, worldHip, hipBind;
    public TealWitchHatContact hat;
    public bool skirt, hair, contacts;

    public void Execute()
    {
        float maxSkirt=0,maxSleeve=0,maxLength=0;
        for(int i=0;i<rest.Length;i++){
            float3 p=rest[i];var w=weights[i];bool leg=!skirt&&!hair&&p.y<1.015f;
            float sleeve=skirt?0:Smooth(Inv(.10f,.18f,math.abs(p.x)))*Inv(.53f,.49f,math.abs(p.x));
            if(!leg&&!skirt&&!hair&&(p.y<1.17f||p.y>1.375f||sleeve<=0)){output[i]=p;continue;}
            var m=matrices[w.boneIndex0]*w.weight0+matrices[w.boneIndex1]*w.weight1+matrices[w.boneIndex2]*w.weight2+matrices[w.boneIndex3]*w.weight3;
            float3 current=math.transform(m,p),target=current;
            if(skirt&&p.y>=1.055f)target=math.transform(local,math.transform(hipWorld,math.transform(hipBind,p)));
            else if(skirt){
                float index=math.frac(math.atan2(p.x/.18f,(p.z-.012f)/.14f)/(2*math.PI))*24;
                int a=(int)math.floor(index),b=(a+1)%24;float angle=math.lerp(angles[a],angles[b],index-a);
                float3 radial=math.normalizesafe(new float3(p.x,0,p.z-.012f));
                float scale=math.rsqrt(radial.x*radial.x/(.112f*.112f)+radial.z*radial.z/(.09f*.09f));
                float3 anchor=new float3(radial.x*scale,1.075f,.012f+radial.z*scale);
                float free=Smooth(Inv(1.055f,1.005f,p.y));
                var rotation=quaternion.AxisAngle(math.normalizesafe(math.cross(radial,new float3(0,1,0))),math.radians(angle*free));
                var h=math.transform(hipBind,anchor+math.rotate(rotation,p-anchor));
                float length=math.distance(h,math.transform(hipBind,anchor))/math.max(.001f,math.distance(math.transform(hipBind,p),math.transform(hipBind,anchor)));
                maxLength=math.max(maxLength,length);
                target=math.transform(local,math.transform(hipWorld,h));maxSkirt=math.max(maxSkirt,math.distance(current,target));
            }else if(hair){
                if(p.y<1.48f&&contacts)target=math.transform(local,Resolve(math.transform(world,current),.006f,p.z<-.025f));
            }else{
                target=math.lerp(current,DualPoint(w,p),leg?1:sleeve);maxSleeve=math.max(maxSleeve,math.distance(current,target));
            }
            var inv=math.inverse(m);output[i]=math.transform(inv,target);
            if(hair){inverse[i]=inv;posed[i]=math.transform(worldHip,math.transform(world,target));}
        }
        if(hair&&contacts)ResolveFaces();
        metrics[0]=new float3(maxSkirt,maxSleeve,maxLength);
    }
    static float Inv(float a,float b,float v)=>math.saturate((v-a)/(b-a));
    static float Smooth(float v)=>v*v*(3-2*v);
    float3 DualPoint(BoneWeight w,float3 p)
    {
        float4 reference=rotations[w.boneIndex0],r=reference*w.weight0,d=duals[w.boneIndex0]*w.weight0;
        Accumulate(w.boneIndex1,w.weight1,reference,ref r,ref d);Accumulate(w.boneIndex2,w.weight2,reference,ref r,ref d);Accumulate(w.boneIndex3,w.weight3,reference,ref r,ref d);
        float length=math.length(r);if(length<1e-6f)return p;r/=length;d/=length;
        var translation=math.mul(new quaternion(d),math.inverse(new quaternion(r))).value;
        return math.rotate(new quaternion(r),p)+translation.xyz*2;
    }
    void Accumulate(int i,float weight,float4 reference,ref float4 r,ref float4 d)
    {if(weight<=0)return;if(math.dot(reference,rotations[i])<0)weight=-weight;r+=rotations[i]*weight;d+=duals[i]*weight;}
    float3 Resolve(float3 point,float margin,bool behind)
    {
        for(int pass=0;pass<16;pass++){
            bool changed=false;
            if(behind){var rear=ResolveBack(point,margin);changed=math.lengthsq(rear-point)>1e-10f;point=rear;}
            for(int i=0;i<volumes.Length;i++){
                var v=volumes[i];float3 r=v.radii+margin;float bound=math.cmax(r);
                if(math.lengthsq(point-v.center)>bound*bound)continue;
                var q=math.rotate(v.inverse,point-v.center)/r;float square=math.lengthsq(q);
                if(square>=.99999f)continue;q=square<1e-8f?new float3(0,0,-1):q*math.rsqrt(square);
                point=v.center+math.rotate(v.rotation,q*r);changed=true;
            }
            var hatPoint=hat.Resolve(point,margin);changed|=math.lengthsq(hatPoint-point)>1e-10f;point=hatPoint;
            if(!changed)break;
        }
        return point;
    }
    float3 ResolveBack(float3 point,float margin)
    {
        float best=float.PositiveInfinity;float3 result=point;
        for(int i=0;i<7;i++){
            var a=backPoints[i];var axis=backPoints[i+1]-a;float raw=math.dot(point-a,axis)/math.max(1e-8f,math.lengthsq(axis));
            if(raw<0||raw>1)continue;
            var q=math.lerp(a,backPoints[i+1],raw);var normal=math.normalizesafe(math.lerp(backNormals[i],backNormals[i+1],raw));
            var delta=point-q;float gap=math.dot(delta,normal);var lateral=delta-normal*gap;
            float width=math.lerp(.185f,.13f,Inv(1.02f,1.20f,1+i*.06f));
            float distance=math.lengthsq(lateral);if(distance>width*width||gap<-.32f||gap>=margin)continue;
            if(distance<best){best=distance;result=point+normal*(margin-gap);}
        }
        return result;
    }
    void ResolveFaces()
    {
        for(int i=0;i<counts.Length;i++){counts[i]=0;corrections[i]=0;}
        bool changed=false;
        for(int i=0;i<triangles.Length;i+=3){
            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
            if(rest[a].y>=1.48f||rest[b].y>=1.48f||rest[c].y>=1.48f)continue;
            var p=(posed[a]+posed[b]+posed[c])/3;var point=math.transform(hipWorld,p);
            var delta=math.rotate(worldHip,Resolve(point,.004f,(rest[a].z+rest[b].z+rest[c].z)/3<-.025f)-point);
            if(math.lengthsq(delta)<1e-9f)continue;
            for(int corner=0;corner<3;corner++){int group=weld[corner==0?a:corner==1?b:c];corrections[group]+=delta;counts[group]++;}changed=true;
        }
        if(!changed)return;
        var toRenderer=math.mul(local,hipWorld);
        for(int i=0;i<posed.Length;i++){
            int group=weld[i];if(counts[group]>0&&rest[i].y<1.48f)posed[i]+=corrections[group]/counts[group];
            if(rest[i].y<1.48f)posed[i]=math.transform(worldHip,Resolve(math.transform(hipWorld,posed[i]),.006f,rest[i].z<-.025f));
            output[i]=math.transform(inverse[i],math.transform(toRenderer,posed[i]));
        }
    }
    public static TealWitchSurfaceJob Create(TealWitchSurfaceDeformation.Surface s)
    {
        const Allocator a=Allocator.Persistent;
        var job=new TealWitchSurfaceJob{
            rest=new NativeArray<Vector3>(s.rest,a),weights=new NativeArray<BoneWeight>(s.weights,a),
            matrices=new NativeArray<float4x4>(s.bind.Length,a),rotations=new NativeArray<float4>(s.bind.Length,a),duals=new NativeArray<float4>(s.bind.Length,a),
            triangles=new NativeArray<int>(s.hair?s.triangles:System.Array.Empty<int>(),a),weld=new NativeArray<int>(s.hair?s.weld:System.Array.Empty<int>(),a),
            output=new NativeArray<Vector3>(s.rest.Length,a),posed=new NativeArray<float3>(s.hair?s.rest.Length:0,a),inverse=new NativeArray<float4x4>(s.hair?s.rest.Length:0,a),
            corrections=new NativeArray<float3>(s.hair?s.weldCount:0,a),counts=new NativeArray<int>(s.hair?s.weldCount:0,a),metrics=new NativeArray<float3>(1,a),skirt=s.skirt,hair=s.hair
        };return job;
    }
    public void Dispose()
    {
        if(!rest.IsCreated)return;
        rest.Dispose();weights.Dispose();matrices.Dispose();rotations.Dispose();duals.Dispose();triangles.Dispose();weld.Dispose();output.Dispose();posed.Dispose();inverse.Dispose();corrections.Dispose();counts.Dispose();metrics.Dispose();
    }
}
