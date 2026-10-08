using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public sealed class ShopExploration : MonoBehaviour
{
    public PokemonShop workshop;
    public ShopCashier cashier;
    public GameObject partyRoot, walkCameraRoot, workshopStage, displayCharacter;
    public Canvas workshopCanvas, explorationCanvas;
    public TMP_Text wallet;

    public bool CanEnter => !workshop.IsOpen && (cashier == null || !cashier.IsOpen);
    public bool CanEnterCashier => cashier != null && CanEnter;

    void Start()
    {
        workshop.CloseWorkshop();
        ShowOverview();
    }

    void Update()
    {
        if (wallet != null)
        {
            var economy = FindFirstObjectByType<ShopData>();
            wallet.text = $"Coins {economy.COin}  ·  Shop Lv.{economy.Shoplevel}  ·  +{economy.incomepers}/sec";
        }
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (cashier != null && cashier.IsOpen) cashier.Close();
        else if (workshop.IsOpen) ExitWorkshop();
    }

    public void EnterWorkshop()
    {
        if (!CanEnter) return;
        SetPresentation(true);
        workshop.OpenWorkshop();
    }

    public void ExitWorkshop()
    {
        workshop.CloseWorkshop();
        ShowOverview();
    }

    public void ShowOverview() => SetPresentation(false);

    void SetPresentation(bool working)
    {
        partyRoot.SetActive(!working && displayCharacter != null);
        walkCameraRoot.SetActive(!working);
        workshop.shopCamera.gameObject.SetActive(working);
        if (workshopStage != null) workshopStage.SetActive(working);
        workshopCanvas.enabled = working;
        explorationCanvas.enabled = !working;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
