using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Rewards a successful dodge: the camera performs a Hitchcock (dolly) zoom and the game
/// slows to half speed until the dodge animation is over.
///
/// "Successful" means an attack actually reached the character and was refused because it
/// was mid-dodge - exactly the case <see cref="CharacterHitReaction.ReceiveHit"/> counts in
/// DodgeRejectCount. Nothing else has to be modified: this component polls that counter in
/// LateUpdate, after PartyMeleeHitTest (execution order 9000) has resolved the frame's
/// contacts, so the reaction lands on the same frame as the miss.
///
/// Drop it on the playground root (the object holding PlayerRoster); the
/// references fill themselves in.
/// </summary>
[DisallowMultipleComponent, DefaultExecutionOrder(9500)]
public sealed class DodgeSuccessDirector : MonoBehaviour
{
    public enum ZoomMode
    {
        None = 0,
        /// <summary>Plain field of view punch. The whole image scales.</summary>
        FieldOfView = 1,
        /// <summary>Hitchcock / vertigo: the camera dollies while the lens compensates, so the character holds its size and only the background warps.</summary>
        DollyZoom = 2
    }

    public enum AfterimageBlend
    {
        /// <summary>Semi transparent over what is behind.</summary>
        Alpha = 0,
        /// <summary>Adds light instead. Reads much stronger against the drained world.</summary>
        Additive = 1
    }

    public PlayerRoster selection;
    [Tooltip("The Cinemachine camera driven by the effect. Found automatically from the selection controller.")]
    public CinemachineCamera followCamera;
    [Tooltip("The orbital body on that camera. Its radial axis is the dolly. Found automatically.")]
    public CinemachineOrbitalFollow orbit;

    [Header("Trigger")]
    [Tooltip("Only the character the player is controlling earns the effect.")]
    public bool playerOnly = true;
    [Tooltip("Ignore further successful dodges for this long, in real seconds, so a multi-hit flurry does not restack.")]
    [Min(0f)] public float retriggerCooldown = 0.08f;

    [Header("Time")]
    [Tooltip("Time scale while the dodge lasts. 0.5 = half speed.")]
    [Range(0.05f, 1f)] public float slowScale = 0.5f;
    [Tooltip("Real seconds to fall into slow motion. 0 snaps, which reads as the sharper hit.")]
    [Min(0f)] public float blendIn = 0.02f;
    [Tooltip("Real seconds to come back up to full speed once the dodge is over.")]
    [Min(0f)] public float blendOut = 0.22f;
    [Tooltip("Extra real seconds the slow motion holds after the dodge animation finishes.")]
    [Min(0f)] public float holdAfterDodge = 0.08f;
    [Tooltip("Safety valve, in real seconds. The dodge clip itself is slowed too, so it takes longer than usual to end.")]
    [Min(0.1f)] public float maxDuration = 1.2f;
    [Tooltip("Keep the physics step in proportion with the time scale.")]
    public bool scaleFixedTimestep = true;
    [Tooltip("Play the dodging character's animation faster so only the world slows down. Shortens the effect, since the dodge then ends in normal time.")]
    public bool dodgerKeepsPace;

    [Header("Camera")]
    public ZoomMode zoomMode = ZoomMode.DollyZoom;
    [Tooltip("Real seconds to reach the full effect.")]
    [Min(0.01f)] public float zoomIn = 0.12f;
    [Tooltip("Real seconds to settle back to the normal framing.")]
    [Min(0.01f)] public float zoomOut = 0.3f;

    [Header("Dolly zoom (Hitchcock)")]
    [Tooltip("Camera distance multiplier at full effect. Below 1 pushes the camera in and widens the lens, so the background stretches away. Above 1 pulls back and narrows the lens, so the background crushes in.")]
    [Range(0.35f, 2.5f)] public float dollyFactor = 0.65f;
    [Tooltip("1 holds the character at exactly the same screen size - the true vertigo effect. Lower values let it grow or shrink a little.")]
    [Range(0f, 1f)] public float dollyCompensation = 1f;
    [Range(5f, 60f)] public float minFieldOfView = 10f;
    [Range(40f, 150f)] public float maxFieldOfView = 110f;

    [Header("Plain zoom")]
    [Tooltip("Field of view multiplier at full effect, used only by the FieldOfView mode.")]
    [Range(0.4f, 1.2f)] public float zoomFactor = 0.8f;

    [Header("Colour")]
    [Tooltip("Everything the camera can see except the dodging character loses its colour. Needs the Focus Desaturate renderer feature.")]
    public bool desaturateWorld = true;
    [Range(0f, 1f)] public float desaturateStrength = 1f;
    [Tooltip("Real seconds for the colour to drain away.")]
    [Min(0.01f)] public float desaturateIn = 0.14f;
    [Tooltip("Real seconds for the colour to come back.")]
    [Min(0.01f)] public float desaturateOut = 0.4f;
    [Tooltip("Height above the character's feet that the remaining colour radiates from.")]
    public float colourCentreHeight = 1.05f;

    [Header("Afterimages")]
    [Tooltip("Leave a frozen, semi transparent copy of the character behind at a fixed interval.")]
    public bool afterimages = true;
    [Tooltip("Material using Stylized/Dodge Afterimage. Left empty, one is built from the shader.")]
    public Material afterimageMaterial;
    [Tooltip("Seconds between copies.")]
    [Min(0.01f)] public float afterimageInterval = 0.1f;
    [Tooltip("Measure the interval in real seconds. Off uses slowed game time, which leaves half as many.")]
    public bool afterimageRealTime = true;
    [Tooltip("Ceiling on how many copies can exist at once.")]
    [Range(1, 48)] public int afterimageLimit = 24;
    [Range(0f, 1f)] public float afterimageAlpha = 0.45f;
    public AfterimageBlend afterimageBlend = AfterimageBlend.Alpha;

    [Header("Afterimage rainbow")]
    [Range(0f, 1f)] public float hueStart;
    [Tooltip("How far the hue advances per copy. 0.12 walks a full rainbow over about eight copies.")]
    [Range(0f, 0.5f)] public float hueStep = 0.12f;
    [Range(0f, 1f)] public float hueSaturation = 1f;
    [Range(0f, 1f)] public float hueValue = 1f;

    public int TriggerCount { get; private set; }
    public bool IsActive => holding || timeWeight > 0.0001f || zoomWeight > 0.0001f;
    public float TimeWeight => timeWeight;
    public float ZoomWeight => zoomWeight;
    public float ColourWeight => colourWeight;

    CharacterMotor[] tracked;
    CharacterHitReaction[] reactions;
    int[] rejects;

    CharacterMotor dodger;
    // Kept past the end of the dodge so the colour has something to fade back around.
    CharacterMotor colourSubject;
    bool holding;
    float startedAt, activeUntil, lastTrigger = float.NegativeInfinity;
    float timeWeight, zoomWeight, colourWeight;

    // Resting camera values, captured the moment an effect starts.
    float baseFieldOfView, baseRadial;
    bool cameraCaptured;

    float defaultFixedDelta;
    bool touchedTime;
    bool parryFrozeTime;
    float dodgerBaseAnimatorSpeed = 1f;
    Animator pacedAnimator;

    readonly DodgeAfterimages ghosts = new DodgeAfterimages();
    Material ghostRuntime;
    float lastGhost = float.NegativeInfinity;
    int ghostIndex;

    static readonly int s_SrcBlend = Shader.PropertyToID("_SrcBlend");
    static readonly int s_DstBlend = Shader.PropertyToID("_DstBlend");

    void Reset() { AutoWire(); }

    void OnEnable()
    {
        AutoWire();
        defaultFixedDelta = Time.fixedDeltaTime;
        Rebuild();
        holding = false;
        timeWeight = zoomWeight = colourWeight = 0f;
        dodger = colourSubject = null;
        lastGhost = float.NegativeInfinity;
        ghostIndex = 0;
        ghosts.Clear();
        FocusSubject.Clear();
    }

    void AutoWire()
    {
        if (selection == null) selection = GetComponent<PlayerRoster>();
        if (selection == null) selection = FindAnyObjectByType<PlayerRoster>();
        if (followCamera == null && selection != null) followCamera = selection.followCamera;
        if (followCamera == null) followCamera = FindAnyObjectByType<CinemachineCamera>();
        if (orbit == null && followCamera != null) orbit = followCamera.GetComponent<CinemachineOrbitalFollow>();
        if (orbit == null) orbit = FindAnyObjectByType<CinemachineOrbitalFollow>();
    }

    void Rebuild()
    {
        if (selection == null || selection.characters == null) { tracked = null; return; }
        tracked = selection.characters;
        reactions = new CharacterHitReaction[tracked.Length];
        rejects = new int[tracked.Length];
        for (int i = 0; i < tracked.Length; i++)
        {
            if (tracked[i] == null) continue;
            reactions[i] = tracked[i].GetComponent<CharacterHitReaction>();
            if (reactions[i] != null) rejects[i] = reactions[i].DodgeRejectCount;
        }
    }

    // Camera work happens in Update so it lands before PlayerOrbitCameraInput's value
    // is consumed by the brain in LateUpdate. Writing the dolly and the lens in the same
    // place is what keeps the character locked to its screen size.
    void Update()
    {
        float delta = Time.unscaledDeltaTime;
        timeWeight = Approach(timeWeight, holding ? 1f : 0f, holding ? blendIn : blendOut, delta);
        zoomWeight = Approach(zoomWeight, holding ? 1f : 0f, holding ? zoomIn : zoomOut, delta);
        colourWeight = Approach(colourWeight, holding ? 1f : 0f, holding ? desaturateIn : desaturateOut, delta);

        ApplyTime();
        ApplyCamera();
        ApplyColour();
        ApplyAfterimages();
    }

    // Detection runs late, after the frame's melee contacts have been resolved.
    void LateUpdate()
    {
        Detect();
        UpdateHold();
    }

    static float Approach(float current, float target, float seconds, float delta)
    {
        if (seconds <= 0.0001f) return target;
        return Mathf.MoveTowards(current, target, delta / seconds);
    }

    void Detect()
    {
        if (selection == null) return;
        if (tracked == null || reactions == null || tracked != selection.characters || reactions.Length != selection.characters.Length)
            Rebuild();
        if (tracked == null) return;

        for (int i = 0; i < tracked.Length; i++)
        {
            var reaction = reactions[i];
            if (reaction == null) continue;

            int count = reaction.DodgeRejectCount;
            if (count == rejects[i]) continue;
            // A lower count means the component was rebuilt; resynchronise instead of firing.
            bool advanced = count > rejects[i];
            rejects[i] = count;
            if (!advanced) continue;

            var driver = tracked[i];
            if (driver == null || !driver.IsDodging) continue;
            if (playerOnly && driver != selection.ActiveCharacter) continue;
            if (Time.unscaledTime - lastTrigger < retriggerCooldown) continue;

            Trigger(driver);
        }
    }

    /// <summary>Starts the effect manually - useful for a parry or any other earned moment.</summary>
    public void Trigger(CharacterMotor driver)
    {
        if (driver == null) return;

        if (!holding && timeWeight <= 0.0001f)
            startedAt = Time.unscaledTime;

        if (!holding)
        {
            // A fresh dodge restarts the rainbow and drops a copy on the very first frame.
            lastGhost = float.NegativeInfinity;
            ghostIndex = 0;
        }

        CaptureCameraRest();
        ReleasePace();

        // Re-read the hierarchy each time: the sword may have been drawn or stowed since.
        if (desaturateWorld)
        {
            colourSubject = driver;
            FocusSubject.Capture(driver.gameObject);
        }

        dodger = driver;
        holding = true;
        lastTrigger = Time.unscaledTime;
        activeUntil = Time.unscaledTime + holdAfterDodge;
        TriggerCount++;

        if (dodgerKeepsPace && driver.animator != null)
        {
            pacedAnimator = driver.animator;
            dodgerBaseAnimatorSpeed = pacedAnimator.speed;
        }
    }

    void UpdateHold()
    {
        if (!holding) return;

        if (dodger != null && dodger.IsDodging && dodger.isActiveAndEnabled)
            activeUntil = Time.unscaledTime + holdAfterDodge;

        bool expired = Time.unscaledTime >= activeUntil || Time.unscaledTime - startedAt >= maxDuration;
        if (!expired) return;

        holding = false;
        dodger = null;
        ReleasePace();
    }

    void ApplyTime()
    {
        float scale = Mathf.Lerp(1f, slowScale, timeWeight);
        var parry = CharacterParryAction.Active;
        if (parry != null) scale = Mathf.Min(scale, parry.RequestedScale);
        if (parry != null) parry.SetPace(scale);

        if (timeWeight > 0.0001f || parry != null)
        {
            // CombatHitFeedback parks the time scale at zero for its hit stop and then puts
            // back the value it captured. Never fight it - the next frame restores our scale.
            if (Time.timeScale > 0.0001f || parryFrozeTime && !CombatHitFeedback.IsPaused) Time.timeScale = scale;
            parryFrozeTime = parry != null && parry.FreezesWorld || parryFrozeTime && CombatHitFeedback.IsPaused;
            if (scaleFixedTimestep) Time.fixedDeltaTime = defaultFixedDelta * Mathf.Max(0.05f, scale);
            touchedTime = true;
        }
        else if (touchedTime)
        {
            if (Time.timeScale > 0.0001f || parryFrozeTime && !CombatHitFeedback.IsPaused) Time.timeScale = 1f;
            if (!CombatHitFeedback.IsPaused) parryFrozeTime = false;
            if (scaleFixedTimestep) Time.fixedDeltaTime = defaultFixedDelta;
            // A hit stop may restore its captured slow scale later; keep the reset pending.
            touchedTime = Time.timeScale <= 0.0001f;
        }

        if (pacedAnimator != null)
        {
            if (holding && dodgerKeepsPace) pacedAnimator.speed = dodgerBaseAnimatorSpeed / Mathf.Max(0.05f, scale);
            else ReleasePace();
        }
    }

    void ReleasePace()
    {
        if (pacedAnimator == null) return;
        pacedAnimator.speed = dodgerBaseAnimatorSpeed;
        pacedAnimator = null;
    }

    void CaptureCameraRest()
    {
        if (cameraCaptured || followCamera == null) return;
        baseFieldOfView = followCamera.Lens.FieldOfView;
        baseRadial = orbit != null ? orbit.RadialAxis.Value : 1f;
        cameraCaptured = true;
    }

    void ApplyCamera()
    {
        if (!cameraCaptured || followCamera == null) return;

        if (zoomMode == ZoomMode.None) { ReleaseCamera(); return; }

        float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoomWeight));

        if (zoomMode == ZoomMode.FieldOfView)
        {
            SetFieldOfView(Mathf.Lerp(baseFieldOfView, baseFieldOfView * zoomFactor, eased));
        }
        else
        {
            // Dolly first, then read back what the axis actually accepted: if its range clips
            // the travel, the lens compensates for the real distance and the subject still
            // holds its size - the effect just gets weaker.
            float k = Mathf.Lerp(1f, dollyFactor, eased);
            if (orbit != null)
            {
                float applied = orbit.RadialAxis.ClampValue(baseRadial * k);
                orbit.RadialAxis.Value = applied;
                k = applied / Mathf.Max(0.0001f, baseRadial);
            }

            // Constant subject size means distance * tan(fov / 2) stays constant.
            float halfRest = Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad);
            float half = Mathf.Lerp(halfRest, halfRest / Mathf.Max(0.0001f, k), Mathf.Clamp01(dollyCompensation));
            float fov = 2f * Mathf.Atan(half) * Mathf.Rad2Deg;
            SetFieldOfView(Mathf.Clamp(fov, minFieldOfView, maxFieldOfView));
        }

        if (zoomWeight <= 0.0001f && !holding) ReleaseCamera();
    }

    void ApplyColour()
    {
        if (!desaturateWorld)
        {
            if (FocusSubject.Weight > 0f) FocusSubject.Clear();
            return;
        }

        // Ease the ramp so the colour drains and returns rather than switching.
        FocusSubject.Weight = Mathf.SmoothStep(0f, 1f, colourWeight) * Mathf.Clamp01(desaturateStrength);

        if (colourSubject != null)
        {
            FocusSubject.CentreWorld = colourSubject.transform.position + Vector3.up * colourCentreHeight;
            FocusSubject.HasCentre = true;
        }

        if (colourWeight <= 0.0001f && !holding)
        {
            FocusSubject.Clear();
            colourSubject = null;
        }
    }

    void ApplyAfterimages()
    {
        if (!afterimages)
        {
            if (ghosts.Count > 0) ghosts.Clear();
            return;
        }

        var material = ResolveGhostMaterial();
        if (material == null) return;

        float now = afterimageRealTime ? Time.unscaledTime : Time.time;
        if (holding && dodger != null && dodger.IsDodging
            && now - lastGhost >= afterimageInterval
            && ghosts.Count < afterimageLimit)
        {
            var colour = Color.HSVToRGB(Mathf.Repeat(hueStart + ghostIndex * hueStep, 1f), hueSaturation, hueValue);
            ghosts.Spawn(dodger, material, colour);
            ghostIndex++;
            lastGhost = now;
        }

        // They hang in the air for the whole grey window and lift out with it.
        ghosts.SetAlpha(afterimageAlpha * Mathf.SmoothStep(0f, 1f, colourWeight));

        if (colourWeight <= 0.0001f && !holding && ghosts.Count > 0)
        {
            ghosts.Clear();
            ghostIndex = 0;
        }
    }

    // Always paint from a private copy so the blend mode never dirties the asset.
    Material ResolveGhostMaterial()
    {
        Shader shader = afterimageMaterial != null ? afterimageMaterial.shader : Shader.Find("Stylized/Dodge Afterimage");
        if (shader == null) return null;

        if (ghostRuntime == null || ghostRuntime.shader != shader)
        {
            CoreUtils.Destroy(ghostRuntime);
            ghostRuntime = afterimageMaterial != null ? new Material(afterimageMaterial) : new Material(shader);
            ghostRuntime.hideFlags = HideFlags.HideAndDontSave;
        }

        bool additive = afterimageBlend == AfterimageBlend.Additive;
        ghostRuntime.SetFloat(s_SrcBlend, (float)BlendMode.SrcAlpha);
        ghostRuntime.SetFloat(s_DstBlend, (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        return ghostRuntime;
    }

    void ReleaseCamera()
    {
        if (!cameraCaptured) return;
        SetFieldOfView(baseFieldOfView);
        // Hand the dolly back exactly where the player's own zoom left it.
        if (orbit != null) orbit.RadialAxis.Value = orbit.RadialAxis.ClampValue(baseRadial);
        cameraCaptured = false;
    }

    void SetFieldOfView(float value)
    {
        var lens = followCamera.Lens;
        lens.FieldOfView = value;
        followCamera.Lens = lens;
    }

    void OnDisable()
    {
        holding = false;
        dodger = colourSubject = null;
        timeWeight = zoomWeight = colourWeight = 0f;
        ReleasePace();
        ReleaseCamera();
        FocusSubject.Clear();
        ghosts.Clear();
        ghostIndex = 0;
        CoreUtils.Destroy(ghostRuntime);
        ghostRuntime = null;

        if (touchedTime)
        {
            if (Time.timeScale > 0.0001f || parryFrozeTime) Time.timeScale = 1f;
            parryFrozeTime = false;
            Time.fixedDeltaTime = defaultFixedDelta;
            touchedTime = false;
        }
    }
}
