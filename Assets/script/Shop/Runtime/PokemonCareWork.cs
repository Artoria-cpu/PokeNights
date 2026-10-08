using System.Collections.Generic;
using UnityEngine;

// Only the sale data is needed; costume pieces stay on the model.
public sealed class PokemonCareWork : MonoBehaviour
{
    public int partCount;
    public bool processed, sold;

    public void HideBody()
    {
        // Keep the skeleton active so attached Pokemon parts retain their pose.
        var renderers = GetComponentsInChildren<Renderer>(true);
        var partRoots = new HashSet<Transform>();
        foreach (var renderer in renderers)
            if (IsPartName(renderer.name)) partRoots.Add(renderer.transform.parent);
        foreach (var renderer in renderers)
        {
            bool pokemonPart = false;
            foreach (var root in partRoots)
                if (root != null && renderer.transform.IsChildOf(root)) { pokemonPart = true; break; }
            if (!pokemonPart) renderer.enabled = false;
        }
    }

    static bool IsPartName(string name) => name.EndsWith("_Head") || name.EndsWith("_Tail")
        || name.EndsWith("_Bud") || name.EndsWith("_Wing");

    public void Initialize()
    {
        if (partCount > 0) return;
        var parts = new HashSet<Transform>();
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            string name = renderer.name;
            if (IsPartName(name))
                parts.Add(renderer.transform.parent);
        }
        partCount = Mathf.Max(1, parts.Count);
    }
}
