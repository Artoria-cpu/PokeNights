using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(20000)]
public sealed class TargetOutline : MonoBehaviour
{
    public Material material;
    sealed class Part
    {
        public Renderer source,outline;
        public SkinnedMeshRenderer sourceSkin,outlineSkin;
        public MeshFilter sourceMesh,outlineMesh;
        public Mesh mesh;
    }
    readonly List<Part> parts=new List<Part>();
    bool highlighted;
    public bool IsHighlighted=>highlighted&&parts.Exists(p=>p.outline!=null&&p.outline.enabled);

    public void SetHighlighted(bool active)
    {
        highlighted=active&&isActiveAndEnabled;
        if(highlighted&&material!=null&&(parts.Count==0||parts[0].outline==null)){parts.Clear();Build();}
        Sync();
    }
    void Build()
    {
        // Snapshot before adding renderers, so outline copies can never outline themselves.
        foreach(var source in GetComponentsInChildren<Renderer>(true))
        {
            if(source.name=="Lock outline"&&(source.gameObject.hideFlags&HideFlags.DontSave)!=0)continue;
            var skin=source as SkinnedMeshRenderer;
            var mesh=source is MeshRenderer?source.GetComponent<MeshFilter>():null;
            if((skin==null||skin.sharedMesh==null)&&(mesh==null||mesh.sharedMesh==null))continue;
            var go=new GameObject("Lock outline"){hideFlags=HideFlags.DontSave,layer=source.gameObject.layer};
            go.transform.SetParent(source.transform,false);
            var part=new Part{source=source,sourceSkin=skin,sourceMesh=mesh};
            if(skin!=null)
            {
                part.outlineSkin=go.AddComponent<SkinnedMeshRenderer>();part.outline=part.outlineSkin;
                part.outlineSkin.bones=skin.bones;part.outlineSkin.rootBone=skin.rootBone;
                part.outlineSkin.quality=skin.quality;part.outlineSkin.updateWhenOffscreen=false;
            }
            else
            {
                part.outlineMesh=go.AddComponent<MeshFilter>();part.outline=go.AddComponent<MeshRenderer>();
            }
            part.outline.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            part.outline.receiveShadows=false;part.outline.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            part.outline.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            parts.Add(part);
        }
    }
    void LateUpdate(){if(highlighted)Sync();}
    void Sync()
    {
        foreach(var part in parts)
        {
            if(part.outline==null)continue;
            bool visible=highlighted&&part.source!=null&&part.source.enabled&&part.source.gameObject.activeInHierarchy&&part.source.transform.IsChildOf(transform);
            part.outline.enabled=visible;if(!visible)continue;
            var mesh=part.sourceSkin!=null?part.sourceSkin.sharedMesh:part.sourceMesh.sharedMesh;
            if(mesh==null){part.outline.enabled=false;continue;}
            if(mesh!=part.mesh)
            {
                part.mesh=mesh;
                if(part.outlineSkin!=null)part.outlineSkin.sharedMesh=mesh;else part.outlineMesh.sharedMesh=mesh;
                var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=material;
                part.outline.sharedMaterials=materials;
            }
            if(part.outlineSkin!=null)
            {
                var bounds=part.sourceSkin.localBounds;bounds.Expand(.08f);part.outlineSkin.localBounds=bounds;
                for(int i=0;i<mesh.blendShapeCount;i++)part.outlineSkin.SetBlendShapeWeight(i,part.sourceSkin.GetBlendShapeWeight(i));
            }
        }
    }
    void OnDisable(){highlighted=false;Sync();}
    void OnDestroy()
    {
        foreach(var part in parts)if(part.outline!=null)
        {
            if(Application.isPlaying)Destroy(part.outline.gameObject);else DestroyImmediate(part.outline.gameObject);
        }
        parts.Clear();
    }
}
