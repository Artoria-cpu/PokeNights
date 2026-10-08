using UnityEngine;
using System.Collections;

public class CameraAdjustor : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject Cameras;
    public float startime;
    public float endtime;

    IEnumerator CameraStart()
    {
        yield return new WaitForSeconds(startime);
        Cameras.SetActive(true);
        yield return new WaitForSeconds(endtime);
        Cameras.SetActive(false);
    }
    void Start()
    {
        StartCoroutine(CameraStart());
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
