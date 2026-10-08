using UnityEngine;

[DefaultExecutionOrder(9900)]
public sealed class TealWitchSkirtMotion : MonoBehaviour
{
    public Transform hips, leftLeg, rightLeg;
    public Transform[] panels;
    Quaternion leftRest, rightRest;
    Quaternion[] rest;
    void OnEnable()
    {
        if (hips == null || leftLeg == null || rightLeg == null || panels == null) return;
        leftRest = Quaternion.Inverse(hips.rotation) * leftLeg.rotation;
        rightRest = Quaternion.Inverse(hips.rotation) * rightLeg.rotation;
        rest = new Quaternion[panels.Length];
        for (int i = 0; i < panels.Length; i++) rest[i] = panels[i].localRotation;
    }
    void LateUpdate()
    {
        if (rest == null || hips == null) return;
        for (int i = 0; i < panels.Length; i++)
        {
            var leg = i < 2 ? leftLeg : rightLeg;
            var baseline = i < 2 ? leftRest : rightRest;
            var delta = Quaternion.Inverse(hips.rotation) * leg.rotation * Quaternion.Inverse(baseline);
            var target = Quaternion.RotateTowards(Quaternion.identity, delta, 45f);
            panels[i].localRotation = Quaternion.Slerp(Quaternion.identity, target, .60f) * rest[i];
        }
    }
    void OnDisable()
    {
        if (rest == null) return;
        for (int i = 0; i < rest.Length; i++) if (panels[i] != null) panels[i].localRotation = rest[i];
    }
}
