using System.Collections.Generic;
using UnityEngine;

/// <summary>Successful capture owns the enemy pose until it is safely stored in this ball.</summary>
public sealed class PokeBallCapture : MonoBehaviour
{
    [Range(0,1)] public float captureChance=.5f;
    public GameObject closedVisual;
    public Transform upperShell,lowerShell;
    public Material beamMaterial;
    public PhysicsMaterial bounceMaterial;
    public bool IsBusy {get;private set;}
    public CharacterCombatStats CapturedEnemy {get;private set;}
    CharacterCombatStats target;
    readonly List<Behaviour> suspended=new List<Behaviour>();
    readonly List<Collider> colliders=new List<Collider>();
    Vector3 targetPosition,targetScale,ballPosition,ballScale,hoverPosition,targetCenter;
    Quaternion ballRotation,targetRotation;
    float elapsed,beamRadius;
    GameObject beam;
    Mesh beamMesh;
    readonly Vector3[] vertices=new Vector3[50];

    public bool TryCapture(CharacterCombatStats enemy)
    {
        if(IsBusy||CapturedEnemy!=null||enemy==null||!enemy.CanCapture||upperShell==null||lowerShell==null||closedVisual==null||Random.value>=captureChance)return false;
        if(!enemy.BeginCapture())return false;
        target=enemy;IsBusy=true;elapsed=0;
        targetPosition=enemy.transform.position;targetRotation=enemy.transform.rotation;targetScale=enemy.transform.localScale;
        ballPosition=transform.position;ballRotation=transform.rotation;ballScale=transform.localScale;
        var bounds=new Bounds(targetPosition+Vector3.up,Vector3.one);
        var skins=enemy.GetComponentsInChildren<SkinnedMeshRenderer>();
        if(skins.Length>0){bounds=skins[0].bounds;foreach(var skin in skins)bounds.Encapsulate(skin.bounds);}
        targetCenter=bounds.center;beamRadius=Mathf.Max(.6f,bounds.extents.x,bounds.extents.z)+.12f;
        var away=Vector3.ProjectOnPlane(ballPosition-targetCenter,Vector3.up).normalized;
        if(away.sqrMagnitude<.1f)away=-enemy.transform.forward;
        hoverPosition=targetCenter+away*.8f+Vector3.up*(bounds.extents.y+.65f);
        var rb=GetComponent<Rigidbody>();if(rb!=null){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;rb.detectCollisions=false;}
        foreach(var c in enemy.GetComponentsInChildren<Collider>())if(c.enabled){colliders.Add(c);c.enabled=false;}
        // Poise retains its value and recovery phase; its update already ignores capturing actors.
        foreach(var b in enemy.GetComponentsInChildren<Behaviour>())
            if(b.enabled&&b!=enemy&&!(b is EnemyPoise)){suspended.Add(b);b.enabled=false;}
        var driver=enemy.GetComponent<CharacterMotor>();driver.SetIncapacitated(true);
        CreateBeam();return true;
    }
    void Update()
    {
        if(!IsBusy)return;
        if(target==null){Abort();return;}
        elapsed+=Time.deltaTime;Sample(elapsed);
        if(elapsed>=2.35f)
        {
            CapturedEnemy=target;target.CompleteCapture();
            target.transform.SetPositionAndRotation(targetPosition,targetRotation);target.transform.localScale=targetScale;
            var collection = FindFirstObjectByType<global::CapturedEnemy>();
            if (collection != null) collection.Catch(target);
            else Debug.LogError("场景中没有 CapturedEnemy 管理器。", this);
            target=null;IsBusy=false;suspended.Clear();colliders.Clear();
            closedVisual.SetActive(true);upperShell.gameObject.SetActive(false);lowerShell.gameObject.SetActive(false);beam.SetActive(false);
            transform.localScale=ballScale;
            GetComponent<PokeBallFlight>()?.EnablePhysics(Vector3.down*.4f);
        }
    }
    // Kept independent of the frame loop for deterministic pose inspection in the Editor.
    void Sample(float seconds)
    {
        float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/.45f));
        transform.position=Vector3.Lerp(ballPosition,hoverPosition,rise);
        var forward=Vector3.ProjectOnPlane(targetCenter-hoverPosition,Vector3.up);
        transform.rotation=Quaternion.Slerp(ballRotation,Quaternion.LookRotation(forward.normalized,Vector3.up),rise);
        transform.localScale=ballScale;
        float open=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-.4f)/.35f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-2f)/.3f)));
        closedVisual.SetActive(open<=.001f);upperShell.gameObject.SetActive(open>.001f);lowerShell.gameObject.SetActive(open>.001f);
        upperShell.localRotation=Quaternion.Euler(-110*open,0,0);
        float absorb=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-.85f)/1.15f));
        float scale=Mathf.Lerp(1,.015f,absorb);
        target.transform.localScale=targetScale*scale;
        target.transform.position=Vector3.Lerp(targetPosition,transform.position-(targetCenter-targetPosition)*scale,absorb);
        beam.SetActive(seconds>=.7f&&seconds<2.05f);
        if(beam.activeSelf)UpdateBeam(Vector3.Lerp(targetPosition,transform.position,absorb),beamRadius*(1-absorb));
    }
    void CreateBeam()
    {
        beam=new GameObject("Capture Light Cone",typeof(MeshFilter),typeof(MeshRenderer));beam.transform.SetParent(transform,false);
        beamMesh=new Mesh{name="Capture cone"};beamMesh.MarkDynamic();
        var triangles=new int[48*3];for(int i=0;i<48;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
        beamMesh.vertices=vertices;beamMesh.triangles=triangles;
        beam.GetComponent<MeshFilter>().sharedMesh=beamMesh;
        var renderer=beam.GetComponent<MeshRenderer>();renderer.sharedMaterial=beamMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;beam.SetActive(false);
    }
    void UpdateBeam(Vector3 end,float radius)
    {
        vertices[0]=Vector3.zero;
        for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;vertices[i+1]=transform.InverseTransformPoint(end+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}
        beamMesh.vertices=vertices;beamMesh.RecalculateBounds();
    }
    void Abort()
    {
        if(target!=null)
        {
            target.CancelCapture();target.transform.SetPositionAndRotation(targetPosition,targetRotation);target.transform.localScale=targetScale;
            foreach(var b in suspended)if(b!=null)b.enabled=true;
            foreach(var c in colliders)if(c!=null)c.enabled=true;
            var driver=target.GetComponent<CharacterMotor>();driver.SetIncapacitated(target.GetComponent<EnemyPoise>()?.IsIncapacitated??false);
        }
        target=null;IsBusy=false;suspended.Clear();colliders.Clear();
        if(beam!=null)beam.SetActive(false);
        if(closedVisual!=null)closedVisual.SetActive(true);
        if(upperShell!=null)upperShell.gameObject.SetActive(false);
        if(lowerShell!=null)lowerShell.gameObject.SetActive(false);
        transform.localScale=ballScale;
        var rb=GetComponent<Rigidbody>();if(rb!=null){rb.isKinematic=false;rb.detectCollisions=true;}
    }
    void OnDisable(){if(IsBusy)Abort();}
    void OnDestroy(){if(beamMesh!=null)Destroy(beamMesh);}
}
