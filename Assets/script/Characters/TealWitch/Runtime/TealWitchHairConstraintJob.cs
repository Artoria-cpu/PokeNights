using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(CompileSynchronously=true)]
internal struct TealWitchHairConstraintJob : IJob
{
    internal struct Chain {public int start,count;public float3 rootVelocity;}
    internal struct Volume {public float3 center,radii;public quaternion rotation,inverse;public float4x4 velocity;}
    public NativeArray<float3> points,velocities,normals,contactVelocities;
    [ReadOnly] public NativeArray<float3> previous;
    [ReadOnly] public NativeArray<float> lengths,bends,widths,radii;
    [ReadOnly] public NativeArray<Chain> chains;
    [ReadOnly] public NativeArray<Volume> volumes;
    [ReadOnly] public NativeArray<float3> backPoints,backNormals;
    public TealWitchHatContact hat;
    public int iterations;
    public float bendStrength;
    public void Execute()
    {
        for(int i=0;i<points.Length;i++){normals[i]=0;contactVelocities[i]=0;}
        for(int pass=0;pass<iterations;pass++){
            for(int k=0;k<chains.Length;k++){
                var c=chains[k];int start=c.start,n=c.count-1;
                for(int i=0;i<n-1;i++)Distance(start,start+i,start+i+2,bends[start+i],bendStrength);
                for(int i=n-1;i>=0;i--)Distance(start,start+i,start+i+1,lengths[start+i+1],1);
                for(int i=0;i<n;i++)Distance(start,start+i,start+i+1,lengths[start+i+1],1);
                for(int i=1;i<n;i++){
                    var a=math.normalizesafe(points[start+i]-points[start+i-1]);var b=math.normalizesafe(points[start+i+1]-points[start+i]);
                    float angle=math.acos(math.clamp(math.dot(a,b),-1,1)),limit=math.radians(48f);
                    if(angle>limit){float t=limit/angle;float sin=math.sin(angle);var direction=math.abs(sin)>1e-5f?(a*math.sin((1-t)*angle)+b*math.sin(t*angle))/sin:math.normalizesafe(math.lerp(a,b,t));points[start+i+1]=points[start+i]+direction*lengths[start+i+1];}
                }
                for(int i=1;i<=n;i++)Collide(start+i,c.rootVelocity);
            }
            for(int k=0;k<chains.Length-1;k++){
                var a=chains[k];var b=chains[k+1];if(a.count<=3||b.count!=a.count)continue;
                for(int i=1;i<a.count;i++){var delta=points[b.start+i]-points[a.start+i];float length=math.length(delta);if(length<1e-6f)continue;var correction=delta*((length-widths[a.start+i])/length)*.06f;points[a.start+i]+=correction*.5f;points[b.start+i]-=correction*.5f;}
            }
        }
        for(int k=0;k<chains.Length;k++){
            var c=chains[k];for(int i=1;i<c.count;i++){
                int p=c.start+i;
                for(int pass=0;pass<6;pass++){points[p]=points[p-1]+math.normalizesafe(points[p]-points[p-1])*lengths[p];if(!Collide(p,c.rootVelocity))break;}
                points[p]=points[p-1]+math.normalizesafe(points[p]-points[p-1])*lengths[p];
                var velocity=(points[p]-previous[p])*120;
                if(math.lengthsq(normals[p])>.01f){var normal=math.normalize(normals[p]);velocity-=normal*math.dot(velocity-contactVelocities[p],normal);}
                float square=math.lengthsq(velocity);velocities[p]=square>100?velocity*(10*math.rsqrt(square)):velocity;
            }
        }
    }
    void Distance(int root,int a,int b,float rest,float strength)
    {
        var delta=points[b]-points[a];float length=math.length(delta);if(length<1e-7f)return;var correction=delta*((length-rest)/length)*strength;
        if(a==root)points[b]-=correction;else{points[a]+=correction*.5f;points[b]-=correction*.5f;}
    }
    bool Collide(int i,float3 rootVelocity)
    {
        float radius=radii[i];if(radius<0)return false;var point=points[i];var original=point;
        float best=float.PositiveInfinity;float3 back=point;
        for(int k=0;k<7;k++){
            var a=backPoints[k];var axis=backPoints[k+1]-a;float t=math.dot(point-a,axis)/math.max(1e-8f,math.lengthsq(axis));if(t<0||t>1)continue;
            var q=math.lerp(a,backPoints[k+1],t);var normal=math.normalizesafe(math.lerp(backNormals[k],backNormals[k+1],t));var delta=point-q;float gap=math.dot(delta,normal);float lateral=math.lengthsq(delta-normal*gap);
            float width=math.lerp(.185f,.13f,math.saturate((1+k*.06f-1.02f)/.18f));if(lateral>width*width||gap<-.32f||gap>=radius)continue;
            if(lateral<best){best=lateral;back=point+normal*(radius-gap);}
        }
        if(math.lengthsq(back-point)>1e-10f){normals[i]+=math.normalize(back-point);contactVelocities[i]=rootVelocity;point=back;}
        for(int k=0;k<volumes.Length;k++){
            var v=volumes[k];var r=v.radii+radius;float bound=math.cmax(r);if(math.lengthsq(point-v.center)>bound*bound)continue;
            var q=math.rotate(v.inverse,point-v.center)/r;float sq=math.lengthsq(q);if(sq>=1)continue;q=sq<1e-8f?new float3(0,0,-1):q*math.rsqrt(sq);
            var surface=q*r;normals[i]+=math.rotate(v.rotation,math.normalizesafe(q/r));contactVelocities[i]=math.transform(v.velocity,surface);point=v.center+math.rotate(v.rotation,surface);
        }
        var hatPoint=hat.Resolve(point,math.min(radius,.018f));
        if(math.lengthsq(hatPoint-point)>1e-10f){normals[i]+=math.normalize(hatPoint-point);contactVelocities[i]=math.transform(hat.velocity,math.transform(hat.toLocal,point));point=hatPoint;}
        points[i]=point;return math.lengthsq(point-original)>1e-10f;
    }
    public void Create(int particles,int chainCount,int volumeCount)
    {
        const Allocator a=Allocator.Persistent;
        points=new NativeArray<float3>(particles,a);velocities=new NativeArray<float3>(particles,a);normals=new NativeArray<float3>(particles,a);contactVelocities=new NativeArray<float3>(particles,a);previous=new NativeArray<float3>(particles,a);
        lengths=new NativeArray<float>(particles,a);bends=new NativeArray<float>(particles,a);widths=new NativeArray<float>(particles,a);radii=new NativeArray<float>(particles,a);chains=new NativeArray<Chain>(chainCount,a);volumes=new NativeArray<Volume>(volumeCount,a);backPoints=new NativeArray<float3>(8,a);backNormals=new NativeArray<float3>(8,a);
    }
    public void Dispose()
    {
        if(!points.IsCreated)return;
        points.Dispose();velocities.Dispose();normals.Dispose();contactVelocities.Dispose();previous.Dispose();lengths.Dispose();bends.Dispose();widths.Dispose();radii.Dispose();chains.Dispose();volumes.Dispose();backPoints.Dispose();backNormals.Dispose();
    }
}
