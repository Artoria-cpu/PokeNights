using UnityEngine;

/// <summary>Releases magic at the combo's authored pose marker.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterComboAttack)), DefaultExecutionOrder(110)]
public sealed class OrbWeapon : MonoBehaviour
{
    public Material effectMaterial;
    public float projectileSpeed = 18f, projectileRange = 24f, shockwaveRadius = 7f;
    CharacterComboAttack combo;
    int sequence = -1;
    bool released;
    public bool IsEquipped => combo != null && combo.equipment != null && combo.equipment.HasOrb && combo.equipment.SelectedSlot == 2
        && combo.equipment.selection.ActiveCharacter == combo.driver;
    void Awake() { combo = GetComponent<CharacterComboAttack>(); }
    void LateUpdate()
    {
        if (Time.deltaTime <= 0 || !combo.IsOrbAttack || combo.IsRunningAttack || (!combo.driver.acceptPlayerInput&&!combo.driver.AIControlled)) return;
        if (sequence != combo.AttackCount) { sequence = combo.AttackCount; released = false; }
        if (released) return;
        var strike = combo.ActiveStrike;
        var animator = combo.driver.animator;
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsName(strike.stateName) || state.normalizedTime < strike.ContactProgress) return;
        released = true;
        GameplayAudio.Play(combo.IsFifthStrike?GameplayCue.MagicBurst:GameplayCue.MagicShot,transform.position+Vector3.up,.48f);
        var driver = combo.driver;
        Vector3 chest = transform.position + Vector3.up * 1.1f;
        if (combo.IsFifthStrike)
        {
            OrbProjectile.Create(driver, chest, Vector3.zero, effectMaterial, 0, shockwaveRadius, true);
            return;
        }
        var hand = animator.GetBoneTransform(strike.contactBone);
        Vector3 origin = hand != null ? hand.position : chest;
        Vector3 forward = combo.AttackForward;
        var target = driver.combatLookTarget;
        if (target != null)
        {
            var capsule = target.GetComponent<CharacterController>();
            Vector3 aim = capsule != null ? capsule.bounds.center : target.position + Vector3.up * 1.1f;
            if ((aim - origin).sqrMagnitude > .001f) forward = (aim - origin).normalized;
        }
        var projectile = OrbProjectile.Create(driver, chest, forward, effectMaterial, projectileSpeed, projectileRange, false);
        // Sweep the hand offset as well, so casting beside a wall cannot spawn beyond it.
        projectile.AdvanceTo(origin);
    }
}
