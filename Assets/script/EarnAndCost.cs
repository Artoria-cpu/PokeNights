using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class EarnAndCost : MonoBehaviour
{
    public ShopData data;
    public int pricehealth1= 175;
    public int pricehealth2= 275;
    public int pricehealth3= 400;
    public int pricehealth4= 550;
    public int priceatk1= 175;
    public int priceatk2= 275;
    public int priceatk3= 400;
    public int priceatk4= 550;
    public int pricedef1= 175;
    public int pricedef2= 275;
    public int pricedef3= 400;
    public int pricedef4= 550;

    public TMP_Text COinText;
    public TMP_Text BallText;
    public TMP_Text AtkText;
    public TMP_Text DefText;
    public TMP_Text HPText;
    public TMP_Text ShopText;

    public TMP_Text IncomeText;
    float timer = 0;

    public GameObject blakc;

    public GameObject newfactorybutton;
    public GameObject newcitybutton;

    public GameObject oldfactorybutton;
    public GameObject oldcitybutton;

    public TMP_Text healthPriceText;
    public TMP_Text attackPriceText;
    public TMP_Text defensePriceText;
    public TMP_Text shopPriceText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        data = FindFirstObjectByType<ShopData>();
    }

    void Start()
    {
        RefreshUnlockButtons();
    }

    void RefreshUnlockButtons()
    {
        if (data.UnlockCity == true)
        {
            oldcitybutton.SetActive(false);
            newcitybutton.SetActive(true);
        }
        else
        {
            oldcitybutton.SetActive(true);
            newcitybutton.SetActive(false);
        }

        if (data.Unlockfactory == true)
        {
            oldfactorybutton.SetActive(false);
            newfactorybutton.SetActive(true);
        }
        else
        {
            oldfactorybutton.SetActive(true);
            newfactorybutton.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {

        healthPriceText.text = data. Pricehealth.ToString();
        attackPriceText.text = data.priceatk.ToString();
        defensePriceText.text = data.pricedef.ToString();
        shopPriceText.text = data.ShopPrice.ToString();

        if(data.UnlockCity == true)
        {
            oldcitybutton.SetActive(false);
        }

         if(data.Unlockfactory == true)
        {
            oldfactorybutton.SetActive(false);
        }

        COinText.text = data.COin.ToString();
        BallText.text = data.ballscount.ToString();
        AtkText.text = data.atklevel.ToString();
        DefText.text = data.dflevel.ToString();
        HPText.text = data.healthlevel.ToString();
        ShopText.text = data.Shoplevel.ToString();
        IncomeText.text = data.incomepers.ToString();

        timer = timer + Time.deltaTime;
        if(timer >= 1)
        {
            timer = timer -1;
            data.COin = data.COin + data.incomepers;
        }
    }

    public void BuyBall()
    {
        if(data.COin >= 50)
        {
            data.COin = data.COin - 50;
            data.ballscount = data.ballscount + 20;
        }
    }

    public void UpGradeHealth()
    {
        if(data.healthlevel >= 5)
        {
            return;
        }

        if(data.COin >= data.Pricehealth)
        {
            data.COin = data.COin - data.Pricehealth;
            data.healthlevel = data.healthlevel + 1;
            if(data.Pricehealth < pricehealth1)
            {
                data.Pricehealth = pricehealth1;
            }

            else if(data.Pricehealth < pricehealth2)
            {
                data.Pricehealth = pricehealth2;
            } 
            
            else if(data.Pricehealth < pricehealth3)
            {
                data.Pricehealth = pricehealth3;
            } 
            
            else if(data.Pricehealth < pricehealth4)
            {
                data.Pricehealth = pricehealth4;
            }
        }
    }

    public void UpGradeAtk()
    {
         if(data.atklevel >= 5)
        {
            return;
        }

        if(data.COin >= data.priceatk)
        {
            data.COin = data.COin - data.priceatk;
            data.atklevel = data.atklevel + 1;
            if(data.priceatk < priceatk1)
            {
                data.priceatk = priceatk1;
            }

            else if(data.priceatk < priceatk2)
            {
                data.priceatk = priceatk2;
            } 
            
            else if(data.priceatk < priceatk3)
            {
                data.priceatk = priceatk3;
            } 
            
            else if(data.priceatk < priceatk4)
            {
                data.priceatk = priceatk4;
            }
        }
    }

    public void UpGradedef()
    {
         if(data.dflevel >= 5)
        {
            return;
        }

        if(data.COin >= data.pricedef)
        {
            data.COin = data.COin - data.pricedef;
            data.dflevel = data.dflevel + 1;
            if(data.pricedef < pricedef1)
            {
                data.pricedef = pricedef1;
            }

            else if(data.pricedef < pricedef2)
            {
                data.pricedef = pricedef2;
            } 
            
            else if(data.pricedef < pricedef3)
            {
                data.pricedef = pricedef3;
            } 
            
            else if(data.pricedef < pricedef4)
            {
                data.pricedef = pricedef4;
            }
        }
    }

    public void LockFactory()
    {
        if(data.Unlockfactory == true)
        {
            return;
        }
        if(data.COin >= 300)
        {
            data.COin = data.COin - 300;
            data.Unlockfactory = true;
            oldfactorybutton.SetActive(false);
            newfactorybutton.SetActive(true);
        }
    }

    public void LockCity()
    {
        if(data.UnlockCity == true)
        {
            return;
        }
        if(data.COin >= 500)
        {
            data.COin = data.COin - 500;
            data.UnlockCity = true;
            oldcitybutton.SetActive(false);
            newcitybutton.SetActive(true);
        }
    }
    
    public void UpgradeShop()
    {
        if(data.Shoplevel >= 5)
        {
            return;
        }

        if(data.COin >= data.ShopPrice)
        {
            data.COin = data.COin - data.ShopPrice;
            data.Shoplevel = data.Shoplevel + 1;
            data.ShopPrice = data.ShopPrice + 200;
            data.incomepers = data.incomepers * 2;
        }

    }

    IEnumerator topark()
    {
        blakc.SetActive(true);
        yield return new WaitForSeconds(2);
         SceneManager.LoadScene("CentralPark");
    }

    IEnumerator tofact()
    {
        blakc.SetActive(true);
        yield return new WaitForSeconds(2);
         SceneManager.LoadScene("Factory");
    }

    IEnumerator tocity()
    {
        blakc.SetActive(true);
        yield return new WaitForSeconds(2);
         SceneManager.LoadScene("City");
    }

    public void toparkcheck()
    {
        StartCoroutine(topark());

    }

    public void tofactorycheck()
    {
        if(data.Unlockfactory == true)
        {
             StartCoroutine(tofact());
        }
        

    }

    public void tocitycheck()
    {
        if(data.UnlockCity == true)
        {
             StartCoroutine(tocity());
        }
        

    }

}

