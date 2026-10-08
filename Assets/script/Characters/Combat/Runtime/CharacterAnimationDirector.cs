using UnityEngine;

/// <summary>
/// One transition gate per actor. Gameplay scripts keep their timing and motion,
/// but cannot restore locomotion over a live action or replace a terminal pose.
/// Owned by the driver; no scene/prefab migration or second set of state flags.
/// </summary>
public sealed class CharacterAnimationDirector
{
    public enum Action { Locomotion, Attack, Dodge, Hit, Knockdown, Death, Overlay }

    readonly CharacterMotor driver;
    bool interrupting;
    public int RejectedTransitions { get; private set; }

    public CharacterAnimationDirector(CharacterMotor driver) { this.driver = driver; }

    public bool HasAttackOwner =>
        (driver.GetComponent<CharacterComboAttack>() is CharacterComboAttack combo && combo.isActiveAndEnabled && combo.IsAttacking) ||
        (driver.GetComponent<SwordThrowSkill>() is SwordThrowSkill sword && sword.isActiveAndEnabled && sword.OwnsBody) ||
        (driver.GetComponent<OrbBeamSkill>() is OrbBeamSkill beam && beam.isActiveAndEnabled && beam.OwnsBody) ||
        (driver.GetComponent<CharacterParryAction>() is CharacterParryAction parry && parry.isActiveAndEnabled && parry.OwnsBody);

    public bool CanTransition(Action action)
    {
        var stats = driver.GetComponent<CharacterCombatStats>();
        if (action == Action.Death) return stats != null && stats.IsDead;
        if (interrupting || (stats != null && (stats.IsDead || stats.IsCapturing || stats.IsCaptured))) return false;
        switch (action)
        {
            case Action.Locomotion:
                return !driver.IsActionBlocked && !driver.IsDodging && !driver.IsAttacking && !HasAttackOwner;
            case Action.Attack:
                return !driver.IsActionBlocked && !driver.IsDodging;
            case Action.Dodge:
                return !driver.IsActionBlocked && !driver.IsAttacking && !HasAttackOwner;
            case Action.Overlay:
                return !driver.IsActionBlocked;
            case Action.Hit:
                // A sword recoil may precede an already-started poise fall.
                var poise = driver.GetComponent<EnemyPoise>();
                return !driver.IsIncapacitated || (poise != null && poise.Phase == EnemyPoise.RecoveryPhase.Falling);
            case Action.Knockdown:
                return driver.IsIncapacitated;
            default:
                return false;
        }
    }

    bool Accept(Action action, int layer)
    {
        var animator = driver.animator;
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            layer < 0 || layer >= animator.layerCount || !CanTransition(action))
        { RejectedTransitions++; return false; }
        return true;
    }

    public bool CrossFade(Action action, string state, float blend, int layer = 0, float offset = 0)
        => CrossFade(action, Animator.StringToHash(state), blend, layer, offset);

    public bool CrossFade(Action action, int state, float blend, int layer = 0, float offset = 0)
    {
        if (!Accept(action, layer)) return false;
        driver.animator.CrossFadeInFixedTime(state, blend, layer, offset);
        return true;
    }

    public bool Play(Action action, string state, int layer, float normalizedTime)
        => Play(action, Animator.StringToHash(state), layer, normalizedTime);

    public bool Play(Action action, int state, int layer, float normalizedTime)
    {
        if (!Accept(action, layer)) return false;
        driver.animator.Play(state, layer, normalizedTime);
        return true;
    }

    /// <summary>Suppress recovery requests while old actions release their resources.</summary>
    public void InterruptActions()
    {
        if (interrupting) return;
        interrupting = true;
        try
        {
            driver.GetComponent<PokeBallThrow>()?.Interrupt();
            driver.GetComponent<SwordThrowSkill>()?.InterruptForHit();
            driver.GetComponent<OrbBeamSkill>()?.InterruptForHit();
            driver.GetComponent<CharacterComboAttack>()?.CancelForControlChange();
            driver.GetComponent<OrbDashAttack>()?.EndMotion();
            driver.GetComponent<CharacterParryAction>()?.Interrupt();
        }
        finally { interrupting = false; }
    }
}
