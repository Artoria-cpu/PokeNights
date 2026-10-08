using UnityEngine;
using System.Collections;

public class CloseMyself : MonoBehaviour
{
    public GameObject me;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(close());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator close()
    {
        yield return new WaitForSeconds(1);
        me.SetActive(false);
    }
}
