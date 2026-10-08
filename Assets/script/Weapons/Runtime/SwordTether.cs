using UnityEngine;

/// <summary>Small damped rope simulation; endpoints follow the final animated hand and sword poses.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(LineRenderer)), DefaultExecutionOrder(9500)]
public sealed class SwordTether : MonoBehaviour
{
    const int Count = 19, Iterations = 10;
    const float Step = 1f / 90f, Radius = .018f;
    readonly Vector3[] points = new Vector3[Count], previous = new Vector3[Count], beforeStep = new Vector3[Count];
    readonly RaycastHit[] hits = new RaycastHit[12];
    SwordThrowSkill owner;
    Transform hand, hilt;
    LineRenderer line;
    float accumulator, length;
    bool initialized;

    public void Initialize(SwordThrowSkill skill, Transform start, Transform end)
    {
        owner = skill; hand = start; hilt = end; line = GetComponent<LineRenderer>();
        line.positionCount = Count; line.useWorldSpace = true;
        ResetPoints();
    }
    void ResetPoints()
    {
        length = Vector3.Distance(hand.position, hilt.position) + owner.ropeSag;
        for (int i = 0; i < Count; i++)
        {
            float t = (float)i / (Count - 1);
            points[i] = previous[i] = Vector3.Lerp(hand.position, hilt.position, t) - Vector3.up * (4 * t * (1 - t) * owner.ropeSag);
        }
        initialized = true; accumulator = 0;
    }
    void LateUpdate()
    {
        if (!initialized || owner == null || hand == null || hilt == null) return;
        if ((hand.position - points[0]).sqrMagnitude > 16 || (hilt.position - points[Count - 1]).sqrMagnitude > 36) ResetPoints();
        accumulator = Mathf.Min(accumulator + Time.deltaTime, Step * 5);
        while (accumulator >= Step) { Simulate(); accumulator -= Step; }
        // Final pinning also removes the one-frame gap after pose blending or hit-stop starts.
        points[0] = hand.position; points[Count - 1] = hilt.position;
        line.SetPositions(points);
    }
    void Simulate()
    {
        float distance = Vector3.Distance(hand.position, hilt.position);
        float slack = owner.RopeTaut ? .005f : Mathf.Min(owner.ropeSag, distance * .04f + .03f);
        length = Mathf.Max(distance, Mathf.Lerp(length, distance + slack, 1 - Mathf.Exp(-12f * Step)));
        float segment = length / (Count - 1), damping = Mathf.Exp(-owner.ropeDamping * Step);
        for (int i = 1; i < Count - 1; i++)
        {
            var p = points[i]; beforeStep[i] = p;
            points[i] += Vector3.ClampMagnitude((p - previous[i]) * damping, .35f) + Vector3.down * (owner.ropeGravity * Step * Step);
            previous[i] = p;
        }
        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            points[0] = hand.position; points[Count - 1] = hilt.position;
            for (int i = 0; i < Count - 1; i++)
            {
                var delta = points[i + 1] - points[i]; float d = delta.magnitude;
                if (d < .00001f) continue;
                var correction = delta * ((d - segment) / d);
                if (i == 0) points[i + 1] -= correction;
                else if (i + 1 == Count - 1) points[i] += correction;
                else { points[i] += correction * .5f; points[i + 1] -= correction * .5f; }
            }
        }
        for (int i = 1; i < Count - 1; i++)
        {
            var travel = points[i] - beforeStep[i]; float d = travel.magnitude;
            if (d < .00001f) continue;
            int count = Physics.SphereCastNonAlloc(beforeStep[i], Radius, travel / d, hits, d, ~0, QueryTriggerInteraction.Ignore);
            float closest = d; Vector3 normal = Vector3.zero;
            for (int j = 0; j < count; j++)
            {
                var hit = hits[j];
                if (hit.distance >= closest || hit.collider.GetComponentInParent<CharacterMotor>() != null
                    || hit.collider.GetComponentInParent<CombatDummy>() != null) continue;
                closest = hit.distance; normal = hit.normal;
            }
            if (normal.sqrMagnitude > .1f)
            {
                points[i] = beforeStep[i] + travel / d * Mathf.Max(0, closest - .003f);
                previous[i] = points[i];
            }
        }
    }
}
