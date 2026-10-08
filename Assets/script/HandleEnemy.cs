using UnityEngine;

public class HandleEnemy : MonoBehaviour
{
    public ProcessEnemy qte;
    public ShopDisplay display;
    public GameObject effect;
    public EarnAndCost wallet;
    public GameObject sellbutton;
    public GameObject processbutton;
    public HideBody target;

    void Update()
    {
        if (qte.completed == true)
        {
            qte.completed = false;

            if (target != null)
            {
                if (target.process == false)
                {
                    if (target.sold == false)
                    {
                        target.Hide();
                        Instantiate(effect, new Vector3(27.75f, 1.35f, 0.02f), Quaternion.identity);
                    }
                }
            }
        }

        Refreshbuttons();
    }

    public void Refreshbuttons()
    {

        if (qte.running == true)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(false);
            return;
        }

        if (display.captured == null)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(false);
            return;
        }

        Transform handle = display.captured.forever.transform;
        if (handle.childCount == 0)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(false);
            return;
        }

        Transform enemy = handle.GetChild(display.order);
        HideBody work = enemy.GetComponent<HideBody>();

        if (work == null)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(false);
            return;
        }

        if (work.sold == true)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(false);
            return;
        }

        if (work.process == true)
        {
            processbutton.SetActive(false);
            sellbutton.SetActive(true);
        }
        else
        {
            sellbutton.SetActive(false);
            processbutton.SetActive(true);
        }
    }

    public void BeginProcess()
    {
        if (qte.running == true)
        {
            return;
        }

        if (display.captured == null)
        {
            return;
        }

        Transform handle = display.captured.forever.transform;
        if (handle.childCount == 0)
        {
            return;
        }

        Transform enemy = handle.GetChild(display.order);
        HideBody work = enemy.GetComponent<HideBody>();

        if (work == null)
        {
            return;
        }

        if (work.process == true)
        {
            return;
        }

        if (work.sold == true)
        {
            return;
        }

        target = work;
        qte.KillingStart();
        Refreshbuttons();
    }

    public void Sellit()
    {
        if (qte.running == true)
        {
            return;
        }

        if (display.captured == null)
        {
            return;
        }

        Transform handle = display.captured.forever.transform;
        if (handle.childCount == 0)
        {
            return;
        }

        Transform enemy = handle.GetChild(display.order);
        HideBody work = enemy.GetComponent<HideBody>();

        if (work == null)
        {
            return;
        }

        if (work.process == false)
        {
            return;
        }

        if (work.sold == true)
        {
            return;
        }

        work.sold = true;
        wallet.data.COin = wallet.data.COin + work.sellPrice;
        enemy.SetParent(null);
        enemy.gameObject.SetActive(false);
        Destroy(enemy.gameObject);

        display.order = 0;
        display.ShowCurrent();
        Refreshbuttons();
    }
}
