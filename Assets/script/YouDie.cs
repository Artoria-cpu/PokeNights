using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using System;

public class YouDie : MonoBehaviour
{
    public CharacterCombatStats player;
    public CapturedEnemy Gotcha;

    public GameObject dietext;

    public GameObject black;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   IEnumerator WWait()
    {
        Gotcha.Youdiedright();
        yield return new WaitForSeconds(1f);
        dietext.SetActive(true);
        black.SetActive(true);
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("PokemonShop");
    }

    // Update is called once per frame
    void Update()
    {
        if(player.IsDead)
        {
            StartCoroutine(WWait());
        }
    }

    void Start()
    {
        Gotcha = FindFirstObjectByType<CapturedEnemy>();
    }
}
