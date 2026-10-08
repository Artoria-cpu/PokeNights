using UnityEngine;
using TMPro;

public class ChangeTyeps : MonoBehaviour
{
    public int weapon = 1;
    public TMP_Text text;
    public TMP_Text fistText;
    public TMP_Text swordText;
    public TMP_Text magicText;


    public GameObject firegroups;
    public GameObject normalgroups;
    public GameObject watergroups;
    public GameObject electircgroups;
    public GameObject grassgroups;
    public GameObject icegroups;
    public GameObject fightgroups;
    public GameObject poisiongroups;
    public GameObject groundgroups;
    public GameObject flyinggroups;
    public GameObject pyhsicgroups;
    public GameObject buggroups;
    public GameObject rockgroups;
    public GameObject ghostgroups;
    public GameObject dragongroups;
    public GameObject darkgroups;
    public GameObject steelgroups;
    public GameObject fairygroups;




    public string tyypes;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        weapon = 1;
    }

    // Update is called once per frame
    void Update()
    {
        GameSession data = GameSession.Ensure();
        fistText.text = "FISTS: " + data.Progression.fistType;
        swordText.text = "SWORD: " + data.Progression.swordType;
        magicText.text = "MAGIC BALL: " + data.Progression.orbType;

        if(weapon == 1)
        {
            text.text = "Now Choossing: Fists";
        }

        if(weapon == 2)
        {
            text.text = "Now Choossing: Sword";
        }

        if(weapon == 3)
        {
            text.text = "Now Choossing: Magical Balls";
        }
    }

    public void fist()
    {
        weapon = 1;
    }

    public void sword()
    {
        weapon = 2;
    }

    public void magic()
    {
        weapon = 3;
    }

    public void changetypes(string type)
    {
        tyypes = type;
        GameSession data = GameSession.Ensure();
        if(weapon == 1)
        {
            data.Progression.fistType = tyypes;
        }
        else if(weapon == 2)
        {
            data.Progression.swordType = tyypes;
        }
        else if(weapon == 3)
        {
            data.Progression.orbType = tyypes;
        }
    }

    public void selectfire()
    {
        firegroups.SetActive(true);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectnormal()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(true);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectelectirc()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(true);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectice()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(true);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectelghost()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(true);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectdark()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(true);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectwater()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(true);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectdragon()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(true);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectgrass()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(true);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectsteel()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(true);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectbug()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(true);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectfight()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(true);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectground()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(true);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectpoison()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(true);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }
    public void selectflying()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(true);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectphysic()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(true);
        fairygroups.SetActive(false);
        rockgroups.SetActive(false);
    }

    public void selectfairy()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(true);
        rockgroups.SetActive(false);
    }

    public void selectrock()
    {
        firegroups.SetActive(false);
        normalgroups.SetActive(false);
        electircgroups.SetActive(false);
        icegroups.SetActive(false);
        ghostgroups.SetActive(false);
        darkgroups.SetActive(false);
        watergroups.SetActive(false);
        dragongroups.SetActive(false);
        grassgroups.SetActive(false);
        steelgroups.SetActive(false);
        buggroups.SetActive(false);
        fightgroups.SetActive(false);
        groundgroups.SetActive(false);
        poisiongroups.SetActive(false);
        flyinggroups.SetActive(false);
        pyhsicgroups.SetActive(false);
        fairygroups.SetActive(false);
        rockgroups.SetActive(true);
    }

}
