using System.Collections.Generic;
using UnityEngine;

// Each visit rolls a new population from this map's templates. Cleared enemies stay cleared until the next visit.
[DefaultExecutionOrder(-900)]
public sealed class MapEnemySpawner : MonoBehaviour
{
    public CentralParkEncounter placement;
    public CharacterMotor[] enemyPool;
    [Min(1)] public int minimumEnemies = 12;
    [Min(1)] public int maximumEnemies = 20;
    public int RequestedCount { get; private set; }
    bool spawned;

    void Awake() => SpawnEnemies();
    public void SpawnEnemies()
    {
        if (spawned || placement == null || placement.selection == null || enemyPool == null || enemyPool.Length == 0) return;
        spawned = true;
        int minimum = Mathf.Max(1, minimumEnemies);
        RequestedCount = Random.Range(minimum, Mathf.Max(minimum, maximumEnemies) + 1);
        var points = new List<Vector3>(placement.enemySpawnPoints ?? System.Array.Empty<Vector3>());
        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var point = points[i]; points[i] = points[j]; points[j] = point;
        }
        var used = new List<Vector3>();
        var selection = placement.selection;
        foreach (var point in points)
        {
            if (used.Count >= RequestedCount) break;
            if ((point - placement.entrance.position).sqrMagnitude < 15 * 15 ||
                used.Exists(p => (p - point).sqrMagnitude < placement.enemySpacing * placement.enemySpacing)) continue;
            var template = enemyPool[Random.Range(0, enemyPool.Length)];
            if (template == null) continue;
            // Templates are inactive so references and position are ready before AI Awake records its home.
            var actor = Instantiate(template, point, Quaternion.Euler(0, Random.Range(0f, 360f), 0), transform);
            actor.name = template.name + " (Encounter)";
            actor.GetComponent<PartyEnemyAI>().selection = selection;
            actor.GetComponent<CharacterComboAttack>().equipment = selection.GetComponent<CharacterWeaponEquipment>();
            selection.RegisterEnemy(actor);
            actor.gameObject.SetActive(true);
            used.Add(point);
        }
        if (used.Count < RequestedCount) Debug.LogWarning($"{gameObject.scene.name}: spawned {used.Count}/{RequestedCount}; add more separated spawn points.", this);
    }
}
