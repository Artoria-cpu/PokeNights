using System.Collections.Generic;
using UnityEngine;

/// <summary>Animated skin shells shed world-space black fragments during the flying attack.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(9200)]
public sealed class OrbDashDissolve : MonoBehaviour
{
    public Material shellMaterial, fragmentMaterial;
    public float fragmentsPerSecond = 240f;
    readonly List<SkinnedMeshRenderer> sources = new List<SkinnedMeshRenderer>();
    readonly List<SkinnedMeshRenderer> shells = new List<SkinnedMeshRenderer>();
    readonly List<Transform> emitBones = new List<Transform>();
    MaterialPropertyBlock block;
    ParticleSystem fragments;
    float envelope, budget, phase;
    bool emitting;
    static readonly int Coverage=Shader.PropertyToID("_Coverage"), Phase=Shader.PropertyToID("_Phase");
    public void Begin(){Build();emitting=true;budget=0;}
    public void Release(){emitting=false;}
    void Build()
    {
        if(fragments!=null||shellMaterial==null||fragmentMaterial==null)return;
        block=new MaterialPropertyBlock();
        foreach(var source in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(source.sharedMesh==null||!source.enabled)continue;
            var go=new GameObject("Black dissolving shell");go.transform.SetParent(source.transform,false);
            var shell=go.AddComponent<SkinnedMeshRenderer>();shell.sharedMesh=source.sharedMesh;shell.bones=source.bones;shell.rootBone=source.rootBone;
            shell.localBounds=source.localBounds;shell.updateWhenOffscreen=true;
            var materials=new Material[source.sharedMesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=shellMaterial;
            shell.sharedMaterials=materials;shell.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shell.receiveShadows=false;shell.enabled=false;
            sources.Add(source);shells.Add(shell);
        }
        var driver=GetComponent<CharacterMotor>();
        foreach(var bone in new[]{HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.Hips,HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
        {var t=driver.animator.GetBoneTransform(bone);if(t!=null)emitBones.Add(t);}
        var obj=new GameObject("Falling black fragments");obj.transform.SetParent(transform,false);fragments=obj.AddComponent<ParticleSystem>();fragments.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=fragments.main;main.playOnAwake=false;main.loop=true;main.duration=1;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=500;main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.8f);main.startSize=new ParticleSystem.MinMaxCurve(.04f,.15f);main.startSpeed=0;main.gravityModifier=.45f;main.startColor=Color.white;
        var emission=fragments.emission;emission.enabled=false;var shape=fragments.shape;shape.enabled=false;
        var color=fragments.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.35f),new GradientAlphaKey(0,1)});color.color=gradient;
        var size=fragments.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.2f,1),new Keyframe(1,.1f)));
        var renderer=fragments.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=fragmentMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        fragments.Play();
    }
    void LateUpdate()
    {
        if(fragments==null||Time.deltaTime<=0)return;
        phase+=Time.deltaTime;envelope=Mathf.MoveTowards(envelope,emitting?1:0,Time.deltaTime*(emitting?12:6));
        block.SetFloat(Coverage,envelope);block.SetFloat(Phase,phase);
        for(int i=0;i<shells.Count;i++)
        {
            var shell=shells[i];var source=sources[i];if(shell==null||source==null)continue;
            shell.enabled=envelope>.001f&&source.enabled;
            if(!shell.enabled)continue;
            shell.sharedMesh=source.sharedMesh;
            for(int b=0;b<source.sharedMesh.blendShapeCount;b++)shell.SetBlendShapeWeight(b,source.GetBlendShapeWeight(b));
            shell.SetPropertyBlock(block);
        }
        if(!emitting||emitBones.Count==0)return;
        budget+=fragmentsPerSecond*Time.deltaTime;
        int count=Mathf.Min(48,Mathf.FloorToInt(budget));budget-=count;
        for(int i=0;i<count;i++)
        {
            var bone=emitBones[Random.Range(0,emitBones.Count)];
            var position=bone.position+Random.insideUnitSphere*.13f;
            var emit=new ParticleSystem.EmitParams{position=position,velocity=-transform.forward*Random.Range(.3f,1.5f)+Random.insideUnitSphere*.4f,rotation=Random.Range(0,360)};
            fragments.Emit(emit,1);
        }
    }
    void OnDisable()
    {
        emitting=false;envelope=0;
        foreach(var shell in shells)if(shell!=null)shell.enabled=false;
        if(fragments!=null)fragments.Clear();
    }
}
