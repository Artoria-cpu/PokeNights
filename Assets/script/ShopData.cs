using UnityEngine;

public class ShopData : MonoBehaviour
{
    public static ShopData Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance()
    {
        Instance = null;
    }


    public int COin = 100;
    public int ballscount =20;

    public int healthlevel = 0;
    public int atklevel = 0;
    public int dflevel = 0;
    public int Shoplevel = 0;
    public int ShopPrice = 200;
    public int incomepers =1;
    public int Pricehealth = 100;
    public int priceatk = 100;
    public int pricedef = 100;

    public bool UnlockCity;
    public bool Unlockfactory;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        if (Instance != null)
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
