using UnityEngine;

public class CapturedEnemy : MonoBehaviour
{

public GameObject thistime;
public GameObject forever;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    public void Catch(CharacterCombatStats enemy)
    {
        if(enemy == null)
        {
            return;
        }

        if (enemy.IsCaptured)
        {
            enemy.transform.SetParent(thistime.transform);
            enemy.gameObject.SetActive(false);
        }
    }

    public void Safe()
    {
        while (thistime.transform.childCount > 0)
        {
            thistime.transform.GetChild(0).SetParent(forever.transform);
        }
    }

    public void Youdiedright()
    {
        foreach (Transform enemy in thistime.transform)
        {
            Destroy(enemy.gameObject);
        }
    }
}
