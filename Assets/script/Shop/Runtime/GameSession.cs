using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

// Money, upgrades, balls and scene travel. CapturedEnemy owns captured characters.
public class GameSession : MonoBehaviour
{
    public const string ShopScene = "PokemonShop", BattleScene = "CentralPark";
    public static GameSession Instance { get; private set; }
    public PlayerProgression Progression = new PlayerProgression();
    public int PokeBalls
    {
        get { return FindFirstObjectByType<ShopData>().ballscount; }
        private set { FindFirstObjectByType<ShopData>().ballscount = value; }
    }
    public const int BallPackSize = 20, BallPackPrice = 50;
    bool travelling;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() { Instance = null; }
    public static GameSession Ensure()
    {
        if (!Instance) Instance = new GameObject("GameSession").AddComponent<GameSession>();
        return Instance;
    }
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }
    // Income is added by EarnAndCost while the shop is open.
    public bool UsePokeBall() { if (PokeBalls <= 0) return false; PokeBalls--; return true; }
    public bool BuyPokeBalls()
    {
        EarnAndCost shop = FindFirstObjectByType<EarnAndCost>();
        if (shop == null)
        {
            return false;
        }
        int before = PokeBalls;
        shop.BuyBall();
        return PokeBalls > before;
    }
    public int Sell(CharacterCombatStats enemy)
    {
        var captures = FindFirstObjectByType<CapturedEnemy>();
        if (!enemy || !captures || !captures.forever || enemy.transform.parent != captures.forever.transform) return 0;
        var work = enemy.GetComponent<PokemonCareWork>();
        if (!work || work.sold || !work.processed) return 0;
        HideBody parts = enemy.GetComponent<HideBody>();
        if (parts == null || parts.sold) return 0;
        int price = parts.sellPrice;
        work.sold = true;
        parts.sold = true;
        ShopData data = FindFirstObjectByType<ShopData>();
        data.COin = data.COin + price;
        return price;
    }
    public void Travel(bool shop) { TravelTo(shop ? ShopScene : BattleScene); }
    public static bool CanTravelTo(string sceneName)
    {
        return SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity") >= 0;
    }
    public void TravelTo(string sceneName)
    {
        if (travelling || !CanTravelTo(sceneName)) return;
        StartCoroutine(ChangeScene(sceneName));
    }
    IEnumerator ChangeScene(string sceneName)
    {
        travelling = true;
        var captures = FindFirstObjectByType<CapturedEnemy>();
        bool returning = sceneName == ShopScene;
        Time.timeScale = 1f;
        while (FindObjectsByType<PokeBallCapture>(FindObjectsSortMode.None).Any(c => c.IsBusy))
        {
            yield return null;
        }
        if (captures)
        {
            if (returning) captures.Safe();
            if (captures.forever)
                foreach (Transform enemy in captures.forever.transform) enemy.gameObject.SetActive(false);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (SceneManager.GetActiveScene().name != sceneName)
            yield return SceneManager.LoadSceneAsync(sceneName);
        travelling = false;
    }
}
