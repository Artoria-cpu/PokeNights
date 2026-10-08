using UnityEngine;
using TMPro;

public sealed class TypeMatchupPanel : MonoBehaviour
{
    public PokemonTypeIcons icons;
    public UnityEngine.UI.Image[] strongIcons, weakIcons;
    public TMP_Text[] strongLabels, weakLabels;
    public TMP_Text strongEmpty, weakEmpty;
    string shown;
    public void Show(string type)
    {
        if(shown==type)return;
        shown=type;
        Fill(type,false,strongIcons,strongLabels,strongEmpty);
        Fill(type,true,weakIcons,weakLabels,weakEmpty);
    }
    void Fill(string type,bool incoming,UnityEngine.UI.Image[] images,TMP_Text[] labels,TMP_Text empty)
    {
        int count=0;
        foreach(var other in PokemonTypeChart.Types)
        {
            if((incoming?PokemonTypeChart.Against(other,type):PokemonTypeChart.Against(type,other))<=1)continue;
            images[count].sprite=icons.Get(other);labels[count].text=other;
            images[count].gameObject.SetActive(true);labels[count].gameObject.SetActive(true);count++;
        }
        for(int i=count;i<images.Length;i++){images[i].gameObject.SetActive(false);labels[i].gameObject.SetActive(false);}
        empty.gameObject.SetActive(count==0);
    }
}
