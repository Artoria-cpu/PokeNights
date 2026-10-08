using UnityEngine;
using System.Collections;

public class WaitToPlaySound : MonoBehaviour
{
    public AudioSource sound1;
    public AudioSource sound2;

    public AudioClip clip1;
    public AudioClip clip2;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(PlaySound());
    }

    IEnumerator PlaySound()
    {
        yield return new WaitForSeconds(5.5f);
        sound1.clip = clip1;
        sound2.clip = clip2;
        sound1.Play();
        sound2.Play();


    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
