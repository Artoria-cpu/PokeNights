using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small render-only hit shake that never feeds back into camera controls.</summary>
[DisallowMultipleComponent]
public sealed class CombatCameraShake : MonoBehaviour
{
    Camera target;
    float started=float.NegativeInfinity,duration=.105f,positionAmount,rotationAmount;
    Vector3 appliedPosition;
    Quaternion appliedRotation=Quaternion.identity;
    bool applied;

    void OnEnable()
    {
        target=GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering+=BeginCamera;
        RenderPipelineManager.endCameraRendering+=EndCamera;
    }
    public void Trigger(bool sword,bool finisher=false)
    {
        if(Time.unscaledTime-started>=duration)positionAmount=rotationAmount=0;
        started=Time.unscaledTime;
        float strength=finisher?1.3f:1;
        duration=finisher?.126f:.105f;
        positionAmount=Mathf.Min(.016f*strength,positionAmount+(sword?.012f:.008f)*strength);
        rotationAmount=Mathf.Min(.22f*strength,rotationAmount+(sword?.16f:.10f)*strength);
    }
    void BeginCamera(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=target||applied)return;
        float age=(Time.unscaledTime-started)/duration;
        if(age<0||age>=1){positionAmount=rotationAmount=0;return;}
        float envelope=(1-age)*(1-age);
        float phase=age*31f;
        appliedPosition=new Vector3(Mathf.Sin(phase*2.17f),Mathf.Sin(phase*2.83f+.7f),0)*positionAmount*envelope;
        appliedRotation=Quaternion.Euler(Mathf.Sin(phase*2.41f)*rotationAmount*envelope,Mathf.Sin(phase*1.89f+.4f)*rotationAmount*envelope,Mathf.Sin(phase*2.67f+1.1f)*rotationAmount*.45f*envelope);
        transform.localPosition+=appliedPosition;
        transform.localRotation*=appliedRotation;
        applied=true;
    }
    void EndCamera(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=target||!applied)return;
        transform.localRotation*=Quaternion.Inverse(appliedRotation);
        transform.localPosition-=appliedPosition;
        applied=false;
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering-=BeginCamera;
        RenderPipelineManager.endCameraRendering-=EndCamera;
        if(applied){transform.localRotation*=Quaternion.Inverse(appliedRotation);transform.localPosition-=appliedPosition;applied=false;}
    }
}
