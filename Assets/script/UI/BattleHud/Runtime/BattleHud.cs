using UnityEngine;
using TMPro;

[DefaultExecutionOrder(19010)]
public sealed class BattleHud : MonoBehaviour
{
    public PlayerRoster selection;
    public CharacterWeaponEquipment equipment;
    public Canvas canvas;
    public RectTransform healthFill;
    public TMP_Text ballCount, skillStatus, skillName;
    public UnityEngine.UI.Image skillIcon, cooldown, keyHint;
    public UnityEngine.UI.Image[] weaponCards, typeIcons;
    public TMP_Text[] typeLabels;
    public Sprite[] elementSprites, skillSprites;
    CharacterMotor actor;
    CharacterCombatStats stats;
    SwordThrowSkill sword;
    OrbBeamSkill orb;
    void LateUpdate(){Refresh();}
    public void Refresh()
    {
        var current=selection!=null?selection.ActiveCharacter:null;
        canvas.enabled=current!=null;
        if(current==null)return;
        if(actor!=current){actor=current;stats=actor.GetComponent<CharacterCombatStats>();sword=actor.GetComponent<SwordThrowSkill>();orb=actor.GetComponent<OrbBeamSkill>();}
        float fraction=stats!=null?Mathf.Clamp01(stats.Health/Mathf.Max(1,stats.maximumHealth)):0;
        healthFill.anchorMax=new Vector2(fraction,1);
        healthFill.gameObject.SetActive(fraction>0);
        var collection=GameSession.Ensure();
        ballCount.text=collection.PokeBalls.ToString("00");
        int slot=equipment!=null?equipment.SelectedSlot:0;
        for(int i=0;i<3;i++){
            string type=collection.Progression.WeaponType(i);
            typeLabels[i].text=type.ToUpperInvariant();
            int index=System.Array.IndexOf(PokemonTypeChart.Types,type);
            typeIcons[i].sprite=elementSprites[Mathf.Max(0,index)];
            typeLabels[i].color=i==slot?new Color(.55f,1,.74f):new Color(.84f,.88f,.85f);
        }
        skillIcon.sprite=skillSprites[slot];
        bool available=slot==1?sword!=null&&equipment.HasSword:slot==2&&orb!=null&&equipment.HasOrb;
        float remaining=slot==1&&sword!=null?sword.CooldownRemaining:slot==2&&orb!=null?orb.CooldownRemaining:0;
        skillName.text=slot==1?"SWORD RECALL":slot==2?"ARCANE BEAM":"UNARMED";
        skillStatus.text=available?Mathf.CeilToInt(remaining).ToString():"-";
        skillIcon.color=available&&remaining<=0?Color.white:new Color(.3f,.3f,.3f,1f);
        cooldown.enabled=false;
    }
}

