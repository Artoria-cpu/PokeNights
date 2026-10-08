using UnityEngine;

public class SoundSystem : MonoBehaviour
{
    public AudioSource sword1;
    public AudioSource sword2;
    public AudioSource punch1;
    public AudioSource punch2;
    public AudioSource punch3;
    public AudioSource enemyHurt1;
    public AudioSource enemyHurt2;
    public AudioSource woodStep1;
    public AudioSource woodStep2;
    public AudioSource woodStep3;
    public AudioSource woodStep4;
    public AudioSource roadStep1;
    public AudioSource roadStep2;
    public AudioSource roadStep3;
    public AudioSource roadStep4;
    public AudioSource roadStep5;
    public AudioSource parry1;
    public AudioSource parry2;
    public AudioSource parry3;
    public AudioSource air;
    public AudioSource magicShot;
    public AudioSource magicBurst;
    public AudioSource magicCharge;
    public AudioSource magicBeam;
    public AudioSource playerHurt;
    public AudioSource landing;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlaySword()
    {
        int number = Random.Range(0, 2);

        if (number == 0)
        {
            sword1.Play();
        }
        else
        {
            sword2.Play();
        }
    }

     public void PlaySwordHeavy()
    {
        sword2.Play();
    }

     public void PlayPunch()
    {
        int number = Random.Range(0, 3);

        if (number == 0)
        {
            punch1.Play();
        }
        else if (number == 1)
        {
            punch2.Play();
        }
        else
        {
            punch3.Play();
        }
    }

     public void PlayEnemyHurt()
    {
         int number = Random.Range(0, 2);

        if (number == 0)
        {
            enemyHurt1.Play();
        }
        else
        {
            enemyHurt2.Play();
        }
    }

     public void Playwood()
    {
         int number = Random.Range(0, 4);

        if (number == 0)
        {
            woodStep1.Play();
        }
        else if (number == 1)
        {
            woodStep2.Play();
        }
        else if (number == 2)
        {
            woodStep3.Play();
        }
        else
        {
            woodStep4.Play();
        }
    }

     public void Playroad()
    {
         int number = Random.Range(0, 5);

        if (number == 0)
        {
            roadStep1.Play();
        }
        else if (number == 1)
        {
            roadStep2.Play();
        }
        else if (number == 2)
        {
            roadStep3.Play();
        }
        else if (number == 3)
        {
            roadStep4.Play();
        }
        else
        {
            roadStep5.Play();
        }
    }

     public void PlayParry()
    {
        int number = Random.Range(0, 3);

        if (number == 0)
        {
            parry1.Play();
        }
        else if (number == 1)
        {
            parry2.Play();
        }
        else
        {
            parry3.Play();
        }
    }

     public void Playair()
    {
        air.Play();
    
    }

     public void PlayMagicShoot()
    {
        magicShot.Play();
    }

     public void PlayMagicBurst()
    {
        magicBurst.Play();
    }

     public void PlayMagicCharge()
    {
        magicBeam.Stop();
        magicCharge.Play();
    }

     public void PlayMagicBeam()
    {
        magicCharge.Stop();
        magicBeam.Play();
    }

     public void PlayMagicStop()
    {
        magicCharge.Stop();
        magicBeam.Stop();
    }

     public void PlayPlayerhurt()
    {
        playerHurt.Play();
    }
    

     public void PlayLand()
    {
        landing.Play();
    }
}
