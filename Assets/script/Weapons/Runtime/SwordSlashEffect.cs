using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(CharacterComboAttack)), DefaultExecutionOrder(10060)]
public sealed class SwordSlashEffect : MonoBehaviour
{
    public Material material,emberMaterial;
    public Material windMaterial;
    [Min(1)] public float rangeScale=1.25f;
    [Min(.05f)] public float hitHalfWidth=.3f,runningHitHalfWidth=.75f;
    [Min(0)] public float hitEdgePadding=.16f;
    [Min(0)] public float finisherTravelDistance=.85f;
    [Min(.1f)] public float runningCloseReach=1.55f,runningCloseHalfWidth=.85f;
    const float PlaybackSpeed=1.4f,OuterRadiusScale=.86f;
    public const float Lifetime=.56f/PlaybackSpeed;
    public int EmittedCount {get;private set;}
    public int ActiveArcCount {get;private set;}
    CharacterComboAttack combo;
    CharacterAttackHitbox hitbox;
    Collider[] hitContacts=new Collider[32];
    readonly HashSet<int> sampledColliders=new HashSet<int>();
    int sequence=-1,nextContact,poolIndex;
    Vector3 previousTip,tipMotion;bool hasTip;
    ParticleSystem embers;
    readonly System.Random random=new System.Random(73019);
    const int Segments=72,RibbonCount=3;
    sealed class Arc
    {
        public GameObject root;public Mesh mesh;public MeshRenderer renderer;
        public Vector3[] vertices=new Vector3[(Segments+1)*2*RibbonCount];
        public Vector2[] uv=new Vector2[(Segments+1)*2*RibbonCount];
        public Color[] colors=new Color[(Segments+1)*2*RibbonCount];
        public float birth,span,direction,radius,head,lastMeasured,followUntil,emission,angularSpeed;
        public float travelDistance,launchDelay,closeStartFrame,closeEndFrame,previousSourceFrame;
        public int sequence;public bool active,frontCut,running,released;
        public Vector3 previousHitPosition,previousActorPosition,baseLocalPosition,flightForward;
        public Vector3 launchPosition;
        public Quaternion worldRotation;
        public CombatWindRing wind;
        public readonly HashSet<int> hitTargets=new HashSet<int>();
        public MaterialPropertyBlock properties=new MaterialPropertyBlock();
    }
    Arc[] arcs=new Arc[3];
    void Awake(){combo=GetComponent<CharacterComboAttack>();hitbox=GetComponent<CharacterAttackHitbox>();}
    Arc Create()
    {
        var arc=new Arc();arc.root=new GameObject("Crimson crescent slash");arc.root.transform.SetParent(transform,false);
        arc.mesh=new Mesh{name="Pixel prismatic crescent"};arc.mesh.MarkDynamic();
        arc.root.AddComponent<MeshFilter>().sharedMesh=arc.mesh;arc.renderer=arc.root.AddComponent<MeshRenderer>();arc.renderer.sharedMaterial=material;
        arc.renderer.shadowCastingMode=ShadowCastingMode.Off;arc.renderer.receiveShadows=false;
        int[] indices=new int[Segments*6*RibbonCount];int k=0;
        var layers=new Vector2[arc.vertices.Length];
        for(int ribbon=0;ribbon<RibbonCount;ribbon++)
        {
            for(int i=0;i<=Segments;i++)
            {
                int v=ribbon*(Segments+1)*2+i*2;
                layers[v]=layers[v+1]=new Vector2(ribbon,0);
            }
            for(int i=0;i<Segments;i++)
            {
                int v=ribbon*(Segments+1)*2+i*2;
                indices[k++]=v;indices[k++]=v+1;indices[k++]=v+2;indices[k++]=v+2;indices[k++]=v+1;indices[k++]=v+3;
            }
        }
        arc.mesh.vertices=arc.vertices;arc.mesh.uv2=layers;arc.mesh.triangles=indices;return arc;
    }
    void Emit(int contact,Vector3 tip,float frame)
    {
        if(material==null)return;
        var arc=arcs[poolIndex];if(arc==null||arc.root==null)arcs[poolIndex]=arc=Create();poolIndex=(poolIndex+1)%arcs.Length;
        int stage=combo.CurrentStage;bool spin=stage==3||stage==5;
        arc.frontCut=combo.IsRunningAttack||(stage==4&&contact==2);
        bool lowSpin=!combo.IsRunningAttack&&stage==5;
        var forward=Vector3.ProjectOnPlane(transform.InverseTransformDirection(combo.AttackForward),Vector3.up).normalized;
        if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
        var center=Vector3.up*(stage==5?.65f:combo.IsRunningAttack?1.15f:1.08f)+forward*.28f;
        Vector3 radial=tip-center;if(radial.sqrMagnitude<.01f)radial=Vector3.forward;
        // Orient the plane with the sampled blade velocity. Positive rotation now follows the sword,
        // including opposite-handed swings and vertical cuts, instead of a per-stage guessed sign.
        Vector3 normal=Vector3.Cross(radial,tipMotion);
        if(normal.sqrMagnitude<.000001f)normal=Vector3.ProjectOnPlane(Vector3.up,radial);
        if(normal.sqrMagnitude<.000001f)normal=Vector3.ProjectOnPlane(Vector3.right,radial);
        // Running strikes and the final overhead cut stay in front; the low spin stays level.
        if(arc.frontCut)
        {
            radial=forward;
            normal=Vector3.Cross(Vector3.up,radial);
            center=Vector3.up*center.y+radial*.28f;
        }
        else if(lowSpin)
        {
            normal=Vector3.up;radial=Vector3.ProjectOnPlane(radial,normal);
            if(radial.sqrMagnitude<.01f)radial=Vector3.forward;
        }
        arc.root.transform.localPosition=center;
        arc.baseLocalPosition=center;arc.flightForward=combo.AttackForward;
        arc.root.transform.localRotation=Quaternion.LookRotation(radial.normalized,normal.normalized);
        arc.worldRotation=arc.root.transform.rotation;arc.released=false;
        arc.root.transform.localScale=Vector3.one;
        arc.direction=1;arc.head=arc.lastMeasured=0;arc.emission=24;
        arc.angularSpeed=spin?360:280;
        if(arc.frontCut)
        {
            Vector3 local=Quaternion.Inverse(arc.root.transform.localRotation)*(tip-center);
            arc.lastMeasured=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            arc.head=Mathf.Clamp(arc.lastMeasured,-82,82);
            // Local +X points down on this plane: running overhead cuts always travel downward.
            if(combo.IsRunningAttack)arc.direction=1;
        }
        else if(lowSpin)arc.direction=Vector3.Dot(Vector3.Cross(radial,tipMotion),normal)<0?-1:1;
        arc.radius=(spin?2.32f:combo.IsRunningAttack?2.20f:2.16f)*rangeScale;
        arc.span=spin?255:210;arc.birth=Time.time;arc.active=true;arc.renderer.enabled=true;
        arc.sequence=combo.AttackCount;arc.followUntil=combo.ActiveStrike.ContactFrame(contact)+5;
        arc.running=combo.IsRunningAttack;arc.previousHitPosition=arc.root.transform.position;
        arc.previousActorPosition=transform.position;
        var strike=combo.ActiveStrike;
        arc.travelDistance=(combo.IsFifthStrike||arc.running)&&contact==strike.ContactCount-1?finisherTravelDistance:0;
        arc.launchDelay=Mathf.Clamp((strike.ContactFrame(contact)-frame)/(strike.clip.frameRate*strike.playbackSpeed),0,Lifetime*.5f);
        arc.closeStartFrame=strike.ContactFrame(contact)-3;arc.closeEndFrame=strike.ContactFrame(contact)+5;
        arc.previousSourceFrame=frame;
        arc.hitTargets.Clear();
        arc.wind=CombatWindRing.Create(arc.root.transform.position,Quaternion.LookRotation(arc.root.transform.up,arc.root.transform.forward),
            windMaterial,arc.radius*1.17f,arc.radius*1.55f,.75f,Mathf.Min(320,arc.span+30),arc.direction*35);
        EmittedCount++;
    }
    bool FollowBlade(Arc arc,float frame,Vector3 tip)
    {
        if(!combo.IsSwordAttack||arc.sequence!=combo.AttackCount||frame>arc.followUntil)return false;
        if(arc.travelDistance>0&&Time.time-arc.birth>arc.launchDelay)return false;
        if(Time.deltaTime<=0)return true;
        Vector3 local=Quaternion.Inverse(arc.worldRotation)*(transform.TransformPoint(tip)-arc.root.transform.position);
        if(local.x*local.x+local.z*local.z<.01f)return true;
        float angle=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
        float delta=Mathf.DeltaAngle(arc.lastMeasured,angle);arc.lastMeasured=angle;
        // Keep the cutting direction through recovery rebounds, then coast in that same direction.
        if(delta*arc.direction<0)return false;
        if(Mathf.Abs(delta)<.12f)return true;
        arc.angularSpeed=Mathf.Lerp(arc.angularSpeed,Mathf.Clamp(Mathf.Abs(delta)/Time.deltaTime,140,650),.35f);
        if(arc.frontCut)
        {
            float frontAngle=Mathf.Clamp(angle,-82,82);
            arc.head=arc.direction>0?Mathf.Max(arc.head,frontAngle):Mathf.Min(arc.head,frontAngle);
        }
        else arc.head+=delta;
        return true;
    }
    void Draw(Arc arc,float age)
    {
        // Fade in a fixed arc: growing its span behind the blade makes the
        // crescent appear to rotate backwards before it follows the swing.
        float span=arc.span;
        if(arc.frontCut)span=Mathf.Min(span,arc.direction>0?arc.head+82:82-arc.head);
        float expansion=Mathf.Lerp(1,1.30f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,1,age)));
        for(int ribbon=0;ribbon<RibbonCount;ribbon++)
        {
            // A compact white inner crescent, a colored outer crescent, and a broader halo.
            float head=arc.head-(ribbon==1?6:ribbon==2?12:0)*arc.direction;
            if(arc.frontCut)head=Mathf.Clamp(head,-82,82);
            float ribbonSpan=span*(ribbon==1?.85f:ribbon==2?1.08f:1);
            if(arc.frontCut)ribbonSpan=Mathf.Min(ribbonSpan,arc.direction>0?head+82:82-head);
            for(int i=0;i<=Segments;i++)
            {
                float u=i/(float)Segments;
                float angle=(head-ribbonSpan*(1-u)*arc.direction)*Mathf.Deg2Rad;
                var direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                float taper=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.68f);
                float radius=arc.radius*(ribbon==1?.66f:ribbon==2?.91f:OuterRadiusScale)*expansion;
                float width=(ribbon==1?.20f:ribbon==2?.95f:.66f)*taper*expansion*rangeScale;
                float outerEdge=(ribbon==1?.065f:ribbon==2?.25f:.12f)*taper*expansion*rangeScale;
                int v=ribbon*(Segments+1)*2+i*2;
                arc.vertices[v]=direction*(radius-width);arc.vertices[v+1]=direction*(radius+outerEdge);
                arc.uv[v]=new Vector2(u,0);arc.uv[v+1]=new Vector2(u,1);
                arc.colors[v]=arc.colors[v+1]=new Color(1,1,1,ribbon==1?.9f:ribbon==2?.30f:1);
            }
        }
        arc.mesh.vertices=arc.vertices;arc.mesh.uv=arc.uv;arc.mesh.colors=arc.colors;arc.mesh.RecalculateBounds();
        arc.properties.SetFloat("_Opacity",Mathf.SmoothStep(0,1,age/.045f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,1,age))));
        arc.properties.SetFloat("_Age",age);arc.properties.SetFloat("_PixelSize",4);
        arc.renderer.SetPropertyBlock(arc.properties);
        if(age<.55f&&Time.deltaTime>0)EmitEmbers(arc,span,expansion);
    }
    float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    void ResolveHits(Arc arc,float frame)
    {
        // Fill the swept sector from the hilt to the visible outer edge, so a
        // nearby target cannot stand inside the hollow crescent unharmed.
        var pose=arc.root.transform;
        Vector3 sweptOrigin=arc.running?arc.previousActorPosition:transform.position;
        bool sweep=arc.running||arc.travelDistance>0;
        sampledColliders.Clear();
        for(int start=2;start<70;start+=3)
        {
            int end=Mathf.Min(start+3,70),offset=0;
            Vector3 radial=(arc.vertices[offset+start*2+1]+arc.vertices[offset+end*2+1]).normalized;
            if(radial.sqrMagnitude<.01f)continue;
            Quaternion rotation=Quaternion.LookRotation(pose.TransformDirection(radial),pose.up);
            Quaternion inverse=Quaternion.Inverse(rotation);
            var bounds=new Bounds();bool first=true;
            for(int i=start;i<=end;i++)
            {
                Vector3 inner=arc.vertices[offset+i*2],outer=arc.vertices[offset+i*2+1];
                for(int edge=0;edge<2;edge++)
                {
                    // Keep the shader's outer boundary, filling only its interior.
                    Vector3 localPoint=edge==0?Vector3.zero:Vector3.Lerp(inner,outer,.90f);
                    Vector3 point=inverse*(pose.TransformPoint(localPoint)-pose.position);
                    if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                }
            }
            if(bounds.size.x<.001f||bounds.size.z<.001f)continue;
            Vector3 half=bounds.extents;half.x+=hitEdgePadding;half.z+=hitEdgePadding;
            half.y=arc.running?runningHitHalfWidth:hitHalfWidth;
            Vector3 center=pose.position+rotation*bounds.center;
            if(sweep)
            {
                // Cover the movement of the whole enlarged ribbon between frames.
                Vector3 travel=pose.position-arc.previousHitPosition;
                Vector3 localTravel=inverse*travel;
                half+=new Vector3(Mathf.Abs(localTravel.x),Mathf.Abs(localTravel.y),Mathf.Abs(localTravel.z))*.5f;
                center-=travel*.5f;
            }
            ResolveBox(arc,center,half,rotation,frame,sweptOrigin);
        }
        if(arc.running&&frame>=arc.closeStartFrame&&arc.previousSourceFrame<=arc.closeEndFrame)
        {
            // Cover the gap between the actor's chest and the vertical ribbon, only during contact.
            Quaternion rotation=Quaternion.LookRotation(arc.flightForward,Vector3.up);
            Vector3 travel=transform.position-arc.previousActorPosition;
            Vector3 localTravel=Quaternion.Inverse(rotation)*travel;
            Vector3 center=transform.position+Vector3.up+arc.flightForward*(runningCloseReach*.5f)-travel*.5f;
            Vector3 half=new Vector3(runningCloseHalfWidth,.85f,runningCloseReach*.5f+.03f);
            half+=new Vector3(Mathf.Abs(localTravel.x),Mathf.Abs(localTravel.y),Mathf.Abs(localTravel.z))*.5f;
            ResolveBox(arc,center,half,rotation,frame,sweptOrigin);
        }
        arc.previousHitPosition=pose.position;
        arc.previousActorPosition=transform.position;
        arc.previousSourceFrame=frame;
    }
    void ResolveBox(Arc arc,Vector3 center,Vector3 half,Quaternion rotation,float frame,Vector3 origin)
    {
        int count;
        while(true)
        {
            count=Physics.OverlapBoxNonAlloc(center,half,hitContacts,rotation,~0,QueryTriggerInteraction.Ignore);
            if(count<hitContacts.Length)break;
            System.Array.Resize(ref hitContacts,hitContacts.Length*2);
        }
        for(int i=0;i<count;i++)
            if(sampledColliders.Add(hitContacts[i].GetInstanceID()))
                hitbox.ResolveSwordContact(hitContacts[i],center,frame,arc.hitTargets,origin);
    }
    void CreateEmbers()
    {
        if(embers!=null||emberMaterial==null)return;
        var go=new GameObject("Golden sword embers");go.transform.SetParent(transform,false);
        embers=go.AddComponent<ParticleSystem>();embers.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=embers.main;main.simulationSpeed=PlaybackSpeed;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=520;
        main.startLifetime=new ParticleSystem.MinMaxCurve(.24f,.50f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.085f);main.startSpeed=0;main.gravityModifier=new ParticleSystem.MinMaxCurve(.55f,.90f);
        var velocity=embers.velocityOverLifetime;velocity.enabled=true;
        velocity.speedModifier=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.18f,.82f),new Keyframe(.65f,.32f),new Keyframe(1,.12f)));
        var emission=embers.emission;emission.enabled=false;var shape=embers.shape;shape.enabled=false;
        var size=embers.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.18f),new Keyframe(.12f,1),new Keyframe(.35f,.55f),new Keyframe(1,0)));
        var color=embers.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(new Color(1,1,.65f),0),new GradientColorKey(new Color(1,.94f,.26f),.22f),new GradientColorKey(new Color(1,.82f,.06f),.6f),new GradientColorKey(new Color(.8f,.62f,.025f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.07f),new GradientAlphaKey(.55f,.35f),new GradientAlphaKey(0,1)});color.color=gradient;
        var rotation=embers.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-3.5f,3.5f);
        var renderer=embers.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=emberMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.View;renderer.maxParticleSize=.12f;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        embers.Play(false);
    }
    void EmitEmbers(Arc arc,float span,float expansion)
    {
        CreateEmbers();if(embers==null)return;if(!embers.isPlaying)embers.Play(false);
        arc.emission+=Time.deltaTime*320*PlaybackSpeed;
        while(arc.emission>=1)
        {
            arc.emission-=1;
            float angle=(arc.head-Range(0,span*.55f)*arc.direction)*Mathf.Deg2Rad;
            var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));var tangent=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle))*arc.direction;
            Color color=new Color(1,Range(.82f,1),Range(.18f,.38f));
            Vector3 velocity=arc.root.transform.TransformDirection(radial*Range(2.6f,4.8f)+tangent*Range(1.5f,3.4f))+Vector3.up*Range(.25f,.80f);
            if(arc.running)velocity+=arc.flightForward*Mathf.Max(0,2-Vector3.Dot(velocity,arc.flightForward));
            var particle=new ParticleSystem.EmitParams
            {
                position=arc.root.transform.TransformPoint(radial*(arc.radius*OuterRadiusScale*expansion*Range(.91f,1.03f))),
                velocity=velocity,
                startColor=color,startSize=random.Next(3)==0?Range(.055f,.085f):Range(.025f,.055f),startLifetime=Range(.24f,.50f),rotation=Range(0,360)
            };
            embers.Emit(particle,1);
        }
    }
    void LateUpdate()
    {
        float frame=0;Vector3 tip=previousTip;
        if(combo.IsSwordAttack&&combo.AttackSword!=null)
        {
            tip=transform.InverseTransformPoint(combo.AttackSword.TransformPoint(Vector3.up*1.18f));
            if(sequence!=combo.AttackCount){sequence=combo.AttackCount;nextContact=0;hasTip=false;tipMotion=Vector3.zero;}
            // Sample the blade after body/hand correction, so the effect uses its rendered pose.
            if(hasTip&&Time.deltaTime>0)tipMotion=Vector3.Lerp(tipMotion,tip-previousTip,.85f);
            var strike=combo.ActiveStrike;var animator=combo.driver.animator;
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            frame=state.normalizedTime*strike.clip.length*strike.clip.frameRate;
            if(state.IsName(strike.stateName)&&nextContact<strike.ContactCount&&frame>=strike.ContactFrame(nextContact)-strike.clip.frameRate*.14f)
                Emit(nextContact++,tip,frame);
            previousTip=tip;hasTip=true;
        }
        else hasTip=false;
        ActiveArcCount=0;
        bool canHit=hitbox!=null&&hitbox.isActiveAndEnabled&&combo.IsSwordAttack&&
            (combo.driver.acceptPlayerInput||combo.driver.AIControlled)&&!combo.driver.IsDodging&&Time.deltaTime>0;
        bool synced=false;
        foreach(var arc in arcs)
        {
            if(arc==null||!arc.active||arc.root==null)continue;
            float age=(Time.time-arc.birth)/Lifetime;
            if(age>=1){arc.active=false;arc.renderer.enabled=false;continue;}
            Vector3 center=transform.TransformPoint(arc.baseLocalPosition);
            arc.root.transform.rotation=arc.worldRotation;
            if(!arc.released)arc.root.transform.position=center;
            bool launching=arc.travelDistance>0&&Time.time-arc.birth>=arc.launchDelay;
            if(!arc.released&&!FollowBlade(arc,frame,tip))
            {
                arc.head+=arc.direction*arc.angularSpeed*Mathf.Lerp(1,.18f,age)*Time.deltaTime*PlaybackSpeed;
                if(arc.frontCut)arc.head=Mathf.Clamp(arc.head,-82,82);
            }
            if(launching)
            {
                if(!arc.released){arc.launchPosition=center;arc.released=true;}
                // Preserve the release pose in world space; forward carry cannot pull it backwards.
                arc.launchPosition+=arc.flightForward*Mathf.Max(0,Vector3.Dot(center-arc.launchPosition,arc.flightForward));
                float flight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(arc.launchDelay,Lifetime*.82f,Time.time-arc.birth));
                arc.root.transform.position=arc.launchPosition+arc.flightForward*(arc.travelDistance*flight);
            }
            if(arc.wind!=null)arc.wind.transform.position=arc.root.transform.position;
            Draw(arc,age);ActiveArcCount++;
            float opacity=Mathf.SmoothStep(0,1,age/.045f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,1,age)));
            if(canHit&&arc.sequence==combo.AttackCount&&opacity>.05f)
            {
                if(!synced){Physics.SyncTransforms();synced=true;}
                ResolveHits(arc,frame);
            }
        }
    }
    void OnDisable(){if(arcs!=null)foreach(var arc in arcs)if(arc!=null&&arc.root!=null){arc.active=false;arc.renderer.enabled=false;}if(embers!=null)embers.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ActiveArcCount=0;hasTip=false;}
    void OnDestroy(){if(arcs!=null)foreach(var arc in arcs)if(arc!=null){if(arc.mesh!=null)Destroy(arc.mesh);if(arc.root!=null)Destroy(arc.root);}if(embers!=null)Destroy(embers.gameObject);}
}
