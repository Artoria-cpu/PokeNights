using UnityEngine;

/// <summary>Places the authored particle prefab at a hit; all emission is configured in Particle System modules.</summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class LimbImpactParticles : MonoBehaviour
{
    [Tooltip("身上受击组面向相机。大小、寿命、数量直接在子物体的 Particle System 中调整。")]
    public Transform contactRoot;
    [Tooltip("尖刺与身上受击组共用生成位置，沿攻击方向喷发。")]
    public Transform exitRoot;
    [Min(.01f),Tooltip("空手命中的缩放倍率。剑命中使用预制体的原始 Scale。")]
    public float unarmedScale=.85f;

    ParticleSystem playback;
    Transform victim;
    Vector3 exitLocal;
    bool initialized;

    void Awake(){playback=GetComponent<ParticleSystem>();}

    public void Initialize(Camera camera,bool sword=false,Vector3 attackDirection=default,Collider target=null)
    {
        if(playback==null)playback=GetComponent<ParticleSystem>();
        playback.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        Vector3 direction=attackDirection.sqrMagnitude>.001f?attackDirection.normalized:Vector3.forward;
        transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)>.99f?Vector3.forward:Vector3.up);
        if(!sword)transform.localScale*=unarmedScale;
        camera=camera!=null?camera:Camera.main;
        if(contactRoot!=null&&camera!=null)
        {
            contactRoot.rotation=camera.transform.rotation;
            Vector3 toward=(camera.transform.position-transform.position).normalized;
            contactRoot.position=target!=null?
                target.ClosestPoint(transform.position+toward*(target.bounds.extents.magnitude+1))+toward*.035f:
                contactRoot.position+toward*.035f;
        }
        if(exitRoot!=null)
        {
            exitRoot.position=contactRoot!=null?contactRoot.position:transform.position;
            if(target!=null){victim=target.transform;exitLocal=victim.InverseTransformPoint(exitRoot.position);}
        }
        // Start Delay, Bursts, sizes, colours and lifetimes are never replaced at runtime.
        playback.Play(true);
        initialized=true;
    }

    void LateUpdate()
    {
        if(!initialized)return;
        // Only the emitter follows the target; emitted world-space particles continue their flight.
        if(victim!=null&&exitRoot!=null)
        {
            exitRoot.position=victim.TransformPoint(exitLocal);
            if(contactRoot!=null)contactRoot.position=exitRoot.position;
        }
        if(!playback.IsAlive(true))Destroy(gameObject);
    }
}
