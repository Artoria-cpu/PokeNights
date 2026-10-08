using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class BattleUI : MonoBehaviour
{
    public CharacterCombatStats playerstats;
    public Slider healthbar;
    public TMP_Text ballcount;
    public ShopData data;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        data = FindFirstObjectByType<ShopData>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
