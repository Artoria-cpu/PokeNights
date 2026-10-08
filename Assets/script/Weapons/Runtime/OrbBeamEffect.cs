using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Charge, collapse, constellation, beam, then a thinning core and lingering wind.</summary>
[DefaultExecutionOrder(9600)]
public sealed class OrbBeamEffect : MonoBehaviour
{
    public const float ThinSeconds = .28f, WindTailSeconds = .80f;
    public const float StarGrowthSeconds=.30f;
    public static float StarStartProgress(float chargeDuration)=>Mathf.Max(1-StarGrowthSeconds/chargeDuration,.68f+.1f/chargeDuration);
    const int Segments = 128;
    Material coreMaterial, glowMaterial, ringMaterial, spriteMaterial, windMaterial, wrapMaterial;
    LineRenderer core, glow, corona;
    readonly LineRenderer[] strands = new LineRenderer[5];
    LineRenderer[] particles;
    readonly Ribbon[] groundWaves = new Ribbon[6], airWaves = new Ribbon[2], spirals = new Ribbon[2];
    readonly MeshRenderer[] stars = new MeshRenderer[4];
    MeshRenderer orb, halo, muzzleGlow, impactGlow;
    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<Material> ownedMaterials=new List<Material>();
    bool empowered;
    Material darkParticleMaterial;
    float Strength=>empowered?1.7f:1;
    float Tempo=>empowered?1.8f:1;
    MaterialPropertyBlock properties;
    readonly Vector3[] points = new Vector3[Segments + 1];
    Vector3 start, end, ground, forward, right, up;
    float charge, chargeSeconds, age, duration, visualTime, lastChargeUpdate, cancelAge = -1;
    float thinSeconds=ThinSeconds;
    bool fired;
    Light sourceLight, impactLight;
    Mesh quad;
    CombatWindRing[] releaseWaves;
    readonly AnimationCurve beamWidth=new AnimationCurve();
    readonly Keyframe[] beamWidthKeys=new Keyframe[65];

    sealed class Ribbon
    {
        public Mesh mesh;
        public MeshRenderer renderer;
        public readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        public readonly Color[] colors = new Color[(Segments + 1) * 2];
    }

    public static OrbBeamEffect BeginCharge(Vector3 origin, Vector3 direction, Vector3 groundPoint,
        Material core, Material glow, Material ring, Material sprite, Material wind, float chargeDuration,Material wrap=null,bool red=false)
    {
        var effect = new GameObject("Orb beam - charge and release").AddComponent<OrbBeamEffect>();
        effect.coreMaterial = core; effect.glowMaterial = glow; effect.ringMaterial = ring;
        effect.spriteMaterial = sprite; effect.windMaterial = wind;
        effect.wrapMaterial=wrap!=null?wrap:wind;
        effect.empowered=red;
        effect.start = origin; effect.end = origin + direction.normalized;
        effect.ground = groundPoint; effect.chargeSeconds = Mathf.Max(.1f, chargeDuration);
        effect.lastChargeUpdate = Time.time;
        try { if(red)effect.ConfigureRed();effect.Build(); effect.RenderCharge(red?StarStartProgress(effect.chargeSeconds):0); }
        catch
        {
            effect.Dispose();
            throw;
        }
        return effect;
    }
    Material RedMaterial(Material original,Color tint)
    {
        var material=new Material(original);material.name=original.name+" red burst";ownedMaterials.Add(material);
        material.SetColor("_Color",tint);
        if(material.HasProperty("_HotColor"))material.SetColor("_HotColor",new Color(1.6f,.08f,.16f,1));
        if(material.HasProperty("_Flow"))material.SetFloat("_Flow",material.GetFloat("_Flow")*1.8f);
        return material;
    }
    void ConfigureRed()
    {
        coreMaterial=RedMaterial(coreMaterial,new Color(1,.015f,.035f,1));
        glowMaterial=RedMaterial(glowMaterial,new Color(.85f,.005f,.02f,.8f));
        ringMaterial=RedMaterial(ringMaterial,new Color(1,.02f,.045f,1));
        spriteMaterial=RedMaterial(spriteMaterial,new Color(1,.015f,.04f,1));
        windMaterial=RedMaterial(windMaterial,new Color(.48f,.025f,.045f,1));
        wrapMaterial=RedMaterial(wrapMaterial,new Color(.025f,.006f,.012f,1));
        darkParticleMaterial=RedMaterial(ringMaterial,new Color(.012f,.006f,.009f,1));
        darkParticleMaterial.SetColor("_HotColor",new Color(.018f,.006f,.01f,1));
        darkParticleMaterial.SetFloat("_Intensity",1);darkParticleMaterial.SetFloat("_DstBlend",10);
    }

    public void SetCharge(Vector3 origin, Vector3 direction, float progress)
    {
        if (fired || cancelAge >= 0) return;
        start = origin; end = origin + direction.normalized; charge = Mathf.Clamp01(progress);
        lastChargeUpdate = Time.time;
    }

    public void Fire(Vector3 origin, Vector3 endpoint, float beamDuration)
    {
        if (fired || cancelAge >= 0) return;
        start = origin; end = endpoint; fired = true; charge = 1; age = 0;
        duration = Mathf.Max(.1f, beamDuration);
        releaseWaves=CombatWindRing.CreatePair(origin,endpoint-origin,windMaterial,quickExpansion:false);
        foreach(var wave in releaseWaves)if(wave!=null){wave.transform.SetParent(transform,true);wave.enabled=false;}
        RenderBeam(0);
    }
    public void SetBeam(Vector3 origin,Vector3 endpoint)
    { if(fired&&cancelAge<0){start=origin;end=endpoint;} }

    // Stop sustaining without hiding the beam: keep its current phase and run the normal thinning tail.
    public void EndEmission(float fadeSeconds=ThinSeconds)
    {
        if(fired&&cancelAge<0){duration=Mathf.Min(duration,Mathf.Max(0,visualTime-chargeSeconds));thinSeconds=Mathf.Max(.01f,fadeSeconds);}
    }

    public void Cancel()
    {
        if (cancelAge >= 0) return;
        cancelAge = 0;
        foreach (var line in GetComponentsInChildren<LineRenderer>()) line.enabled = false;
        foreach (var mesh in GetComponentsInChildren<MeshRenderer>()) mesh.enabled = false;
        sourceLight.intensity = impactLight.intensity = 0;
    }

    void LateUpdate()
    {
        if (Time.deltaTime <= 0) return;
        if (cancelAge >= 0)
        {
            cancelAge += Time.deltaTime;
            DrawGround(charge * chargeSeconds + cancelAge, true, Mathf.Clamp01(1 - cancelAge / .3f));
            if (cancelAge >= .3f) Dispose();
            return;
        }
        if (!fired)
        {
            if (Time.time - lastChargeUpdate > .5f) { Cancel(); return; }
            RenderCharge(charge);
        }
        else
        {
            age += Time.deltaTime; RenderBeam(age);
            if (age >= duration + thinSeconds + WindTailSeconds) Dispose();
        }
    }

    void Basis()
    {
        forward = (end - start).sqrMagnitude > .0001f ? (end - start).normalized : Vector3.forward;
        right = Vector3.Cross(Mathf.Abs(forward.y) > .92f ? Vector3.right : Vector3.up, forward).normalized;
        up = Vector3.Cross(forward, right).normalized;
    }

    void Build()
    {
        properties = new MaterialPropertyBlock();
        Basis();
        quad = new Mesh { name = "Orb effect billboard" };
        quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2); ownedMeshes.Add(quad);
        orb = Sprite("Gathering sphere"); halo = Sprite("Soft amber aura");
        muzzleGlow = Sprite("Release flare"); impactGlow = Sprite("Beam endpoint glow");
        for (int i = 0; i < stars.Length; i++) stars[i] = Sprite("Cross star " + i);
        core = Line("White gold beam core", coreMaterial, 65);
        glow = Line("Amber beam glow", glowMaterial, 65);
        corona = Line("Outer beam aura", glowMaterial, 65);
        for (int i = 0; i < strands.Length; i++)
        { strands[i] = Line("Converging energy arc " + i, ringMaterial, Segments + 1); strands[i].widthCurve = GatherWidth; }
        particles=new LineRenderer[empowered?84:36];
        for (int i = 0; i < particles.Length; i++)
        { particles[i] = Line("Energy shard " + i, empowered&&i%3!=0?darkParticleMaterial:i % 3 == 0 ? coreMaterial : ringMaterial, 3); particles[i].widthCurve = ArcWidth; }
        for (int i = 0; i < groundWaves.Length; i++) groundWaves[i] = MakeRibbon("Expanding ground wind " + i, windMaterial);
        for (int i = 0; i < airWaves.Length; i++) airWaves[i] = MakeRibbon("Travelling air crescent " + i, windMaterial);
        for (int i = 0; i < spirals.Length; i++) spirals[i] = MakeRibbon("Rotating beam helix " + i, wrapMaterial);
        sourceLight = MakeLight("Charge light", 5); impactLight = MakeLight("Impact light", 4);
    }

    MeshRenderer Sprite(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = spriteMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.enabled = false; return renderer;
    }

    LineRenderer Line(string name, Material material, int count)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
        line.positionCount = count; line.numCapVertices = 0; line.numCornerVertices = 0;
        line.textureMode = LineTextureMode.Stretch; line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false; line.enabled = false; return line;
    }

    Ribbon MakeRibbon(string name, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var ribbon = new Ribbon { mesh = new Mesh { name = name } };
        ribbon.mesh.MarkDynamic(); var uv = new Vector2[ribbon.vertices.Length]; var triangles = new int[Segments * 6];
        for (int i = 0; i <= Segments; i++)
        {
            uv[i * 2] = new Vector2((float)i / Segments, 0); uv[i * 2 + 1] = new Vector2((float)i / Segments, 1);
            if (i == Segments) continue;
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        ribbon.mesh.vertices = ribbon.vertices; ribbon.mesh.uv = uv; ribbon.mesh.triangles = triangles;
        go.AddComponent<MeshFilter>().sharedMesh = ribbon.mesh;
        ribbon.renderer = go.AddComponent<MeshRenderer>(); ribbon.renderer.sharedMaterial = material;
        ribbon.renderer.shadowCastingMode = ShadowCastingMode.Off; ribbon.renderer.receiveShadows = false;
        ribbon.renderer.enabled = false; ownedMeshes.Add(ribbon.mesh); return ribbon;
    }

    Light MakeLight(string name, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, .49f, .08f);
        if(empowered)light.color=new Color(1,.02f,.055f);
        light.range = range; light.shadows = LightShadows.None; light.intensity = 0; return light;
    }

    // Pure visual sampling also serves the edit-mode storyboard; no combat is invoked.
    public void RenderCharge(float progress)
    {
        charge = Mathf.Clamp01(progress); Basis(); float time = charge * chargeSeconds; visualTime=time;
        float gather = Mathf.Pow(Smooth(.02f, .57f, charge),1.65f), collapse = Smooth(.57f, .68f, charge);
        float radius = Mathf.Lerp(.035f, .46f, gather) * (1 - collapse);
        float gatherFade=Smooth(0,Mathf.Min(.45f,chargeSeconds*.25f),time);
        float sphereDelay=Mathf.Min(.35f,chargeSeconds*.20f);
        float sphereAlpha = Smooth(sphereDelay,sphereDelay+Mathf.Min(.25f,chargeSeconds*.15f),time) * (1 - Smooth(.64f, .68f, charge));
        DrawSprite(orb, start, radius * 2, sphereAlpha, 0, time);
        DrawSprite(halo, start, radius * 7.8f, sphereAlpha * .75f, 1, time);
        float arcsAlpha = gatherFade * (1 - Smooth(.50f, .70f, charge));
        for (int s = 0; s < strands.Length; s++)
        {
            float phase = s * 2.39996f + time*.45f+time*time*.65f;
            float life=Mathf.Clamp01(time/(chargeSeconds*(.63f+s*.015f)));
            var tilt=Quaternion.Euler(s*47+25,s*71+38,s*29);
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                // The inner endpoint stays inside the orb; the outer ribbon curls in from the sphere surface.
                float r=(1.85f+.25f*Mathf.Sin(s*7))*(1-Mathf.Pow(t,1.55f));
                float angle=phase+t*Mathf.PI*1.48f;
                var orbit=tilt*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),.20f*Mathf.Sin(t*Mathf.PI));
                points[i]=start+(right*orbit.x+up*orbit.y+forward*orbit.z).normalized*r;
            }
            strands[s].SetPositions(points);
            properties.Clear();properties.SetFloat("_TailCut",Mathf.Min(.97f,Mathf.Pow(Smooth(.08f,1,life),1.35f)));
            properties.SetFloat("_EffectTime",time);strands[s].SetPropertyBlock(properties);
            Tint(strands[s], (.16f + .10f * gather) * (s % 2 == 0 ? 1.15f : .9f), arcsAlpha*Smooth(0,.10f,life)*(1-Smooth(.94f,1,life)));
        }
        float starStart=StarStartProgress(chargeSeconds);
        float starPhase=Mathf.InverseLerp(starStart,1,charge);
        float starBirth=Smooth(0,.015f,starPhase),starGrowth=.8f*Smooth(0,.20f,starPhase)+.2f*Smooth(.20f,1,starPhase);
        DrawSprite(stars[0], start, 8f*starGrowth, starBirth, 2, .30f+starPhase*.24f);
        for (int s = 1; s < stars.Length; s++)
        {
            float fade = Smooth(.08f+s*.10f,.34f+s*.10f,starPhase);
            Vector3 offset = right * (s == 1 ? -.65f : s == 2 ? .60f : .12f) + up * (s == 1 ? .28f : s == 2 ? .60f : -.50f);
            DrawSprite(stars[s], start + offset * Mathf.Lerp(.4f, 1, starGrowth), (.37f + s * .07f) * starBirth, fade, 2, time * .26f + s * .4f);
        }
        DrawSprite(muzzleGlow, start, .9f + starGrowth * .5f, starBirth * .22f, 1, 0);
        if(charge<.68f){foreach(var particle in particles)particle.enabled=false;}
        else if(charge<starStart)DrawParticles((charge-.68f)*chargeSeconds,false,0,1);
        else DrawParticles((charge-starStart)*chargeSeconds,false,0,1-Smooth(.93f,1,charge));
        DrawGround(time, false, gatherFade);
        sourceLight.transform.position = start; sourceLight.intensity = sphereAlpha * (1 + gather * 3) + starBirth * 1.4f;
    }

    static readonly AnimationCurve ArcWidth = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.26f, .8f), new Keyframe(.65f, 1), new Keyframe(1, 0));
    static readonly AnimationCurve GatherWidth = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.22f, .65f), new Keyframe(.55f, 1), new Keyframe(.92f, .55f), new Keyframe(1, .25f));
    float BeamWidth(float along,float time,float length)
    {
        // Width belongs to the moment this section left the source, then travels down the beam.
        float emitted=Mathf.Max(0,time-along*length/(24f*Tempo));
        float pulse=1+2*(1-Smooth(0,.5f,emitted))+.28f*Smooth(.5f,.65f,emitted)*Mathf.Sin((emitted-.5f)*Mathf.PI*2*Tempo/.42f);
        return pulse*(1-Smooth(.97f,1,along));
    }

    public void RenderBeam(float time)
    {
        Basis();visualTime=chargeSeconds+time;
        // Let the red front travel visibly through the fading gold instead of replacing its full length at once.
        float grow = empowered?Mathf.Clamp01(time*120f/Mathf.Max(.01f,Vector3.Distance(start,end))):1-Mathf.Pow(1-Mathf.Clamp01(time/.065f),3);
        float thin = 1 - Smooth(duration, duration + thinSeconds, time);
        Vector3 tip = Vector3.Lerp(start, end, grow);
        float beamLength=Vector3.Distance(start,tip);
        for(int i=0;i<beamWidthKeys.Length;i++)
        {
            float t=(float)i/(beamWidthKeys.Length-1);
            beamWidthKeys[i]=new Keyframe(t,BeamWidth(t,time,beamLength));
        }
        beamWidth.keys=beamWidthKeys;
        core.widthCurve=glow.widthCurve=corona.widthCurve=beamWidth;
        if(releaseWaves!=null)foreach(var wave in releaseWaves)if(wave!=null)wave.RenderAtTime(time);
        orb.enabled = halo.enabled = false;
        foreach (var strand in strands) strand.enabled = false;
        foreach (var star in stars) star.enabled = false;
        float flash = 1 - Smooth(.06f, .23f, time);
        float starFade=1-Smooth(0,.10f,time);
        DrawSprite(stars[0], start, 8f*(1+.08f*(1-starFade)),starFade,2,.54f+time*.24f);
        DrawSprite(stars[1], start, 2.1f*Mathf.Lerp(.05f,1,thin),thin*.6f*(1-starFade),2,.54f+time*.24f);
        DrawSprite(muzzleGlow, start, (2.7f + flash * 1.5f) * Mathf.Lerp(.3f, 1, thin), thin * .85f, 1, 0);
        DrawSprite(impactGlow, tip, 1.2f * thin, thin * grow * .8f, 1, 0);
        SetLine(core, start, tip, .78f * thin*Strength, thin);
        SetLine(glow, start, tip, 1.5f * thin*Strength, thin * .74f);
        SetLine(corona, start, tip, 2.4f * thin*Strength, thin * .13f);
        for (int s = 0; s < spirals.Length; s++)
        {
            var ribbon = spirals[s]; float span = Vector3.Distance(start, tip);
            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments, angle = t * Mathf.Min(span*.85f,Mathf.PI*5) - time * 4.2f*Tempo + s * Mathf.PI;
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                float radius = .52f *Strength* BeamWidth(Mathf.Min(t,.95f),time,beamLength) * (1+3*(1-thin));
                Vector3 center = Vector3.Lerp(start, tip, t) + radial * radius;
                float width = .34f * thin;
                ribbon.vertices[i * 2] = center - forward * width;
                ribbon.vertices[i * 2 + 1] = center + forward * width;
                float alpha = thin * Smooth(0, .04f, t) * (1 - Smooth(.9f, 1, t));
                ribbon.colors[i * 2] = ribbon.colors[i * 2 + 1] = new Color(thin, .88f*thin, .62f*thin, alpha);
            }
            Upload(ribbon, thin * grow);
        }
        for (int i = 0; i < airWaves.Length; i++)
        {
            float born=i*.65f;
            born+=Mathf.Floor(Mathf.Max(0,Mathf.Min(time,duration)-born)/1.3f)*1.3f;
            float elapsed = time-born, life = Mathf.Clamp01(elapsed / 1.05f);
            float dying=Smooth(.8f,1,life);
            float eased=1-Mathf.Pow(1-life,3);
            float distance = eased*17*Tempo, length = Vector3.Distance(start, end);
            float alpha = elapsed >= 0 && elapsed <= 1.05f ? Smooth(0,.06f,life)*(1-dying) : 0;
            Vector3 center = start + forward * Mathf.Min(length, distance);
            DrawArc(airWaves[i], center, right, up, (.55f + eased * .85f)*(1+dying*.55f), .12f*(1-dying*.6f),
                time *Tempo* (i % 2 == 0 ? 3 : -2.7f) + i * 1.8f, 5.3f, alpha, 1-dying);
        }
        DrawGround(chargeSeconds + time, true, 1);
        // A fresh, smaller burst follows the large cross as it dissolves into the beam.
        float burstTime=empowered&&time<duration?Mathf.Repeat(time,.24f):Mathf.Max(0,time-.04f);
        DrawParticles(burstTime, false, 0, (time>=.04f?1:0)*(empowered?thin:1-Smooth(.42f,.75f,time))*thin,empowered?1:.6f);
        sourceLight.transform.position = start; sourceLight.intensity = (3.6f + flash * 5) * thin;
        impactLight.transform.position = tip; impactLight.intensity = 2.5f * thin * grow;
    }

    void DrawGround(float time, bool afterRelease, float opacity)
    {
        for (int i = 0; i < groundWaves.Length; i++)
        {
            float born = i < 4 ? i * chargeSeconds * .20f : chargeSeconds + (i - 4) * .14f;
            if(i>=4&&afterRelease)born+=Mathf.Floor(Mathf.Max(0,Mathf.Min(time,chargeSeconds+duration)-born)/.70f)*.70f;
            float elapsed = time - born, life = Mathf.Clamp01(elapsed / 1.05f);
            float dying=Smooth(.8f,1,life);
            float alpha = elapsed >= 0 && elapsed <= 1.05f && (i < 4 || afterRelease) ? Smooth(0,.06f,life)*(1-dying)*.66f*opacity : 0;
            var ribbon=groundWaves[i];float radius=(.30f+2.5f*(1-Mathf.Pow(1-life,2)))*(1+dying*.45f);
            for(int p=0;p<=Segments;p++)
            {
                float angle=p*Mathf.PI*2/Segments+time*(i%2==0?1.4f:-1.2f);
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float height=(.20f+.30f*(1-life))*(.9f+.1f*Mathf.Sin(angle*5));
                ribbon.vertices[p*2]=ground+radial*radius+Vector3.up*.025f;
                ribbon.vertices[p*2+1]=ground+radial*(radius+.09f)+Vector3.up*height;
                ribbon.colors[p*2]=ribbon.colors[p*2+1]=new Color(1-dying,1-dying,1-dying,alpha);
            }
            Upload(ribbon,alpha);
        }
    }

    void DrawArc(Ribbon ribbon, Vector3 center, Vector3 axisX, Vector3 axisY, float radius, float width, float rotation, float span, float alpha, float brightness)
    {
        for (int i = 0; i <= Segments; i++)
        {
            float t = (float)i / Segments, angle = rotation + t * span;
            Vector3 radial = axisX * Mathf.Cos(angle) + axisY * Mathf.Sin(angle);
            float uneven = 1 + .04f * Mathf.Sin(angle * 7 + rotation), taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .7f);
            ribbon.vertices[i * 2] = center + radial * (radius * uneven - width * taper);
            ribbon.vertices[i * 2 + 1] = center + radial * (radius * uneven + width * taper);
            ribbon.colors[i * 2] = ribbon.colors[i * 2 + 1] = new Color(brightness, brightness, brightness, alpha * taper);
        }
        Upload(ribbon, alpha);
    }

    void Upload(Ribbon ribbon, float alpha)
    {
        ribbon.renderer.enabled = alpha > .002f;
        if (!ribbon.renderer.enabled) return;
        ribbon.mesh.vertices = ribbon.vertices; ribbon.mesh.colors = ribbon.colors; ribbon.mesh.RecalculateBounds();
        properties.Clear();properties.SetFloat("_EffectTime",visualTime*Tempo);ribbon.renderer.SetPropertyBlock(properties);
    }

    void DrawParticles(float time, bool inward, float gatherAlpha, float burstAlpha,float sizeScale=1)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            float phase = i * 2.39996f, z = Mathf.Sin(i * 17.31f) * .7f;
            Vector3 direction = (right * Mathf.Cos(phase) + up * Mathf.Sin(phase) + forward * z).normalized;
            float travel = inward ? Mathf.Repeat(time * 1.7f + i * .137f, 1) : Mathf.Clamp01(time*Tempo / (.35f + (i % 5) * .06f));
            float distance = inward ? Mathf.Lerp(2, .08f, travel) : .1f + travel * (1.3f + (i % 4) * .35f)*Strength;
            Vector3 center = start + direction * distance;
            if(empowered&&fired&&i>=36)
                center+=forward*Vector3.Distance(start,end)*(i-36)/48f*Smooth(0,.12f,visualTime-chargeSeconds);
            float alpha = inward ? Mathf.Sin(travel * Mathf.PI) * gatherAlpha : (1 - travel) * burstAlpha;
            float size = inward ? .07f + .12f * travel : .04f + .20f * (1 - travel);
            SetLine(particles[i], center - direction * size*sizeScale, center + direction * size * .4f*sizeScale, .035f * (1 - travel * .5f)*sizeScale, alpha);
        }
    }

    void DrawSprite(MeshRenderer renderer, Vector3 center, float size, float alpha, int shape, float spin)
    {
        renderer.enabled = size > .001f && alpha > .002f;
        if (!renderer.enabled) return;
        renderer.transform.position = center; renderer.transform.localScale = Vector3.one * size;
        properties.Clear(); properties.SetFloat("_Opacity", alpha); properties.SetFloat("_Shape", shape); properties.SetFloat("_Spin", spin);
        renderer.SetPropertyBlock(properties);
    }

    static float Smooth(float a, float b, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));
    static void Tint(LineRenderer line, float width, float alpha)
    {
        line.enabled = alpha > .002f && width > .001f; line.widthMultiplier = width;
        line.startColor = line.endColor = new Color(1, 1, 1, Mathf.Clamp01(alpha));
    }
    static void SetLine(LineRenderer line, Vector3 a, Vector3 b, float width, float alpha)
    {
        for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, Vector3.Lerp(a, b, (float)i / (line.positionCount - 1)));
        Tint(line, width, alpha);
    }

    // Explicit cleanup also covers edit-mode previews, which do not receive all MonoBehaviour messages.
    public void Dispose()
    {
        ReleaseMeshes();
        if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
    }
    void OnDestroy() => ReleaseMeshes();
    void ReleaseMeshes()
    {
        if(releaseWaves!=null){foreach(var wave in releaseWaves)if(wave!=null)wave.Dispose();releaseWaves=null;}
        foreach (var mesh in ownedMeshes)
        {
            if (mesh == null) continue;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        ownedMeshes.Clear();
        foreach(var material in ownedMaterials)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        ownedMaterials.Clear();
    }
}
