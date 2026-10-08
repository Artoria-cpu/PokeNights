using UnityEngine;
using TMPro;
using System.Collections.Generic;

[RequireComponent(typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler))]
public sealed class DamagePopupDisplay : MonoBehaviour
{
    public TMP_FontAsset font;
    public Color enemyDamageColor=new Color(1,.84f,.28f),playerDamageColor=new Color(1,.3f,.28f);
    public float lifetime=.95f;
    static DamagePopupDisplay instance;
    sealed class Popup {public TMP_Text text;public Vector3 position;public Vector2 drift;public Color color;public float age,tilt;}
    static readonly AnimationCurve punchScale=new AnimationCurve(
        new Keyframe(0,.12f,0,38),
        new Keyframe(.065f,1.85f,0,0),
        new Keyframe(.10f,1.85f,0,0),
        new Keyframe(.20f,.86f,0,0),
        new Keyframe(.29f,1.12f,0,0),
        new Keyframe(.39f,1,0,0));
    readonly List<Popup> popups=new List<Popup>();
    RectTransform canvasRect;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatic(){instance=null;}
    void Awake()
    {
        instance=this;var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=75;
        var scaler=GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        canvasRect=GetComponent<RectTransform>();if(font==null)font=TMP_Settings.defaultFontAsset;
    }
    public static float FontSizeFor(float damage)=>24+Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0,damage))*3,0,40);
    public static void Show(CharacterCombatStats victim,float damage)
    {
        if(victim==null||damage<0)return;
        if(instance==null)instance=FindFirstObjectByType<DamagePopupDisplay>();
        if(instance==null)instance=new GameObject("Damage Popups",typeof(RectTransform)).AddComponent<DamagePopupDisplay>();
        instance.Emit(victim,damage);
    }
    public void Emit(CharacterCombatStats victim,float damage)
    {
        if(canvasRect==null)Awake();
        if(popups.Count>=40)Remove(0);
        var go=new GameObject("Damage "+Mathf.RoundToInt(damage),typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(transform,false);
        var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.text=Mathf.RoundToInt(damage).ToString();text.fontSize=FontSizeFor(damage);text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        text.outlineColor=new Color32(18,12,20,255);text.outlineWidth=.2f;text.rectTransform.sizeDelta=new Vector2(260,110);text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);
        var color=victim.enemyOnly?enemyDamageColor:playerDamageColor;text.color=color;
        float multiplier=victim.LastTypeMultiplier;
        if(damage==0) text.text="IMMUNE";
        else if(!Mathf.Approximately(multiplier,1)) text.text+=$"\n<size=50%>x{multiplier:0.##}</size>";
        if(multiplier>1) color=new Color(1,.55f,.15f);
        else if(multiplier<1) color=new Color(.68f,.78f,.88f);
        text.color=color;
        var driver=victim.GetComponent<CharacterMotor>();var head=driver!=null&&driver.animator!=null&&driver.animator.isHuman?driver.animator.GetBoneTransform(HumanBodyBones.Head):null;
        var point=(head!=null?head.position:victim.transform.position+Vector3.up*1.7f)+Vector3.up*.25f;
        popups.Add(new Popup{text=text,position=point,color=color,drift=new Vector2(Random.Range(-35f,35f),75),tilt=Random.Range(-9f,9f)});
        text.transform.localScale=Vector3.one*.12f;
        text.enabled=false;
    }
    void LateUpdate(){Advance(Time.unscaledDeltaTime,Camera.main);}
    public void Advance(float seconds,Camera camera)
    {
        for(int i=popups.Count-1;i>=0;i--){var p=popups[i];p.age+=Mathf.Max(0,seconds);float t=p.age/Mathf.Max(.1f,lifetime);
            if(t>=1){Remove(i);continue;}
            if(camera==null){p.text.enabled=false;continue;}
            var screen=camera.WorldToScreenPoint(p.position);p.text.enabled=screen.z>0&&screen.x>=0&&screen.x<=Screen.width&&screen.y>=0&&screen.y<=Screen.height;
            if(!p.text.enabled)continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var local);
            float kick=1-Mathf.Pow(1-Mathf.Clamp01(p.age/.085f),3);
            float drift=Mathf.Clamp01((p.age-.13f)/Mathf.Max(.1f,lifetime-.13f));
            p.text.rectTransform.anchoredPosition=local+Vector2.up*(18*kick)+p.drift*drift;
            float scale=punchScale.Evaluate(p.age);
            float squash=Mathf.Sin(Mathf.Clamp01(p.age/.20f)*Mathf.PI)*.1f;
            p.text.transform.localScale=new Vector3(scale*(1+squash),scale*(1-squash),1);
            p.text.transform.localRotation=Quaternion.Euler(0,0,p.tilt*Mathf.Exp(-8*p.age));
            var color=Color.Lerp(Color.white,p.color,Mathf.Clamp01(p.age/.09f));
            color.a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,t));p.text.color=color;
        }
    }
    void Remove(int index){var text=popups[index].text;if(text!=null){if(Application.isPlaying)Destroy(text.gameObject);else DestroyImmediate(text.gameObject);}popups.RemoveAt(index);}
    void OnDestroy(){if(instance==this)instance=null;}
}
