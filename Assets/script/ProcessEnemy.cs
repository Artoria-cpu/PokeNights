using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Threading;

public class ProcessEnemy : MonoBehaviour
{
    public TMP_Text hint;
    public bool completed;
    public bool running;
    public int keynumber = 0;
    public GameObject startbutton;
    public int rightNumber;
    public TMP_Text right;

    public GameObject Processbutton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        right.text = rightNumber + "/3";
    }
    IEnumerator Killing()
    {
        keynumber = Random.Range(0,4);
        if(keynumber == 0)
        {
            hint.text = "Press W";
            float timer = 0;
            

            while(timer <= 1f)
            {
            
                if(Keyboard.current.wKey.wasPressedThisFrame)
                {
                    rightNumber = rightNumber + 1;
                    hint.text = "correct";
                   
                    

                    if(rightNumber >= 3)
                    {
                        completed = true;
                        hint.text = "all compelete";
                        running = false;
                        yield break;
                    }

                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(Killing());
                    yield break;
                }
                else if (Keyboard.current.anyKey.wasPressedThisFrame)
                {
                    rightNumber = 0;
                    hint.text = "wrong";
                    yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;

                }
                    timer = timer + Time.deltaTime;
                    yield return null;
            }

                    rightNumber = 0;
                    hint.text = "overtime";
                     yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;
            
        }
        if(keynumber == 1)
        {
            hint.text = "Press A";
            float timer = 0;
            

            while(timer <= 1f)
            {
            
                if(Keyboard.current.aKey.wasPressedThisFrame)
                {
                    rightNumber = rightNumber + 1;
                    hint.text = "correct";
                   
                    

                    if(rightNumber >= 3)
                    {
                        completed = true;
                        hint.text = "all compelete";
                        running = false;
                        yield break;
                    }

                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(Killing());
                    yield break;
                }
                else if (Keyboard.current.anyKey.wasPressedThisFrame)
                {
                    rightNumber = 0;
                    hint.text = "wrong";
                    yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;

                }
                    timer = timer + Time.deltaTime;
                    yield return null;
            }

                    rightNumber = 0;
                    hint.text = "overtime";
                     yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;
        }
        if(keynumber == 2)
        {
            hint.text = "Press S";
            float timer = 0;
            

            while(timer <= 1f)
            {
            
                if(Keyboard.current.sKey.wasPressedThisFrame)
                {
                    rightNumber = rightNumber + 1;
                    hint.text = "correct";
                   
                    

                    if(rightNumber >= 3)
                    {
                        completed = true;
                        hint.text = "all compelete";
                        running = false;
                        yield break;
                    }

                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(Killing());
                    yield break;
                }
                else if (Keyboard.current.anyKey.wasPressedThisFrame)
                {
                    rightNumber = 0;
                    hint.text = "wrong";
                    yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;

                }
                    timer = timer + Time.deltaTime;
                    yield return null;
            }

                    rightNumber = 0;
                    hint.text = "overtime";
                     yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;
        }
        if(keynumber == 3)
        {
            hint.text = "Press D";
            float timer = 0;
            

            while(timer <= 1f)
            {
            
                if(Keyboard.current.dKey.wasPressedThisFrame)
                {
                    rightNumber = rightNumber + 1;
                    hint.text = "correct";
                   
                    

                    if(rightNumber >= 3)
                    {
                        completed = true;
                        hint.text = "all compelete";
                        running = false;
                        yield break;
                    }

                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(Killing());
                    yield break;
                }
                else if (Keyboard.current.anyKey.wasPressedThisFrame)
                {
                    rightNumber = 0;
                    hint.text = "wrong";
                    yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;

                }
                    timer = timer + Time.deltaTime;
                    yield return null;
            }

                    rightNumber = 0;
                    hint.text = "overtime";
                     yield return new WaitForSeconds(0.5f);
                    running = false;
                    yield break;
        }
    }
    void OnDisable()
    {
        StopAllCoroutines();
        running = false;
        completed = false;
    }

    public void KillingStart()
    {
       if (running) return;
       running = true;
       StopAllCoroutines();
       rightNumber = 0;
       completed = false;
       StartCoroutine(Killing());
       Processbutton.SetActive(false);

    }

}

