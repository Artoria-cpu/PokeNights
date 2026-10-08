using UnityEngine;

// Shared entrance and baked spawn positions. Run before the map spawner and AI.
[DefaultExecutionOrder(-1000)]
public sealed class CentralParkEncounter : MonoBehaviour
{
    public PlayerRoster selection;
    public Transform entrance;
    public Vector3[] enemySpawnPoints;
    public float enemySpacing=7f;
    public void Awake(){PlaceActors();}
    public void PlaceActors()
    {
        if(selection==null||entrance==null)return;
        int companion=0;
        foreach(var actor in selection.characters){if(actor==null||actor.GetComponent<CharacterCombatStats>().enemyOnly)continue;
            bool initial=actor==selection.characters[selection.initialCharacter];
            var offset=initial?Vector3.zero:new Vector3((companion++%2==0?-1.5f:1.5f),0,-.6f);
            Place(actor,entrance.position+entrance.rotation*offset,entrance.rotation);
        }
    }
    static void Place(CharacterMotor actor,Vector3 position,Quaternion rotation)
    {
        var controller=actor.GetComponent<CharacterController>();bool enabled=controller!=null&&controller.enabled;
        if(enabled)controller.enabled=false;actor.transform.SetPositionAndRotation(position,rotation);if(enabled)controller.enabled=true;
    }
}
