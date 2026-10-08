using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A visual-only expanding ribbon, shared by kick contact and the outside of sword slashes.</summary>
public sealed class CombatWindRing : MonoBehaviour
{
    const int Segments=80;
    Mesh mesh;MeshRenderer rendererComponent;MaterialPropertyBlock properties;
    readonly Vector3[] vertices=new Vector3[(Segments+1)*2];
    readonly Color[] colors=new Color[(Segments+1)*2];
    float age,lifetime,startRadius,endRadius,span,spin,delay;
    bool unscaled,quickExpansion;
    public static CombatWindRing Create(Vector3 center,Quaternion rotation,Material material,float fromRadius,float toRadius,float seconds,float spanDegrees=360,float spinDegrees=35,bool ignoreTimeScale=false,float delaySeconds=0,bool quickExpansion=true)
    {
        if(material==null)return null;
        var fx=new GameObject("Sword expanding wind").AddComponent<CombatWindRing>();
        fx.transform.SetPositionAndRotation(center,rotation);
        fx.startRadius=fromRadius;fx.endRadius=toRadius;fx.lifetime=Mathf.Max(.1f,seconds);
        fx.span=spanDegrees*Mathf.Deg2Rad;fx.spin=spinDegrees*Mathf.Deg2Rad;fx.unscaled=ignoreTimeScale;
        fx.delay=Mathf.Max(0,delaySeconds);
        fx.quickExpansion=quickExpansion;
        fx.properties=new MaterialPropertyBlock();fx.mesh=new Mesh{name="Sword wind ribbon"};fx.mesh.MarkDynamic();
        var uv=new Vector2[fx.vertices.Length];var indices=new int[Segments*6];
        for(int i=0;i<=Segments;i++)
        {
            uv[i*2]=new Vector2((float)i/Segments,0);uv[i*2+1]=new Vector2((float)i/Segments,1);
            if(i==Segments)continue;int v=i*2,t=i*6;
            indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v+2;indices[t+4]=v+1;indices[t+5]=v+3;
        }
        fx.mesh.vertices=fx.vertices;fx.mesh.uv=uv;fx.mesh.triangles=indices;
        fx.gameObject.AddComponent<MeshFilter>().sharedMesh=fx.mesh;
        fx.rendererComponent=fx.gameObject.AddComponent<MeshRenderer>();fx.rendererComponent.sharedMaterial=material;
        fx.rendererComponent.shadowCastingMode=ShadowCastingMode.Off;fx.rendererComponent.receiveShadows=false;
        fx.Render(0);return fx;
    }
    void Update()
    {
        age+=unscaled?Time.unscaledDeltaTime:Time.deltaTime;RenderAtTime(age);
        if(age>=delay+lifetime)Dispose();
    }
    public static CombatWindRing[] CreatePair(Vector3 center,Vector3 direction,Material material,bool unscaled=false,float seconds=.7f,bool quickExpansion=true)
    {
        var forward=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
        if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
        var rotation=Quaternion.LookRotation(forward,Vector3.up);
        return new[]{Create(center,rotation,material,.12f,2.1f,seconds,360,35,unscaled,quickExpansion:quickExpansion),
            Create(center+forward*.18f,rotation,material,.08f,1.35f,seconds,360,-28,unscaled,quickExpansion:quickExpansion)};
    }
    public void RenderAtTime(float seconds)=>Render((seconds-delay)/lifetime);
    public void Render(float life)
    {
        life=Mathf.Clamp01(life);float dying=Mathf.SmoothStep(0,1,Mathf.InverseLerp(quickExpansion?.2f:.7f,1,life));
        float growth=quickExpansion?.8f*Mathf.SmoothStep(0,1,Mathf.Clamp01(life/.2f))+.2f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1,life))
            :1-Mathf.Pow(1-Mathf.Clamp01(life/.7f),3);
        float radius=Mathf.Lerp(startRadius,endRadius,growth)*(quickExpansion?1:1+dying*.10f);
        float alpha=Mathf.SmoothStep(0,1,life/.05f)*(1-dying)*.68f;
        for(int i=0;i<=Segments;i++)
        {
            float t=(float)i/Segments,angle=(t-.5f)*span+life*lifetime*spin;
            var radial=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
            float taper=span>6.2f?1:Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.6f);
            float width=(.09f+radius*.055f)*taper;
            vertices[i*2]=radial*(radius-width);vertices[i*2+1]=radial*(radius+width);
            colors[i*2]=colors[i*2+1]=new Color(1-dying,1-dying,1-dying,alpha*taper);
        }
        mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();rendererComponent.enabled=alpha>.001f;
        properties.SetFloat("_EffectTime",life*lifetime*.5f);rendererComponent.SetPropertyBlock(properties);
    }
    public void Dispose(){ReleaseMesh();if(Application.isPlaying)Destroy(gameObject);else DestroyImmediate(gameObject);}
    void OnDestroy()=>ReleaseMesh();
    void ReleaseMesh(){if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);mesh=null;}}
}
