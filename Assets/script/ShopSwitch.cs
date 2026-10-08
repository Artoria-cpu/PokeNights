using UnityEngine;

public class ShopSwitch : MonoBehaviour
{
    public GameObject MenuGroup;
    public GameObject ExitGroup;
    public GameObject UpgradeGroup;
    public GameObject killGroup;

    public GameObject typesGroup;

    public GameObject UpgradessGroup;
    public GameObject ShopUpgradeGroup;

    public GameObject camera1;
    public GameObject camera2;

    public GameObject Processbutton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        camera1.SetActive(true);
        camera2.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PressExit()
    {
        MenuGroup.SetActive(false);
        ExitGroup.SetActive(true);

    }

    public void LeaveExit()
    {
        MenuGroup.SetActive(true);
        ExitGroup.SetActive(false);

    }

    public void PressKill()
    {
        MenuGroup.SetActive(false);
        killGroup.SetActive(true);
        camera1.SetActive(false);
        camera2.SetActive(true);
        Processbutton.SetActive(true);

    }

    public void LeaveKill()
    {
        MenuGroup.SetActive(true);
        killGroup.SetActive(false);
        camera1.SetActive(true);
        camera2.SetActive(false);

    }
    
    public void PressUpgrade()
    {
        MenuGroup.SetActive(false);
        UpgradeGroup.SetActive(true);

    }

    public void LeaveUpgrade()
    {
        MenuGroup.SetActive(true);
        UpgradeGroup.SetActive(false);

    }

    public void PressAtt()
    {
        typesGroup.SetActive(true);
        UpgradessGroup.SetActive(false);
        ShopUpgradeGroup.SetActive(false);

    }

    public void PressUpgradesss()
    {
        typesGroup.SetActive(false);
        UpgradessGroup.SetActive(true);
        ShopUpgradeGroup.SetActive(false);

    }
     public void PressShopUpgrades()
    {
        typesGroup.SetActive(false);
        UpgradessGroup.SetActive(false);
        ShopUpgradeGroup.SetActive(true);

    }

}
