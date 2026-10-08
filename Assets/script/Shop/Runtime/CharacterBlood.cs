using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public sealed class CharacterBlood : MonoBehaviour
{
    public Material stainMaterial;
    readonly List<GameObject> stains=new List<GameObject>();
    readonly List<Mesh> meshes=new List<Mesh>();
    public int Count=>stains.Count;
    public void AddHit(Vector3 attacker)
    {
        if(stainMaterial==null)return;
        var renderers=GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!(r.GetComponent<BloodPatch>()!=null)&&(r is SkinnedMeshRenderer||r.GetComponent<MeshFilter>()!=null)).ToArray();
        var parts=renderers.Where(r=>r.name.Contains("Head")||r.name.Contains("Tail")||r.name.Contains("Bulb")||r.name.Contains("Wing")).Where(r=>!r.name.Contains("Eye")&&!r.name.Contains("Flame")).ToArray();
        var body=renderers.FirstOrDefault(r=>r.name=="Body")??renderers.OfType<SkinnedMeshRenderer>().FirstOrDefault();
        if(body!=null)Paint(body,attacker,.20f);
        if(parts.Length>0)Paint(parts[Random.Range(0,parts.Length)],attacker,.18f);
        while(stains.Count>40)Remove(0);
    }
    void Paint(Renderer renderer,Vector3 attacker,float radius)
    {
        var skin=renderer as SkinnedMeshRenderer;var source=skin!=null?skin.sharedMesh:renderer.GetComponent<MeshFilter>().sharedMesh;
        if(source==null||!source.isReadable)return;
        Mesh baked=null;var posed=source;
        if(skin!=null){baked=new Mesh();skin.BakeMesh(baked);posed=baked;}
        try{
        var vertices=posed.vertices;var triangles=source.triangles;float best=float.NegativeInfinity;Vector3 point=Vector3.zero,normal=Vector3.forward;
        var toward=(attacker-renderer.bounds.center).normalized;
        for(int i=0;i<triangles.Length;i+=3){var a=renderer.transform.TransformPoint(vertices[triangles[i]]);var b=renderer.transform.TransformPoint(vertices[triangles[i+1]]);var c=renderer.transform.TransformPoint(vertices[triangles[i+2]]);var n=Vector3.Cross(b-a,c-a).normalized;float facing=Vector3.Dot(n,toward);if(facing<.1f)continue;float score=facing+Random.value*.7f;if(score>best){best=score;point=(a+b+c)/3;normal=n;}}
        if(float.IsNegativeInfinity(best))return;
        var tangent=Vector3.Cross(normal,Mathf.Abs(normal.y)>.9f?Vector3.right:Vector3.up).normalized;var bitangent=Vector3.Cross(normal,tangent);var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++){var delta=renderer.transform.TransformPoint(vertices[i])-point;uv[i]=new Vector2(Vector3.Dot(delta,tangent),Vector3.Dot(delta,bitangent))/radius;}
        var selected=new List<int>();
        for(int i=0;i<triangles.Length;i+=3){var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;var world=renderer.transform.TransformPoint(center);if(Vector3.Distance(world,point)>radius*3||Mathf.Abs(Vector3.Dot(world-point,normal))>radius*.6f)continue;selected.Add(triangles[i]);selected.Add(triangles[i+1]);selected.Add(triangles[i+2]);}
        var mesh=Instantiate(source);mesh.name="Attached blood patch";mesh.subMeshCount=1;mesh.SetTriangles(selected,0);mesh.uv=uv;
        var go=new GameObject("Blood stain");go.transform.SetParent(renderer.transform,false);go.AddComponent<BloodPatch>();
        Renderer overlay;
        if(skin!=null){var s=go.AddComponent<SkinnedMeshRenderer>();s.sharedMesh=mesh;s.bones=skin.bones;s.rootBone=skin.rootBone;s.localBounds=skin.localBounds;s.updateWhenOffscreen=true;overlay=s;}
        else {go.AddComponent<MeshFilter>().sharedMesh=mesh;overlay=go.AddComponent<MeshRenderer>();}
        overlay.sharedMaterial=stainMaterial;overlay.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;overlay.receiveShadows=false;
        stains.Add(go);meshes.Add(mesh);
        }finally{if(baked!=null)Release(baked);}
    }
    public void CleanUnder(Transform root)
    {for(int i=stains.Count-1;i>=0;i--)if(stains[i]!=null&&stains[i].transform.IsChildOf(root))Remove(i);}
    void Remove(int i){Release(stains[i]);Release(meshes[i]);stains.RemoveAt(i);meshes.RemoveAt(i);}
    static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    void OnDestroy(){foreach(var m in meshes)if(m!=null)Release(m);}
}



