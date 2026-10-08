using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using TMPro;
public class LeavePoint : MonoBehaviour
{
    public Collider player;
    public bool incircle;
    public float waitime;

    public CapturedEnemy Gotcha;

    public TMP_Text textt;

    public GameObject balck;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnTriggerEnter(Collider other)
    {
        if(other == player)
        {
            incircle = true;
        }
    }

     void OnTriggerExit(Collider other)
    {
        if(other == player)
        {
            incircle = false;
            waitime = 0;
            textt.gameObject.SetActive(false);
        }
    }


    // Update is called once per frame
    void Update()
    {
        if(incircle == true)
        {
            textt.gameObject.SetActive(true);
            waitime += Time.deltaTime;
            float need = 5 - waitime;
            textt.text = "Travel To Shop In " + need;
        }

        if(waitime >= 5)
        {
            Time.timeScale = 1f;
            StartCoroutine(Doagain());

        }
    }

    IEnumerator Doagain()
    {
        balck.SetActive(true);
        yield return new WaitForSeconds(1f);
        Gotcha.Safe();
        SceneManager.LoadScene("PokemonShop");
    }

    void Start()
    {
        Gotcha = FindFirstObjectByType<CapturedEnemy>();
    }
}
