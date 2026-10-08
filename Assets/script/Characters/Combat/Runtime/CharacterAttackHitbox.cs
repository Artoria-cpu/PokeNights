using System.Collections.Generic;
using UnityEngine;
[DisallowMultipleComponent, DefaultExecutionOrder(100), RequireComponent(typeof(CharacterComboAttack))]
public sealed class CharacterAttackHitbox : MonoBehaviour
{
 CharacterComboAttack combo;int sequence=-1,nextContact;
 PartyMeleeHitTest party;
 readonly Collider[] contacts=new Collider[24];readonly HashSet<int> hit=new HashSet<int>();
 public int HitCount {get;private set;}
 public float LastHitSourceFrame {get;private set;}
 public Vector3 LastHitWorldPosition {get;private set;}
 public Transform LastHitBone {get;private set;}
 public float hitRadius=.42f,hitForwardExtension=.55f;
 void Awake(){combo=GetComponent<CharacterComboAttack>();party=FindAnyObjectByType<PartyMeleeHitTest>();}
 public void ResolveSwordContact(Collider contact,Vector3 center,float sourceFrame,HashSet<int> slashHits,Vector3? sweptOrigin=null)
 {
  if(contact==null||contact.transform.IsChildOf(transform))return;
  // Use the direction locked at attack start, not the spinning animated body.
  var victim=contact.GetComponentInParent<CharacterMotor>();
  Vector3 targetPosition=victim!=null?victim.transform.position:contact.bounds.center;
  Vector3 origin=sweptOrigin??transform.position;
  // A touching capsule can straddle the attack plane even when its pivot is just behind it.
  var capsule=victim!=null?victim.GetComponent<CharacterController>():null;
  Vector3 extent=contact.bounds.extents,forward=combo.AttackForward;
  float frontExtent=capsule!=null?capsule.radius*Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x),Mathf.Abs(capsule.transform.lossyScale.z)):
      Mathf.Abs(forward.x)*extent.x+Mathf.Abs(forward.z)*extent.z;
  if(Vector3.Dot(Vector3.ProjectOnPlane(targetPosition-origin,Vector3.up),forward)<-frontExtent-.03f)return;
  if(party!=null&&party.ResolveSwordContact(combo.driver,contact,center,slashHits,origin))return;
  if(victim!=null)return; // Character contacts, including dodges, belong to the reaction path only.
  Transform enemy=contact.transform;while(enemy!=null&&!enemy.CompareTag("Enemy"))enemy=enemy.parent;
  if(enemy==null||enemy.IsChildOf(transform)||!slashHits.Add(enemy.GetInstanceID()))return;
  HitCount++;LastHitSourceFrame=sourceFrame;
  LastHitBone=combo.driver.animator.GetBoneTransform(combo.ActiveStrike.contactBone);
  var dummy=enemy.GetComponent<CombatDummy>();if(dummy!=null)dummy.RegisterHit();
  Vector3 approach=origin;approach.y=Mathf.Clamp(center.y,contact.bounds.min.y,contact.bounds.max.y);
  LastHitWorldPosition=contact.ClosestPoint(approach);
  CombatHitFeedback.Play(LastHitWorldPosition,combo.driver.movementCamera,true,combo.AttackForward,contact,combo.IsFifthStrike);
 }
 void LateUpdate(){if(Time.deltaTime<=0||!combo.driver.acceptPlayerInput||!combo.IsAttacking||combo.IsSwordAttack||combo.IsOrbAttack)return;
  if(sequence!=combo.AttackCount){sequence=combo.AttackCount;nextContact=0;hit.Clear();}
  if(nextContact>=combo.ActiveStrike.ContactCount)return;
  var animator=combo.driver.animator;var strike=combo.ActiveStrike;
  var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
  if(!state.IsName(strike.stateName)||state.normalizedTime<strike.ContactFrame(nextContact)/(strike.clip.length*strike.clip.frameRate))return;
  // Evaluate only the first sampled frame crossing the authored contact marker, never the windup/recovery.
  nextContact++;hit.Clear();var bone=animator.GetBoneTransform(strike.contactBone);if(bone==null)return;
  Vector3 contactPosition=bone.position;
  float extension=combo.IsRunningAttack?Mathf.Max(hitForwardExtension,.65f):hitForwardExtension;
  int count=Physics.OverlapCapsuleNonAlloc(bone.position,bone.position+combo.AttackForward*extension,combo.IsRunningAttack?Mathf.Max(hitRadius,.6f):hitRadius,contacts,~0,QueryTriggerInteraction.Ignore);
  for(int i=0;i<count;i++){if(contacts[i].GetComponentInParent<CharacterMotor>()!=null)continue;Transform enemy=contacts[i].transform;while(enemy!=null&&!enemy.CompareTag("Enemy"))enemy=enemy.parent;
   if(enemy==null||enemy.IsChildOf(transform))continue;
   var bounds=contacts[i].bounds;var forward=combo.AttackForward;
   float extent=Mathf.Abs(forward.x)*bounds.extents.x+Mathf.Abs(forward.z)*bounds.extents.z;
   if(Vector3.Dot(bounds.center-transform.position,forward)<-extent-.03f||!hit.Add(enemy.GetInstanceID()))continue;
   HitCount++;LastHitSourceFrame=state.normalizedTime*strike.clip.length*strike.clip.frameRate;LastHitBone=bone;
   var dummy=enemy.GetComponent<CombatDummy>();if(dummy!=null)dummy.RegisterHit();LastHitWorldPosition=contacts[i].ClosestPoint(contactPosition);CombatHitFeedback.Play(LastHitWorldPosition,combo.driver.movementCamera,false,combo.AttackForward,contacts[i],combo.IsFifthStrike);
  }
 }
}



