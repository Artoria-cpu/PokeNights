using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartGame : MonoBehaviour
{
    public GameObject black;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void IWantToPlay()
    {
        StartCoroutine(PressStart());
    }

    // Update is called once per frame
    public IEnumerator PressStart()
    {
         black.SetActive(true);
         yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("ShopRaidCutscene");
    }
}
