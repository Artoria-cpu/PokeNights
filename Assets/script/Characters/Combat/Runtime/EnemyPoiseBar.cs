using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(19000)]
public sealed class EnemyPoiseBar : MonoBehaviour
{
    public EnemyPoise poise;
    public Canvas canvas;
    public RectTransform fill;
    public UnityEngine.UI.Image fillImage;
    public float headOffset=.55f,visibleDistance=25;
    public Color normalColor=new Color(1,.76f,.22f,1),lowColor=new Color(1,.27f,.22f,1);
    CharacterMotor driver;
    Transform head;
    void Awake()
    {
        if(poise==null)poise=GetComponentInParent<EnemyPoise>();
        driver=poise.GetComponent<CharacterMotor>();
        head=driver.animator!=null?driver.animator.GetBoneTransform(HumanBodyBones.Head):null;
    }
    void LateUpdate()
    {
        if(poise==null||canvas==null||driver==null)return;
        var camera=driver.movementCamera!=null?driver.movementCamera:Camera.main;
        var player=poise.selection!=null?poise.selection.ActiveCharacter:null;
        canvas.enabled=!CharacterCombatStats.Dead(driver)&&poise.IsEnemy&&camera!=null&&(player==null||Vector3.Distance(player.transform.position,driver.transform.position)<=visibleDistance);
        if(!canvas.enabled)return;
        transform.position=(head!=null?head.position:driver.transform.position+Vector3.up*1.7f)+Vector3.up*headOffset;
        transform.rotation=camera.transform.rotation;
        if(camera.WorldToViewportPoint(transform.position).z<=0){canvas.enabled=false;return;}
        fill.anchorMax=new Vector2(Mathf.Clamp01(poise.Current/Mathf.Max(1,poise.maximum)),1);
        fillImage.color=poise.IsIncapacitated?new Color(.72f,.72f,.8f,1):poise.Current<poise.bigHitThreshold?lowColor:normalColor;
    }
}
