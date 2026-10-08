using UnityEngine;
public sealed class CombatDummy : MonoBehaviour
{
 public int HitCount {get;private set;}
 public float health=1000;
 public void RegisterHit(){HitCount++;health=Mathf.Max(0,health-10);GameplayAudio.Play(GameplayCue.PunchHit,transform.position,.4f);}
}
