using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Select a capture, complete three key prompts, then sell it.
public sealed class PokemonShop : MonoBehaviour
{
    public Transform displayPoint;
    public ParticleSystem processingEffect;
    ParticleSystem activeProcessingEffect;
    public Camera shopCamera;
    public TMP_Text information, workStatus, saleLabel, walletLabel;
    public UnityEngine.UI.Button confirmButton, sellButton;
    public UnityEngine.UI.Button[] browseButtons;
    public bool requireWorkstation;
    public int displayLayer = -1;
    public bool IsOpen { get; private set; }
    public CharacterCombatStats SelectedEnemy => shown;

    readonly Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    readonly Key[] keys = { Key.W, Key.A, Key.S, Key.D };
    CharacterCombatStats shown;
    PokemonCareWork work;
    int index, hits;
    bool processing;
    Key expected;
    float deadline;

    void Start() { if (!requireWorkstation) OpenWorkshop(); }
    public void OpenWorkshop() { IsOpen = true; Show(); }
    public void CloseWorkshop() { IsOpen = false; processing = false; HideShown(); }
    public void Next() { if (IsOpen && !processing) { index++; Show(); } }
    public void Previous() { if (IsOpen && !processing) { index--; Show(); } }

    void HideShown()
    {
        if (activeProcessingEffect)
        {
            if (Application.isPlaying) Destroy(activeProcessingEffect.gameObject);
            else DestroyImmediate(activeProcessingEffect.gameObject);
            activeProcessingEffect = null;
        }
        foreach (var pair in originalLayers)
            if (pair.Key) pair.Key.gameObject.layer = pair.Value;
        originalLayers.Clear();
        if (shown) shown.gameObject.SetActive(false);
    }

    void Show()
    {
        HideShown();
        shown = null;
        work = null;
        processing = false;
        var enemies = GetStored();
        enemies.RemoveAll(enemy => enemy == null);
        if (enemies.Count > 0)
        {
            index = (index % enemies.Count + enemies.Count) % enemies.Count;
            shown = enemies[index];
            shown.transform.SetPositionAndRotation(displayPoint.position, displayPoint.rotation);
            shown.gameObject.SetActive(true);
            if (displayLayer >= 0)
                foreach (var child in shown.GetComponentsInChildren<Transform>(true))
                {
                    originalLayers[child] = child.gameObject.layer;
                    child.gameObject.layer = displayLayer;
                }
            work = shown.GetComponent<PokemonCareWork>();
            if (!work) work = shown.gameObject.AddComponent<PokemonCareWork>();
            work.Initialize();
        }
        workStatus.text = !shown ? "Capture an enemy first." : work.processed ? "Ready to sell." : "Click PROCESS, then press the shown keys.";
        Refresh();
    }

    public void BeginProcessing()
    {
        if (!IsOpen || !shown || processing || work.processed) return;
        processing = true;
        hits = 0;
        NextKey();
        Refresh();
    }

    void NextKey()
    {
        expected = keys[Random.Range(0, keys.Length)];
        deadline = Time.unscaledTime + 1.6f;
    }

    void Update()
    {
        if (!IsOpen || !processing) return;
        if (Time.unscaledTime > deadline) { hits = 0; NextKey(); }
        if (Keyboard.current != null)
            foreach (var key in keys)
                if (Keyboard.current[key].wasPressedThisFrame) { CheckKey(key); break; }
        if (processing)
            workStatus.text = $"Press [ {expected} ]\n{hits} / 3     {Mathf.Max(0, deadline - Time.unscaledTime):0.0}s";
    }

    public void CheckKey(Key key)
    {
        if (!IsOpen || !processing) return;
        hits = key == expected && Time.unscaledTime <= deadline ? hits + 1 : 0;
        if (hits < 3) { NextKey(); return; }
        work.processed = true;
        shown.GetComponent<CharacterBlood>()?.CleanUnder(shown.transform);
        work.HideBody();
        PlayProcessingEffect();
        processing = false;
        workStatus.text = "Complete! Ready to sell.";
        Refresh();
    }

    void PlayProcessingEffect()
    {
        if (!processingEffect || !shown) return;
        activeProcessingEffect = Instantiate(processingEffect);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(activeProcessingEffect.gameObject, gameObject.scene);
        foreach (var child in activeProcessingEffect.GetComponentsInChildren<Transform>(true))
            if (displayLayer >= 0) child.gameObject.layer = displayLayer;
        var main = activeProcessingEffect.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
        activeProcessingEffect.gameObject.SetActive(true);
        activeProcessingEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        activeProcessingEffect.transform.SetPositionAndRotation(shown.transform.position, processingEffect.transform.rotation);
        activeProcessingEffect.Play(true);
    }

    public void SellSelected()
    {
        if (!IsOpen || processing || !shown) return;
        int coins = GameSession.Ensure().Sell(shown);
        if (coins <= 0) return;
        var sold = shown.gameObject;
        HideShown();
        shown = null;
        if (Application.isPlaying) Destroy(sold); else DestroyImmediate(sold);
        Show();
        workStatus.text = $"Sold for {coins} coins.";
    }

    void Refresh()
    {
        var collection = GameSession.Ensure();
        information.text = shown ? $"CAPTURES {index + 1} / {GetStored().Count}\n\n{shown.displayName}  #{shown.pokemonId}\n\n{work.partCount} costume parts" : "No captures yet.";
        saleLabel.text = shown ? $"Sale value: {shown.GetComponent<HideBody>().sellPrice} coins" : "Select a capture with the arrow buttons.";
        walletLabel.text = $"Coins {FindFirstObjectByType<ShopData>().COin}";
        confirmButton.interactable = shown && !processing && !work.processed;
        sellButton.interactable = shown && !processing && work.processed;
        foreach (var button in browseButtons) button.interactable = !processing && GetStored().Count > 1;
    }
    static List<CharacterCombatStats> GetStored()
    {
        var enemies = new List<CharacterCombatStats>();
        var collection = FindFirstObjectByType<CapturedEnemy>();
        if (!collection || !collection.forever) return enemies;
        foreach (Transform child in collection.forever.transform)
        {
            var enemy = child.GetComponent<CharacterCombatStats>();
            var work = child.GetComponent<PokemonCareWork>();
            if (enemy && enemy.IsCaptured && (!work || !work.sold)) enemies.Add(enemy);
        }
        return enemies;
    }
    void OnDestroy() { HideShown(); }
}



