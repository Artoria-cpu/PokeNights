using UnityEngine;
using UnityEngine.UI;

public class TutorialText : MonoBehaviour
{
    public Collider player;
    public GameObject texts;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnTriggerEnter(Collider other)
    {
        if(other == player)texts.SetActive(true);
    }

    // Update is called once per frame
    void OnTriggerExit(Collider other)
    {
        if(other == player)texts.SetActive(false);
    }
}
