using UnityEngine;
using UnityEngine.SceneManagement;
public class Win : MonoBehaviour
{
    public GameObject Winpanel;
    public ShopData data;
    public GameObject shop;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void openpanel()
    {
        shop.SetActive(false);
        Winpanel.SetActive(true);
    }

    public void closepanel()
    {
        shop.SetActive(true);
        Winpanel.SetActive(false);
    }

    public void buy()
    {
        if(data.COin >= 10000)
        {
            SceneManager.LoadScene("Win");
        }
    }
}
