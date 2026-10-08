using UnityEngine;

[CreateAssetMenu(menuName="Pokenight/Type Icons")]
public sealed class PokemonTypeIcons : ScriptableObject
{
    public Sprite[] sprites;
    public Sprite Get(string type)
    {
        int index=System.Array.IndexOf(PokemonTypeChart.Types,type);
        return sprites!=null&&index>=0&&index<sprites.Length?sprites[index]:null;
    }
}
