using UnityEngine;
using System.Collections;

public class Quit : MonoBehaviour
{
    public GameObject black;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void IQuit()
    {
        StartCoroutine(PressQuit());
        
    }

    // Update is called once per frame
    public IEnumerator PressQuit()
    {
         black.SetActive(true);
         yield return new WaitForSeconds(1f);
        Application.Quit();
    }
}
