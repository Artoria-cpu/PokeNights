using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A swept, finite-lifetime bolt or expanding radial pulse; survives weapon switching.</summary>
public sealed class OrbProjectile : MonoBehaviour
{
    const float Radius = .18f;
    CharacterMotor owner;
    string attackType;
    float damageMultiplier;
    Vector3 direction, center;
    float speed, range, age, travelled;
    bool wave, consumed;
    LineRenderer line;
    readonly HashSet<int> hit = new HashSet<int>();
    readonly Vector3[] ring = new Vector3[65];

    public static OrbProjectile Create(CharacterMotor owner, Vector3 position, Vector3 direction,
        Material material, float speed, float range, bool wave)
    {
        var go = new GameObject(wave ? "Orb radial shockwave" : "Orb magic bolt");
        go.transform.position = position;
        var bolt = go.AddComponent<OrbProjectile>();
        bolt.owner = owner; bolt.direction = direction.normalized; bolt.center = position;
        bolt.attackType = PlayerProgression.AttackType(owner,2);
        bolt.damageMultiplier = owner.GetComponent<CharacterComboAttack>()?.DamageMultiplier ?? 1;
        bolt.speed = speed; bolt.range = range; bolt.wave = wave;
        bolt.line = go.AddComponent<LineRenderer>();
        bolt.line.sharedMaterial = material; bolt.line.useWorldSpace = true;
        bolt.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bolt.line.receiveShadows = false; bolt.line.numCapVertices = 6;
        bolt.line.widthMultiplier = wave ? .22f : .25f;
        bolt.line.startColor = new Color(.4f, 1, 1, 1); bolt.line.endColor = new Color(.35f, .25f, 1, .8f);
        bolt.Draw(0);
        return bolt;
    }
    void Update()
    {
        if (consumed || Time.deltaTime <= 0) return;
        if (owner == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (wave)
        {
            float radius = Mathf.Min(range, age / .5f * range);
            foreach (var contact in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Ignored(contact) || !IsTarget(contact)) continue;
                Vector3 point = contact.ClosestPoint(center);
                if (Blocked(center, point, contact)) continue;
                Resolve(contact, point, center);
            }
            Draw(radius);
            if (age >= .65f) Destroy(gameObject);
        }
        else
        {
            float distance = Mathf.Min(speed * Time.deltaTime, range - travelled);
            AdvanceTo(transform.position + direction * distance);
            travelled += distance;
            if (!consumed) Draw(0);
            if (travelled >= range || age > 4) Destroy(gameObject);
        }
    }
    bool Ignored(Collider c) => c == null || (owner != null && c.transform.IsChildOf(owner.transform));
    static Transform Enemy(Collider c)
    {
        for (var t = c.transform; t != null; t = t.parent) if (t.CompareTag("Enemy")) return t;
        return null;
    }
    static bool IsTarget(Collider c) => c.GetComponentInParent<CharacterHitReaction>() != null || Enemy(c) != null;
    bool Blocked(Vector3 from, Vector3 to, Collider target)
    {
        var delta = to - from;
        foreach (var contact in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!Ignored(contact.collider) && contact.collider != target && !IsTarget(contact.collider)) return true;
        return false;
    }
    public void AdvanceTo(Vector3 next)
    {
        if (consumed) return;
        var from = transform.position;
        // SphereCast does not report colliders already overlapping the starting sphere.
        foreach (var c in Physics.OverlapSphere(from, Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Ignored(c)) continue;
            Resolve(c, c.ClosestPoint(from), from - direction);
            consumed = true; Destroy(gameObject); return;
        }
        var delta = next - from;
        if (delta.sqrMagnitude < .000001f) return;
        var contacts = Physics.SphereCastAll(from, Radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(contacts, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var contact in contacts)
        {
            if (Ignored(contact.collider)) continue;
            Resolve(contact.collider, contact.point, contact.point - direction);
            consumed = true; Destroy(gameObject); return;
        }
        transform.position = next;
    }
    void Resolve(Collider contact, Vector3 point, Vector3 origin)
    {
        var reaction = contact.GetComponentInParent<CharacterHitReaction>();
        if (reaction != null)
        {
            if (reaction.driver == owner || !reaction.isActiveAndEnabled || !hit.Add(reaction.GetInstanceID())) return;
            if (!reaction.ReceiveHit(origin,attacker:owner,special:true,attackType:attackType,weaponSlot:2,damageMultiplier:damageMultiplier)) return; // A dodge consumes this contact too.
        }
        else
        {
            var enemy = Enemy(contact);
            if (enemy == null || !hit.Add(enemy.GetInstanceID())) return;
            var dummy = enemy.GetComponent<CombatDummy>();
            if (dummy != null) dummy.RegisterHit();
        }
        CombatHitFeedback.Play(point, owner.movementCamera, false, wave ? point - center : direction, contact, wave);
    }
    void Draw(float radius)
    {
        if (wave)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / (ring.Length - 1);
                ring[i] = center + new Vector3(Mathf.Cos(angle) * radius, -.65f, Mathf.Sin(angle) * radius);
            }
            line.positionCount = ring.Length; line.SetPositions(ring);
            line.widthMultiplier = .24f * Mathf.Clamp01((.65f - age) / .18f);
        }
        else
        {
            line.positionCount = 2;
            line.SetPosition(0, transform.position); line.SetPosition(1, transform.position - direction * .45f);
        }
    }
}
