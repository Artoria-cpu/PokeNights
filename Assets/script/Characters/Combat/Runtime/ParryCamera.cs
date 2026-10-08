using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Accents the clash: pushes the camera in and rolls it over, on top of whatever the
/// gameplay camera is already doing.
///
/// Everything is expressed RELATIVE to the live camera state each frame rather than as an
/// absolute pose captured on entry, so the camera keeps trailing the character throughout
/// the parry instead of parking in world space. Two independent weights drive it:
///   Begin      - the parry has triggered. The push-in starts, timed to land when the
///                actors are expected to meet.
///   MarkContact- they actually met. The remainder of the push-in snaps home and the dutch
///                roll kicks in on the side the strike came from.
///   End        - they are sliding apart. Both weights ease back to zero.
/// </summary>
[DisallowMultipleComponent]
public sealed class ParryCamera : CinemachineExtension
{
    [Header("Push in")]
    [Tooltip("How far to dolly towards the clash, as a fraction of the current distance. 0 disables it.")]
    [Range(0f, 0.8f)] public float zoomAmount = .15f;

    [Header("Dutch roll")]
    [Tooltip("Degrees of roll at full effect. The side is taken from which way the strike came in.")]
    [Range(0f, 80f)] public float dutchDegrees = 15f;
    [Tooltip("Flip which side the roll goes to.")]
    public bool invertDutch;

    [Header("Orbit to the side")]
    [Tooltip("The original wide clash framing. Off by default; the push-in and roll read on their own.")]
    public bool orbitToSide;
    public float distance = 4f, sideOffset = 4f;

    [Header("Timing")]
    [Tooltip("Seconds to absorb whatever is left of the push-in, and to snap the roll on, at contact.")]
    [Range(0.01f, 0.3f)] public float contactSnap = .08f;
    [Tooltip("Seconds to ease everything back to the gameplay camera once the actors separate.")]
    [Min(0.02f)] public float blendOut = .35f;
    [Tooltip("Hard limit, in real seconds, before the camera releases itself if End is never called.")]
    [Min(0.5f)] public float safetySeconds = 5f;

    [Tooltip("Legacy. Kept so existing setup tools keep compiling.")]
    public float blendIn = .28f, hold = .5f;

    CharacterMotor subject, opponent;
    float started, approachSeconds;
    float weight, weightTarget, weightRate;
    float dutch, dutchTarget, dutchRate;
    float side = 1f;
    bool running;

    /// <summary>Right-click the component header: the scene may hold older serialised values.</summary>
    [ContextMenu("Apply recommended timing")]
    void ApplyRecommendedTiming()
    {
        zoomAmount = .15f; dutchDegrees = 15f; orbitToSide = false;
        contactSnap = .08f; blendOut = .35f; safetySeconds = 5f;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[ParryCamera] zoom 0.15, dutch 15, orbit-to-side off.", this);
    }

    /// <param name="approach">Seconds the push-in should take, ideally the time until contact.</param>
    public void Begin(CharacterMotor player, CharacterMotor enemy, float approach)
    {
        subject = player; opponent = enemy; running = true;
        approachSeconds = Mathf.Max(.02f, approach);
        started = Time.unscaledTime;

        weight = 0f; weightTarget = 1f; weightRate = 1f / approachSeconds;
        dutch = dutchTarget = 0f; dutchRate = 1f / Mathf.Max(.01f, contactSnap);
    }

    /// <param name="mirror">True when the incoming strike came from the subject's left.</param>
    public void MarkContact(bool mirror)
    {
        if (subject == null || !running) return;
        side = (mirror ? -1f : 1f) * (invertDutch ? -1f : 1f);
        weightTarget = 1f; weightRate = 1f / Mathf.Max(.01f, contactSnap);
        dutchTarget = 1f; dutchRate = 1f / Mathf.Max(.01f, contactSnap);
    }

    public void End()
    {
        if (subject == null || !running) return;
        running = false;
        weightTarget = dutchTarget = 0f;
        weightRate = dutchRate = 1f / Mathf.Max(.02f, blendOut);
        // Reset the underlying follow view, but conceal the change behind our blend.
        GetComponent<PlayerOrbitCameraInput>()?.ResetView();
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || subject == null) return;

        if (running && (!subject.gameObject.activeInHierarchy || Time.unscaledTime - started >= safetySeconds))
            End();

        float step = Time.unscaledDeltaTime;
        weight = Mathf.MoveTowards(weight, weightTarget, weightRate * step);
        dutch = Mathf.MoveTowards(dutch, dutchTarget, dutchRate * step);

        if (!running && weight <= 0.0001f && dutch <= 0.0001f)
        {
            subject = null; opponent = null;
            return;
        }

        var original = state.GetFinalPosition();
        var rotation = state.RawOrientation;

        if (weight > 0.0001f)
        {
            var focus = (opponent != null ? (subject.transform.position + opponent.transform.position) * .5f
                                          : subject.transform.position) + Vector3.up * 1.15f;
            float eased = Mathf.SmoothStep(0f, 1f, weight);
            Vector3 position;

            if (orbitToSide)
            {
                var body = subject.visualRoot != null ? subject.visualRoot : subject.transform;
                var target = focus + body.forward * distance + body.right * sideOffset + Vector3.up * .65f;
                var offset = target - focus; float limit = offset.magnitude;
                foreach (var hit in Physics.SphereCastAll(focus, .18f, offset.normalized, limit, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(subject.transform) && hit.collider.GetComponentInParent<CharacterMotor>() == null)
                        limit = Mathf.Min(limit, Mathf.Max(.4f, hit.distance - .1f));
                target = focus + offset.normalized * limit;

                position = focus + Vector3.Slerp(original - focus, target - focus, eased);
                rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(focus - position, Vector3.up), eased);
            }
            else
            {
                // Straight dolly towards the clash along the live view ray; the gameplay
                // camera keeps deciding where it is, we only close some of the gap.
                position = Vector3.Lerp(original, focus, Mathf.Clamp01(zoomAmount) * eased);
            }

            state.PositionCorrection += position - original;
        }

        if (dutch > 0.0001f)
            rotation *= Quaternion.AngleAxis(dutchDegrees * side * Mathf.SmoothStep(0f, 1f, dutch), Vector3.forward);

        state.RawOrientation = rotation;
    }
}
