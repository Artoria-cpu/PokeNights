using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(90), RequireComponent(typeof(CharacterComboAttack))]
public sealed class AttackLimbTrail : MonoBehaviour
{
    public Material material;
    public const float Lifetime = .26f;
    struct Point { public Vector3 position; public float time; public Point(Vector3 p,float t){position=p;time=t;} }
    readonly List<Point> points = new List<Point>(96);
    readonly Vector3[] positions = new Vector3[96];
    readonly GradientColorKey[] colors = new GradientColorKey[8];
    readonly GradientAlphaKey[] alphas = new GradientAlphaKey[8];
    readonly Gradient gradient = new Gradient();
    CharacterComboAttack combo;
    LineRenderer glow, bolt, core;
    readonly LineRenderer[] forks = new LineRenderer[2];
    Transform carrier;
    Camera view;
    Vector3 previous;
    float previousTime;
    int sequence=-1, pointSerial;
    bool wasEmitting;
    public bool IsEmitting { get; private set; }
    public int EmissionFrames { get; private set; }
    public int PointCount => bolt==null?0:bolt.positionCount;

    void Awake(){combo=GetComponent<CharacterComboAttack>();}
    void Build()
    {
        if(material==null)return;
        var go=new GameObject("Attack lightning trail");go.transform.SetParent(transform,false);carrier=go.transform;
        view=Camera.main;
        glow=Make("Violet lightning glow",.26f);
        bolt=Make("Jagged lightning",.105f);
        core=Make("Lightning core",.042f);
        for(int i=0;i<forks.Length;i++)forks[i]=Make("Lightning fork "+i,.052f);
    }
    LineRenderer Make(string name,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(carrier,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
        line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;
        line.numCornerVertices=0;line.numCapVertices=0;line.widthMultiplier=width;
        line.widthCurve=new AnimationCurve(new Keyframe(0,.8f),new Keyframe(.16f,1),new Keyframe(.68f,.65f),new Keyframe(1,0));
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.positionCount=0;
        return line;
    }
    // Ages are retained with world positions: the bolt does not crawl or jitter as its tip moves.
    void AddPath(Vector3 tip,float now)
    {
        if(!wasEmitting){previous=tip;previousTime=now;points.Add(new Point(tip,now));return;}
        var delta=tip-previous;float distance=delta.magnitude;
        if(distance>1.5f){points.Clear();previous=tip;previousTime=now;points.Add(new Point(tip,now));return;}
        if(distance<.01f)return;
        var tangent=delta/distance;var viewForward=view!=null?view.transform.forward:transform.forward;
        var side=Vector3.Cross(viewForward,tangent).normalized;if(side.sqrMagnitude<.01f)side=transform.right;
        // Resample by distance, independently of frame rate, into uneven electrical corners.
        for(int i=0;i<16;i++)
        {
            float spacing=.095f+.070f*Mathf.Abs(Mathf.Sin((pointSerial+1)*1.713f));
            if(distance<spacing)break;
            float t=spacing/distance;
            previous+=tangent*spacing;previousTime=Mathf.Lerp(previousTime,now,t);distance-=spacing;
            float sign=(pointSerial++&1)==0?1:-1;
            float bend=.028f+.062f*Mathf.Abs(Mathf.Sin(pointSerial*2.31f));
            points.Add(new Point(previous+side*(sign*bend),previousTime));
        }
        while(points.Count>94)points.RemoveAt(0);
    }
    static float Ease(float start,float end,float age)
    {
        float t=Mathf.InverseLerp(start,end,age);
        return t*t*(3-2*t);
    }
    public static Color LightningColor(float age)
    {
        var gold=new Color(1,.80f,.025f);
        var lit=Color.Lerp(Color.white,gold,Ease(.06f,.23f,age));
        return Color.Lerp(lit,new Color(.018f,.005f,.028f),Ease(.34f,.86f,age));
    }
    static float TrailWidth(float age)=>Mathf.Lerp(.82f,1,Ease(0,.13f,age))*Mathf.Lerp(1,.45f,Ease(.38f,1,age));
    void SetColors(LineRenderer line,float newest,float oldest,bool soft=false)
    {
        for(int i=0;i<8;i++)
        {
            float t=i/7f,age=Mathf.Clamp01(Mathf.Lerp(newest,oldest,t));
            var c=LightningColor(age);
            if(soft)c=Color.Lerp(c,new Color(.65f,.12f,1),.55f)*(1-Ease(.34f,1,age));
            colors[i]=new GradientColorKey(c,t);
            alphas[i]=new GradientAlphaKey((soft?.35f:1)*(1-Ease(.34f,1,age)),t);
        }
        gradient.SetKeys(colors,alphas);line.colorGradient=gradient;float width=line==glow?.26f:line==bolt?.105f:line==core?.042f:.052f;line.widthMultiplier=width*1.55f*TrailWidth(newest);
    }
    void Draw(float now,Vector3 tip)
    {
        while(points.Count>0&&now-points[0].time>=Lifetime)points.RemoveAt(0);
        if(points.Count<2){ClearRenderers();return;}
        int count=points.Count;
        for(int i=0;i<count;i++)positions[i]=points[count-1-i].position;
        if(IsEmitting)positions[0]=tip;
        float newest=(now-points[count-1].time)/Lifetime,oldest=(now-points[0].time)/Lifetime;
        SetLine(glow,count);SetLine(bolt,count);SetLine(core,count);
        SetColors(glow,newest,oldest,true);SetColors(bolt,newest,oldest);SetColors(core,newest,oldest);
        for(int i=0;i<forks.Length;i++)
        {
            var line=forks[i];int index=2+i*3;
            if(count<=index+1){line.positionCount=0;continue;}
            Vector3 start=positions[index],tangent=(positions[Mathf.Min(count-1,index+2)]-positions[Mathf.Max(0,index-2)]).normalized;
            Vector3 side=Vector3.Cross(view!=null?view.transform.forward:transform.forward,tangent).normalized*(i==0?1:-1);
            line.positionCount=4;line.SetPosition(0,start);line.SetPosition(1,start+tangent*.07f+side*.13f);
            line.SetPosition(2,start+tangent*.13f+side*.08f);line.SetPosition(3,start+tangent*.22f+side*.25f);
            float age=(now-points[count-1-index].time)/Lifetime;SetColors(line,age,Mathf.Min(1,age+.2f));
        }
    }
    void SetLine(LineRenderer line,int count){line.positionCount=count;for(int i=0;i<count;i++)line.SetPosition(i,positions[i]);}
    void ClearRenderers(){if(glow!=null)glow.positionCount=0;if(bolt!=null)bolt.positionCount=0;if(core!=null)core.positionCount=0;foreach(var f in forks)if(f!=null)f.positionCount=0;}
    void LateUpdate()
    {
        if(combo.IsSwordAttack||combo.IsOrbAttack){points.Clear();IsEmitting=wasEmitting=false;ClearRenderers();return;}
        if(carrier==null||glow==null||bolt==null||core==null||forks[0]==null||forks[1]==null){if(carrier!=null)Destroy(carrier.gameObject);Build();}if(carrier==null)return;
        float now=Time.time;IsEmitting=false;Vector3 tip=previous;
        if(combo.IsAttacking&&combo.ActiveStrike!=null)
        {
            var strike=combo.ActiveStrike;var animator=combo.driver.animator;var bone=animator.GetBoneTransform(strike.contactBone);
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            float frame=state.normalizedTime*strike.clip.length*strike.clip.frameRate;
            if(sequence!=combo.AttackCount){sequence=combo.AttackCount;points.Clear();wasEmitting=false;}
            if(bone!=null)
            {
                tip=bone.position;
                IsEmitting=state.IsName(strike.stateName)&&frame>=Mathf.Max(strike.startFrame,strike.contactFrame-9)&&frame<=strike.contactFrame+3;
                if(IsEmitting&&Time.deltaTime>0){AddPath(tip,now);EmissionFrames++;}
            }
        }
        Draw(now,tip);wasEmitting=IsEmitting;
    }
    void OnDisable(){points.Clear();IsEmitting=wasEmitting=false;ClearRenderers();}
}





