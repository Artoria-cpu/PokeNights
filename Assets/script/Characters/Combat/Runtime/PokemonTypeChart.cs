using System;

// Modern 18-type chart: https://sg.portal-pokemon.com/game/type-chart/
public static class PokemonTypeChart
{
    public static readonly string[] Types = { "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel", "Fairy" };
    public static readonly string[] Labels = Types;
    static readonly string[] strong = { "", "Grass Ice Bug Steel", "Fire Ground Rock", "Water Flying", "Water Ground Rock", "Grass Ground Flying Dragon", "Normal Ice Rock Dark Steel", "Grass Fairy", "Fire Electric Poison Rock Steel", "Grass Fighting Bug", "Fighting Poison", "Grass Psychic Dark", "Fire Ice Flying Bug", "Psychic Ghost", "Dragon", "Psychic Ghost", "Ice Rock Fairy", "Fighting Dragon Dark" };
    static readonly string[] weak = { "Rock Steel", "Fire Water Rock Dragon", "Water Grass Dragon", "Electric Grass Dragon", "Fire Grass Poison Flying Bug Dragon Steel", "Fire Water Ice Steel", "Poison Flying Psychic Bug Fairy", "Poison Ground Rock Ghost", "Grass Bug", "Electric Rock Steel", "Psychic Steel", "Fire Fighting Poison Flying Ghost Steel Fairy", "Fighting Ground Steel", "Dark", "Steel", "Fighting Dark Fairy", "Fire Water Electric Steel", "Fire Poison Steel" };
    static readonly string[] immune = { "Ghost", "", "", "Ground", "", "", "Ghost", "Steel", "Flying", "", "Dark", "", "", "Normal", "Fairy", "", "", "" };
    public static bool Valid(string type) => Array.IndexOf(Types, type) >= 0;
    public static string Label(string type) { int i = Array.IndexOf(Types, type); return i < 0 ? type : Labels[i]; }
    public static float Against(string attack, string defense)
    {
        int i = Array.IndexOf(Types, attack);
        if(i < 0 || !Valid(defense)) return 1;
        if(Contains(immune[i], defense)) return 0;
        if(Contains(strong[i], defense)) return 2;
        return Contains(weak[i], defense) ? .5f : 1;
    }
    static bool Contains(string row, string type) => (" " + row + " ").Contains(" " + type + " ");
    public static float Multiplier(string attack, string[] defenses)
    {
        if(defenses == null || defenses.Length == 0) return Against(attack, "Normal");
        float result = 1;
        for(int i = 0; i < defenses.Length; i++)
            if(Array.IndexOf(defenses, defenses[i]) == i) result *= Against(attack, defenses[i]);
        return result;
    }
}
