using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class ShopCashier : MonoBehaviour
{
    public ShopExploration exploration;
    public Canvas page;
    public GameObject attributesPanel, upgradesPanel, shopPanel;
    public TMP_Text walletLabel, shopSummary, ballStockLabel;
    public TMP_Text weaponSummary, feedback, upgradeSummary;
    public TMP_Text[] upgradeLabels, typeLabels;
    public Button shopUpgradeButton, buyBallsButton;
    public Button[] upgradeButtons, weaponButtons, typeButtons;
    public TypeMatchupPanel typeMatchups;
    public bool IsOpen => page != null && page.enabled;
    int weaponSlot = 1;
    long displayedCoins = -1;

    void Update()
    {
        // Passive income can make an upgrade affordable while the page is open.
        if (IsOpen && FindFirstObjectByType<ShopData>().COin != displayedCoins) Refresh();
    }

    public void Open()
    {
        if (!exploration.CanEnterCashier) return;
        exploration.explorationCanvas.enabled = false;
        page.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        attributesPanel.SetActive(true);
        upgradesPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        feedback.text = "Choose a weapon type or buy an upgrade.";
        Refresh();
    }

    public void Close()
    {
        page.enabled = false;
        exploration.ShowOverview();
    }

    public void SelectWeapon(int slot)
    {
        if (slot < 0 || slot > 2) return;
        weaponSlot = slot;
        Refresh();
    }

    public void ChooseType(int index)
    {
        if (!IsOpen || index < 0 || index >= PokemonTypeChart.Types.Length) return;
        GameSession.Ensure().Progression.SetWeaponType(weaponSlot, PokemonTypeChart.Types[index]);
        feedback.text = "Weapon type changed.";
        Refresh();
    }

    public void Upgrade(int stat)
    {
        if (!IsOpen) return;
        feedback.text = GameSession.Ensure().Progression.Upgrade(stat)
            ? "Party upgraded." : "Not enough coins, or maximum level reached.";
        Refresh();
    }

    public void UpgradeShop()
    {
        if (!IsOpen) return;
        EarnAndCost shop = FindFirstObjectByType<EarnAndCost>();
        if (shop == null) return;
        int before = shop.data.Shoplevel;
        shop.UpgradeShop();
        feedback.text = shop.data.Shoplevel > before
            ? "Shop upgraded." : "Not enough coins, or maximum level reached.";
        Refresh();
    }

    public void BuyBalls()
    {
        if (!IsOpen) return;
        feedback.text = GameSession.Ensure().BuyPokeBalls() ? $"Bought {GameSession.BallPackSize} Poke Balls." : "Not enough coins.";
        Refresh();
    }

    public void Refresh()
    {
        var collection = GameSession.Ensure();
        var progress = collection.Progression;
        var economy = FindFirstObjectByType<ShopData>();
        displayedCoins = economy.COin;
        if (walletLabel != null) walletLabel.text = $"Coins {economy.COin}   +{economy.incomepers}/sec";
        if (ballStockLabel != null) ballStockLabel.text = $"POKE BALLS {collection.PokeBalls}";
        if (buyBallsButton != null)
        {
            buyBallsButton.interactable = economy.COin >= GameSession.BallPackPrice;
            buyBallsButton.GetComponentInChildren<TMP_Text>(true).text = $"Buy {GameSession.BallPackSize} balls · {GameSession.BallPackPrice} Coins";
        }
        if (shopSummary != null) shopSummary.text = $"SHOP LEVEL {economy.Shoplevel}/{5}\nIncome +{economy.incomepers}/sec";
        if (shopUpgradeButton != null)
        {
            shopUpgradeButton.interactable = economy.Shoplevel < 5 && economy.COin >= economy.ShopPrice;
            shopUpgradeButton.GetComponentInChildren<TMP_Text>(true).text = economy.Shoplevel >= 5 ? "MAX LEVEL" : $"Upgrade - {economy.ShopPrice} coins";
        }
        weaponSummary.text = $"Fists: {progress.fistType}\nSword: {progress.swordType}\nOrb: {progress.orbType}\n\nEditing: {new[] { "Fists", "Sword", "Orb" }[weaponSlot]}";
        if (typeMatchups != null) typeMatchups.Show(progress.WeaponType(weaponSlot));
        for (int i = 0; i < typeLabels.Length; i++)
            typeLabels[i].text = (progress.WeaponType(weaponSlot) == PokemonTypeChart.Types[i] ? "> " : "") + PokemonTypeChart.Labels[i];
        upgradeSummary.text = "Upgrades apply to the whole party.";
        string[] names = { "HEALTH +20", "ATK / SP. ATK +5", "DEF / SP. DEF +5" };
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int level = progress.Level(i);
            int cost = new[] { economy.Pricehealth, economy.priceatk, economy.pricedef }[i];
            upgradeLabels[i].text = $"{names[i]}\nLevel {level}/{PlayerProgression.MaximumLevel}";
            upgradeButtons[i].interactable = level < PlayerProgression.MaximumLevel && economy.COin >= cost;
            upgradeButtons[i].GetComponentInChildren<TMP_Text>(true).text = level >= PlayerProgression.MaximumLevel ? "MAX LEVEL" : $"Upgrade - {cost} coins";
        }
    }
}
