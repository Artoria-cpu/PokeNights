using UnityEngine;
public sealed class CombatHitFeedback : MonoBehaviour
{
 public Material glowMaterial,spikeMaterial;
 public GameObject particlePrefab;
 public float hitStopSeconds=.1f;
 public int HitStops {get;private set;}
 static CombatHitFeedback instance;float until,previousScale;bool paused;
 public static bool IsPaused=>instance!=null&&instance.paused;
 void OnEnable(){instance=this;}
 public static void Play(Vector3 position,Camera camera,bool sword=false,Vector3 direction=default,Collider target=null,bool finisher=false,bool hitStop=true,float hitStopOverride=-1f,bool unscaledParticles=false)
 {
  if(instance==null)return;
  var victim=target!=null?target.GetComponentInParent<CharacterMotor>():null;
  if(victim!=null&&victim.IsDodging)return;
  if(hitStop)instance.Pause(hitStopOverride>=0?hitStopOverride:instance.hitStopSeconds);
  if(camera==null)camera=Camera.main;
  if(hitStop&&camera!=null){var shake=camera.GetComponent<CombatCameraShake>();if(shake==null)shake=camera.gameObject.AddComponent<CombatCameraShake>();shake.Trigger(sword,finisher);}
  if(instance.particlePrefab==null)return;
  var go=Instantiate(instance.particlePrefab,position,Quaternion.identity);
  // A long skill contact freeze still needs a visible impact while gameplay is paused.
  if(unscaledParticles)foreach(var particle in go.GetComponentsInChildren<ParticleSystem>(true)){var main=particle.main;main.useUnscaledTime=true;}
  go.GetComponent<LimbImpactParticles>().Initialize(camera,sword,direction,target);
 }

 void Pause(float seconds){if(seconds<=0)return;if(!paused){previousScale=Time.timeScale;paused=true;}Time.timeScale=0;until=Mathf.Max(until,Time.unscaledTime+seconds);HitStops++;}
 void Update(){if(paused&&Time.unscaledTime>=until)Restore();}
 void Restore(){if(!paused)return;Time.timeScale=previousScale;paused=false;}
 void OnDisable(){Restore();if(instance==this)instance=null;}
}

