using Unity.Collections;
using Unity.Mathematics;

// The lower envelope of the actual tilted brim, in the hat's bind frame.
// Shared by strand particles and skin contacts so their collision shapes agree.
internal struct TealWitchHatContact
{
    [ReadOnly] public NativeArray<float> heights;
    public float4 domain;
    public int size;
    public float minimumHeight;
    public float4x4 toWorld, toLocal, velocity;

    public float3 Resolve(float3 worldPoint, float margin)
    {
        if(size<2 || heights.Length!=size*size)return worldPoint;
        // Most hair is well below the brim. Reject against its lowest plane
        // before transforming X/Z or touching the height samples.
        float y=toLocal.c0.y*worldPoint.x+toLocal.c1.y*worldPoint.y+toLocal.c2.y*worldPoint.z+toLocal.c3.y;
        if(y<minimumHeight-margin)return worldPoint;
        float3 p=math.transform(toLocal,worldPoint);
        float2 cell=(p.xz-domain.xy)/domain.zw*(size-1);
        if(math.any(cell<0)||math.any(cell>=size-1))return worldPoint;
        int2 q=(int2)math.floor(cell);float2 t=cell-q;int i=q.y*size+q.x;
        float a=heights[i],b=heights[i+1],c=heights[i+size],d=heights[i+size+1];
        if(math.cmin(new float4(a,b,c,d))<0)return worldPoint;
        float height=math.lerp(math.lerp(a,b,t.x),math.lerp(c,d,t.x),t.y);
        // Hair attached beneath the brim must remain beneath it, including after
        // a fast roll. Projecting through to its top would trap a whole lock.
        if(p.y<=height-margin)return worldPoint;
        p.y=height-margin;return math.transform(toWorld,p);
    }
}
