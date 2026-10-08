using UnityEngine;
using Unity.Cinemachine;

// One fixed player plus the enemy roster used by combat and targeting.
[DefaultExecutionOrder(-100), DisallowMultipleComponent]
public sealed class PlayerRoster : MonoBehaviour
{
    public CharacterMotor[] characters;
    public CinemachineCamera followCamera;
    public Camera outputCamera;
    [Min(0)] public int initialCharacter;
    public int ActiveIndex => initialCharacter;
    public CharacterMotor ActiveCharacter => characters != null && initialCharacter >= 0 && initialCharacter < characters.Length ? characters[initialCharacter] : null;

    void Start() { InitializePlayer(); }

    public void InitializePlayer()
    {
        var player = ActiveCharacter;
        if (!player) return;
        foreach (var actor in characters)
        {
            if (!actor) continue;
            actor.SetPlayerInput(actor == player);
            actor.movementCamera = outputCamera;
        }
        foreach (var actor in characters)
            if (actor) actor.GetComponent<PartyEnemyAI>()?.RefreshControl();
        if (followCamera)
        {
            followCamera.Follow = player.transform;
            var focus = player.transform.Find("CameraFocus");
            followCamera.LookAt = focus ? focus : player.transform;
        }
    }

    public void RegisterEnemy(CharacterMotor actor)
    {
        if (!actor || System.Array.IndexOf(characters ?? System.Array.Empty<CharacterMotor>(), actor) >= 0) return;
        var list = new System.Collections.Generic.List<CharacterMotor>(characters ?? System.Array.Empty<CharacterMotor>());
        list.Add(actor);
        characters = list.ToArray();
        actor.movementCamera = outputCamera;
        actor.SetPlayerInput(false);
        GetComponent<PartyMeleeHitTest>()?.RefreshRoster();
    }
}
