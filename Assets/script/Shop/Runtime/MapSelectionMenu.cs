using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MapSelectionMenu : MonoBehaviour
{
    public ShopExploration exploration;
    public GameObject panel;
    public UnityEngine.UI.Button[] destinationButtons;
    public string[] scenes = { "CentralPark", "Factory", "City" };
    public bool IsOpen => panel != null && panel.activeSelf;

    public void Open()
    {
        if (!exploration.CanEnter) return;
        for (int i = 0; i < destinationButtons.Length; i++)
            destinationButtons[i].interactable = i < scenes.Length && GameSession.CanTravelTo(scenes[i]);
        panel.SetActive(true);
    }
    public void Close() => panel.SetActive(false);
    public void Choose(int index)
    {
        if (!IsOpen || index < 0 || index >= scenes.Length || !GameSession.CanTravelTo(scenes[index])) return;
        GameSession.Ensure().TravelTo(scenes[index]);
        foreach (var button in destinationButtons) button.interactable = false;
    }
    void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }
}
