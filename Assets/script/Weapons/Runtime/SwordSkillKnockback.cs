using UnityEngine;

/// <summary>Collision-resolved push plus the authored uppercut reaction and its recovery.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(80)]
public sealed class SwordSkillKnockback : MonoBehaviour
{
    public const string ReactionState = "SwordSkillKnockback", BackReactionState = "SwordSkillBackKnockback";
    public AnimationClip reactionClip, backReactionClip;
    [Min(.1f)] public float reactionSeconds = 1f;
    [Min(.01f)] public float entryBlend = .1f, recoveryBlend = .22f;
    public bool IsReacting { get; private set; }
    CharacterController controller;
    CharacterMotor driver;
    EnemyPoise poise;
    Vector3 direction;
    float elapsed, duration, distance, reactionStarted;
    bool fallAfterReaction;
    bool fromBack;
    string activeState = ReactionState;
    public void Push(Vector3 heading, float metres, float seconds, bool playReaction = true, bool hitFromBack = false)
    {
        if (CharacterCombatStats.Dead(this)) return;
        controller = GetComponent<CharacterController>();
        if (driver == null) driver = GetComponent<CharacterMotor>();
        if (poise == null) poise = GetComponent<EnemyPoise>();
        direction = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
        distance = Mathf.Max(0, metres); duration = Mathf.Max(.05f, seconds); elapsed = 0;
        if (!playReaction || driver == null || driver.animator == null) return;
        var animator = driver.animator;
        fromBack = hitFromBack && backReactionClip != null && animator.HasState(0, Animator.StringToHash(BackReactionState));
        activeState = fromBack ? BackReactionState : ReactionState;
        if (reactionClip == null || !animator.HasState(0, Animator.StringToHash(ReactionState)))
        { driver.InterruptForHit(seconds); return; }
        // A newly poise-broken victim recoils first, then continues its existing fall/get-up sequence.
        // Already downed victims keep their recovery pose (the caller passes playReaction=false).
        fallAfterReaction = driver.IsIncapacitated;
        if (fallAfterReaction && (poise == null || poise.Phase != EnemyPoise.RecoveryPhase.Falling)) return;
        if (!fallAfterReaction) driver.InterruptForHit(reactionSeconds + .2f, holdPose: false);
        animator.SetBool("IsAttacking", false);
        int weapon = animator.GetLayerIndex("WeaponAction"); if (weapon >= 0) animator.SetLayerWeight(weapon, 0);
        reactionStarted = Time.time; IsReacting = true;
        driver.Animation.CrossFade(CharacterAnimationDirector.Action.Hit, activeState, entryBlend, 0, 0);
    }
    void Update()
    {
        if (CharacterCombatStats.Dead(this)) { elapsed = duration; IsReacting = false; return; }
        if (Time.deltaTime <= 0) return;
        if (elapsed < duration && duration > 0 && controller != null && controller.enabled)
        {
            float before = Mathf.Clamp01(elapsed / duration);
            elapsed += Time.deltaTime;
            float after = Mathf.Clamp01(elapsed / duration);
            // Integral of a linearly decelerating velocity: total unobstructed travel is exactly distance.
            float step = (2 * after - after * after) - (2 * before - before * before);
            controller.Move(direction * (distance * step));
        }
        if (!IsReacting) return;
        if (driver == null || !driver.enabled || driver.animator == null || !driver.animator.isActiveAndEnabled)
        { FinishReaction(false); return; }
        if (driver.IsIncapacitated && !fallAfterReaction) { FinishReaction(false); return; }
        var animator = driver.animator;
        int weapon = animator.GetLayerIndex("WeaponAction"); if (weapon >= 0) animator.SetLayerWeight(weapon, 0);
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        float age = Time.time - reactionStarted;
        if (!state.IsName(activeState) && age > entryBlend + .12f) { FinishReaction(false); return; }
        if ((state.IsName(activeState) && state.normalizedTime >= .98f) || age >= reactionSeconds + .15f)
        { FinishReaction(true); return; }
        if (direction.sqrMagnitude > .001f)
        {
            var body = driver.visualRoot != null ? driver.visualRoot : driver.transform;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(fromBack ? direction : -direction), 900f * Time.deltaTime);
        }
    }
    void FinishReaction(bool recover)
    {
        if (!IsReacting) return;
        IsReacting = false;
        if (driver == null) return;
        driver.EndAnimatedHit();
        if (!recover || driver.animator == null || !driver.animator.isActiveAndEnabled) return;
        if (fallAfterReaction && poise != null && poise.Phase == EnemyPoise.RecoveryPhase.Falling)
            driver.Animation.CrossFade(CharacterAnimationDirector.Action.Knockdown, EnemyPoise.FallState, recoveryBlend, 0, 0);
        else if (!driver.IsIncapacitated && !driver.IsHitStunned)
            driver.Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, driver.IsGrounded ? "Locomotion" : "Fall", recoveryBlend, 0, 0);
    }
    void OnDisable() { elapsed = duration; FinishReaction(true); }
}
