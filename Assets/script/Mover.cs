using UnityEngine;
using System.Collections;

public class Mover : MonoBehaviour
{
    public float speed;
    public float wait;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(wait);

        while(true)
        {
             transform.position += transform.forward * speed * Time.deltaTime;
             yield return null;
        }
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
}
