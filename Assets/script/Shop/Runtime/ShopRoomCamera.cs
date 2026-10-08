using UnityEngine;

// Keep the scene picture aligned with the Canvas's Fit In Parent layout.
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class ShopRoomCamera : MonoBehaviour
{
    public SpriteRenderer background;
    Camera view;
    void OnEnable(){view=GetComponent<Camera>();}
    void LateUpdate()
    {
        if(!background || !view)return;
        Vector3 size=background.bounds.size;
        view.orthographicSize=Mathf.Max(size.y*.5f,size.x*.5f/Mathf.Max(.01f,view.aspect));
    }
}
