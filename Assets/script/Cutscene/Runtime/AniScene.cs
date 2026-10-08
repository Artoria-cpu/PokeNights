using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AniSceneChange : MonoBehaviour
{
    public GameObject black;
    IEnumerator Start()
    {
        yield return new WaitForSeconds(15.5f);
        black.SetActive(true);
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("Tutorial");
    }

}

