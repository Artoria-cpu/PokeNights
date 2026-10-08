using UnityEngine;

/// <summary>Each enemy owns its visual weapon independently of the player's inventory.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10510)]
public sealed class EnemyWeaponLoadout : MonoBehaviour
{
    public int Slot { get; private set; }
    public Transform Weapon { get; private set; }
    CharacterMotor driver;
    public void Initialize(CharacterWeaponEquipment equipment,int slot)
    {
        if(driver!=null)return;
        driver=GetComponent<CharacterMotor>();
        var stats=GetComponent<CharacterCombatStats>();
        if(stats==null||!stats.enemyOnly||equipment==null)return;
        var prefab=slot==1?equipment.swordPrefab:slot==2?equipment.orbPrefab:null;
        if(prefab==null)return;
        Transform socket=null;
        if(slot==1){foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="SwordHandSocket"){socket=t;break;}if(socket==null)return;}
        Weapon=Instantiate(prefab,slot==1?socket:transform,false).transform;
        Weapon.name=slot==1?"Enemy sword":"Enemy magic orb";
        if(slot==1){Weapon.localPosition=Vector3.zero;Weapon.localRotation=Quaternion.identity;Weapon.localScale=Vector3.one;}
        var follower=Weapon.GetComponent<FloatingOrbFollower>();if(follower!=null)follower.enabled=false;
        foreach(var collider in Weapon.GetComponentsInChildren<Collider>())collider.enabled=false;
        Slot=slot;
        var template=equipment.selection!=null?equipment.selection.ActiveCharacter:null;
        if(template==null&&equipment.selection!=null)foreach(var actor in equipment.selection.characters)if(actor!=null&&!actor.GetComponent<CharacterCombatStats>().enemyOnly){template=actor;break;}
        if(slot==2){var orb=GetComponent<OrbWeapon>()??gameObject.AddComponent<OrbWeapon>();var source=template!=null?template.GetComponent<OrbWeapon>():null;if(source!=null)orb.effectMaterial=source.effectMaterial;}
        if(slot==1){var slash=GetComponent<SwordSlashEffect>()??gameObject.AddComponent<SwordSlashEffect>();var source=template!=null?template.GetComponent<SwordSlashEffect>():null;if(source!=null){slash.material=source.material;slash.emberMaterial=source.emberMaterial;slash.windMaterial=source.windMaterial;}}
        var ai=GetComponent<PartyEnemyAI>();if(ai!=null){ai.attackRange=slot==2?7f:2f;ai.preferredDistance=slot==2?6f:1.7f;ai.lockRange=slot==2?10f:6f;ai.orbitDistance=slot==2?7f:4f;}
    }
    void LateUpdate()
    {
        if(driver==null||Weapon==null)return;
        if(Slot==2){var body=driver.visualRoot!=null?driver.visualRoot:transform;Weapon.position=transform.position+Vector3.up*(1.45f+Mathf.Sin(Time.time*1.8f)*.045f)+body.right*.65f;Weapon.rotation=Quaternion.Euler(0,body.eulerAngles.y,0);}
        if(CharacterCombatStats.Dead(this)){Weapon.gameObject.SetActive(false);return;}
        driver.animator.SetFloat("SwordBlend",Slot==1?1:0);driver.animator.SetFloat("OrbBlend",Slot==2?1:0);
        int layer=driver.animator.GetLayerIndex("WeaponAction");
        if(Slot==1&&layer>=0&&!driver.IsAttacking&&!driver.IsIncapacitated&&!driver.IsHitStunned){driver.animator.SetLayerWeight(layer,1);if(!driver.animator.GetCurrentAnimatorStateInfo(layer).IsName("WeaponAction.Held"))driver.animator.Play("WeaponAction.Held",layer,0);}
    }
}
