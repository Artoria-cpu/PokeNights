using UnityEngine;

/// <summary>A small travelling wave along the accessory bones; the attachment stays fixed.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10100)]
public sealed class CharmanderTailSway : MonoBehaviour
{
    public Transform[] joints;
    [Min(0)] public float amplitude = 10f;
    [Min(0)] public float frequency = .65f;
    public CharacterMotor driver;
    Quaternion[] rest;
    float phase, activity, fade;

    void Awake()
    {
        rest = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++) rest[i] = joints[i].localRotation;
    }

    void OnEnable() { phase = activity = fade = 0; }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        activity = Mathf.Lerp(activity, driver != null ? Mathf.Clamp01(driver.PlanarSpeed / 5f) : 0,
            1f - Mathf.Exp(-4f * dt));
        phase += dt * frequency * (1f + .4f * activity) * Mathf.PI * 2;
        fade = Mathf.MoveTowards(fade, 1, dt * 3);
        for (int i = 0; i < joints.Length; i++)
        {
            float along = (i + 1f) / joints.Length;
            float angle = amplitude * Mathf.Lerp(.4f, 1, along) * (1 + .25f * activity) * fade;
            joints[i].localRotation = rest[i] * Quaternion.Euler(
                Mathf.Sin(phase * .7f - i * .5f) * angle * .18f,
                Mathf.Sin(phase - i * .65f) * angle, 0);
        }
    }

    void OnDisable()
    {
        if (rest == null || joints == null) return;
        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null) joints[i].localRotation = rest[i];
    }
}
