using UnityEngine;

public class HideBody : MonoBehaviour
{
    public Renderer[] body;
    public int bodycount = 1;
    public int sellPrice = 100;
    public bool process;
    public bool sold;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Hide()
    {
        foreach(Renderer part in body)
        {
            part.enabled = false;
        }
        process = true;
    }
}
