using UnityEngine;
public sealed class ShopTravelButton : MonoBehaviour
{
    public bool toShop=true;
    public void Travel(){GameSession.Ensure().Travel(toShop);}
}
