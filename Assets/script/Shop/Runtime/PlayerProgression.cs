using System;
using UnityEngine;

[Serializable]
public sealed class PlayerProgression
{
    public const int MaximumLevel = 5;
    public int healthLevel
    {
        get { return UnityEngine.Object.FindFirstObjectByType<ShopData>().healthlevel; }
        set { UnityEngine.Object.FindFirstObjectByType<ShopData>().healthlevel = value; }
    }
    public int attackLevel
    {
        get { return UnityEngine.Object.FindFirstObjectByType<ShopData>().atklevel; }
        set { UnityEngine.Object.FindFirstObjectByType<ShopData>().atklevel = value; }
    }
    public int defenseLevel
    {
        get { return UnityEngine.Object.FindFirstObjectByType<ShopData>().dflevel; }
        set { UnityEngine.Object.FindFirstObjectByType<ShopData>().dflevel = value; }
    }
    public string fistType = "Normal";
    public string swordType = "Normal", orbType = "Normal";
    public int Level(int stat) => stat == 0 ? healthLevel : stat == 1 ? attackLevel : defenseLevel;
    public string WeaponType(int slot) => slot == 1 ? swordType : slot == 2 ? orbType : fistType;
    public bool SetWeaponType(int slot, string type)
    {
        if((slot < 0 || slot > 2) || !PokemonTypeChart.Valid(type)) return false;
        if(slot == 0) fistType = type; else if(slot == 1) swordType = type; else orbType = type;
        return true;
    }
    public bool Upgrade(int stat)
    {
        if(stat < 0 || stat > 2 || Level(stat) >= MaximumLevel) return false;
        EarnAndCost shop = UnityEngine.Object.FindFirstObjectByType<EarnAndCost>();
        if (shop == null)
        {
            return false;
        }
        int before = Level(stat);
        if (stat == 0)
        {
            shop.UpGradeHealth();
        }
        else if (stat == 1)
        {
            shop.UpGradeAtk();
        }
        else if (stat == 2)
        {
            shop.UpGradedef();
        }
        if (Level(stat) == before)
        {
            return false;
        }
        foreach(var actor in UnityEngine.Object.FindObjectsByType<CharacterCombatStats>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            actor.ApplyProgression(this);
        return true;
    }
    public static string AttackType(CharacterMotor actor, int weaponSlot = 0)
    {
        var stats = actor != null ? actor.GetComponent<CharacterCombatStats>() : null;
        if(stats != null && stats.enemyOnly)
            return stats.elementalTypes != null && stats.elementalTypes.Length > 0 ? stats.elementalTypes[0] : "Normal";
        return GameSession.Instance != null ? GameSession.Instance.Progression.WeaponType(weaponSlot) : "Normal";
    }
}
