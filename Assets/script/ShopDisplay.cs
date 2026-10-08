using UnityEngine;

public class ShopDisplay : MonoBehaviour
{
    public CapturedEnemy captured;
    public GameObject Showpoint;
    public int order;
    public ProcessEnemy qte;

    void Start()
    {
        captured = FindFirstObjectByType<CapturedEnemy>();
        ShowCurrent();
    }

    public Transform CurrentEnemy()
    {
        if (captured == null || captured.forever == null)
        {
            return null;
        }

        Transform folder = captured.forever.transform;
        
        if (folder.childCount == 0)
        
        {
            return null;
        }
        
        order = Mathf.Clamp(order, 0, folder.childCount - 1);
        return folder.GetChild(order);
    }

    public void ShowCurrent()
    {
        Transform enemy = CurrentEnemy();
        
        if (enemy == null)
        {
            return;
        }

        foreach (Transform child in captured.forever.transform)
            child.gameObject.SetActive(child == enemy);
        enemy.position = Showpoint.transform.position;
        enemy.rotation = Showpoint.transform.rotation;
    }

    public void PressNext()
    {
        
        if (CurrentEnemy() == null)
        {
            return;
        }

        order = order + 1;

        if (order >= captured.forever.transform.childCount)
        {
            order = 0;
        }

        ShowCurrent();
    }

    public void PressPrivious()
    {
        
        if (CurrentEnemy() == null)
        {
            return;
        }

        order = order - 1;

        if (order < 0)
        {
            order = captured.forever.transform.childCount - 1;
        }

        ShowCurrent();
    }
}
