using UnityEngine;

public class WallDown : MonoBehaviour
{
    public CharacterCombatStats enemy;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (enemy.IsDead)
        {
            transform.position += Vector3.down * 1f * Time.deltaTime;
        }

        else
        {
            if (enemy.IsCaptured)
            {
                 transform.position += Vector3.down * 1f * Time.deltaTime;
            }
        }
    }
}
