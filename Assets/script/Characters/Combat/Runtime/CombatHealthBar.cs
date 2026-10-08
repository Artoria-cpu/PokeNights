using UnityEngine;

[DisallowMultipleComponent, DefaultExecutionOrder(19000)]
public sealed class CombatHealthBar : MonoBehaviour
{
    public CharacterCombatStats stats;
    public PlayerRoster selection;
    public bool playerHud;
    public Canvas canvas;
    public RectTransform fill;
    public UnityEngine.UI.Text label;
    public PokemonTypeIcons iconSet;
    public UnityEngine.UI.Image[] typeIcons;
    [Header("Poise bar (leave off for a health bar)")]
    public bool showPoise;
    public EnemyPoise poise;
    public UnityEngine.UI.Image fillImage;
    public float headOffset=.55f,visibleDistance=25;
    public Color normalColor=new Color(1,.76f,.22f,1),lowColor=new Color(1,.27f,.22f,1);
    CharacterMotor driver;
    Transform head;
    void LateUpdate()
    {
        if(label!=null&&!playerHud&&!showPoise)label.enabled=false;
        if(playerHud&&!showPoise)
        {
            if(selection==null)selection=FindFirstObjectByType<PlayerRoster>();
            stats=selection!=null&&selection.ActiveCharacter!=null?selection.ActiveCharacter.GetComponent<CharacterCombatStats>():null;
        }
        if(canvas==null)return;
        GameObject actor;
        if(showPoise)
        {
            if(poise==null)poise=GetComponentInParent<EnemyPoise>();
            if(poise==null){canvas.enabled=false;return;}
            actor=poise.gameObject;
        }
        else
        {
            if(stats==null||(!playerHud&&!stats.enemyOnly)){canvas.enabled=false;return;}
            actor=stats.gameObject;
        }
        if(driver==null||driver.gameObject!=actor)
        {
            driver=actor.GetComponent<CharacterMotor>();
            head=driver!=null&&driver.animator!=null?driver.animator.GetBoneTransform(HumanBodyBones.Head):null;
        }
        if(driver==null){canvas.enabled=false;return;}
        var camera=driver.movementCamera!=null?driver.movementCamera:Camera.main;
        if(showPoise)
        {
            var player=poise.selection!=null?poise.selection.ActiveCharacter:null;
            canvas.enabled=!CharacterCombatStats.Dead(driver)&&poise.IsEnemy&&camera!=null&&(player==null||Vector3.Distance(player.transform.position,driver.transform.position)<=visibleDistance);
        }
        else
            canvas.enabled=playerHud||(stats.enemyOnly&&!CharacterCombatStats.Dead(stats)&&!driver.acceptPlayerInput&&camera!=null&&Vector3.Distance(camera.transform.position,driver.transform.position)<30);
        if(!canvas.enabled)return;
        if(!showPoise&&!playerHud&&typeIcons!=null&&iconSet!=null)
            for(int i=0;i<typeIcons.Length;i++)
            {
                bool visible=stats.elementalTypes!=null&&i<stats.elementalTypes.Length;
                typeIcons[i].gameObject.SetActive(visible);
                if(visible)typeIcons[i].sprite=iconSet.Get(stats.elementalTypes[i]);
            }
        if(showPoise||!playerHud)
        {
            float offset=showPoise?headOffset:.596f;
            transform.position=(head!=null?head.position:driver.transform.position+Vector3.up*1.7f)+Vector3.up*offset;
            transform.rotation=camera.transform.rotation;
            if(camera.WorldToViewportPoint(transform.position).z<=0){canvas.enabled=false;return;}
        }
        float amount=showPoise?poise.Current:stats.Health;
        float maximum=showPoise?poise.maximum:stats.maximumHealth;
        fill.anchorMax=new Vector2(Mathf.Clamp01(amount/Mathf.Max(1,maximum)),1);
        if(showPoise&&fillImage!=null)
            fillImage.color=poise.IsIncapacitated?new Color(.72f,.72f,.8f,1):poise.Current<poise.bigHitThreshold?lowColor:normalColor;
        if(!showPoise&&label!=null)label.text=playerHud?
            $"ATK {stats.attack:0}     DEF {stats.defense:0}"+(stats.IsDead?"   DEFEATED":""):
            "";
    }
}
