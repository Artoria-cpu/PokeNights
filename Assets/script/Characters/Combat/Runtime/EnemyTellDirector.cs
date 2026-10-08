using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Heavy-attack eye tells and priority input for the animated counter guard.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(9600)]
public sealed class EnemyTellDirector : MonoBehaviour
{
    public PlayerRoster selection;
    [Tooltip("The camera the flare is drawn for. Taken from the selection controller when empty.")]
    public Camera outputCamera;

    [Header("Trigger")]
    [Tooltip("Attack stages that telegraph. CharacterComboAttack counts from 1.")]
    public int[] punishStages = { 4, 5 };
    [Tooltip("Only characters the player is not currently controlling can telegraph.")]
    public bool enemiesOnly = true;

    [Header("Timing")]
    [Tooltip("How long the cross flare is visible. It is meant to snap past, not linger.")]
    [Min(0.02f)] public float crossSeconds = 0.3f;
    [Tooltip("Extra seconds after the flare ends that a click still counts, to forgive input lag.")]
    [Range(0f, 0.3f)] public float windowGrace = 0.06f;
    [Tooltip("Force the punish window to this length instead of following the flare. 0 follows the flare.")]
    [Min(0f)] public float windowSecondsOverride;
    [Tooltip("How long the red lingers after the window before fading out.")]
    [Min(0f)] public float glowHold = 0.15f;

    [Header("Punish")]


    [Tooltip("How close the player has to be, in metres, measured on the ground plane.")]
    [Min(0.5f)] public float punishRange = 10f;
    [Tooltip("Also require the player to be roughly facing the enemy.")]
    public bool requireFacing;
    [Range(15f, 180f)] public float facingConeDegrees = 140f;



    [Header("Cross flare")]
    public Material crossMaterial;
    [Tooltip("Seconds for the flare to reach full brightness.")]
    [Min(0.005f)] public float crossAttack = 0.03f;
    [ColorUsage(false, true)] public Color crossColour = new Color(1f, 0.55f, 0.45f);
    [Min(0f)] public float crossIntensity = 1.6f;
    [Tooltip("Bar thickness as a fraction of screen height. The pixel filter downsamples, so very small values still read.")]
    [Range(0.0005f, 0.02f)] public float crossThickness = 0.0022f;
    [Range(0.0005f, 0.03f)] public float crossSoftness = 0.003f;
    [Range(0f, 1f)] public float crossTaper = 0.35f;
    [Range(0.005f, 0.2f)] public float crossCoreSize = 0.014f;
    [Range(0f, 4f)] public float crossCoreBoost = 0.8f;
    [Tooltip("Flash the cross again when a punish lands.")]
    public bool flashOnPunish = true;

    [Header("Red eye glow")]
    public Material glowMaterial;
    [ColorUsage(false, true)] public Color glowColour = new Color(1f, 0.08f, 0.06f);
    [Min(0f)] public float glowIntensity = 1.4f;
    [Min(0.001f)] public float glowSize = 0.13f;
    [Tooltip("Seconds for the red to rise as the flare dies.")]
    [Min(0.01f)] public float glowAttack = 0.1f;
    [Tooltip("Seconds for the red to die away once the window has passed.")]
    [Min(0.01f)] public float glowRelease = 0.22f;
    [Tooltip("Extra brightness while the punish window is actually open, so the window is readable.")]
    [Range(1f, 4f)] public float windowBoost = 1.8f;
    [Tooltip("Pulses per second outside the window. 0 holds steady.")]
    [Min(0f)] public float glowPulse = 2.5f;
    [Range(0f, 1f)] public float glowPulseDepth = 0.25f;

    [Header("Diagnostics")]
    [Tooltip("Log which transform was chosen for each eye the first time a character is resolved.")]
    public bool logEyeResolution;
    [Tooltip("Leave the generated quads visible in the hierarchy at runtime so their material can be inspected live.")]
    public bool showRuntimeObjects = true;
    [Tooltip("Hold the cross on permanently for the first enemy, for tuning thickness without waiting for a stage 4 or 5.")]
    public bool debugHoldCross;

    public int WindowsOpened { get; private set; }
    public int PunishesLanded { get; private set; }
    public bool AnyWindowOpen { get; private set; }

    sealed class Watch
    {
        public CharacterMotor driver;
        public CharacterComboAttack combo;
        public CharacterHitReaction reaction;

        public Transform leftAnchor, rightAnchor;
        public Vector3 leftOffset, rightOffset;
        public bool eyesResolved;
        public bool warnedNoEye;

        public int attackSeen = -1;

        public bool armed;
        public bool punished;
        public float crossEnd, windowStart, windowEnd, tellEnd;
        public float crossUntil = float.NegativeInfinity;
        public float glow;

        public GameObject cross;
        public Material crossInstance;
        public GameObject leftGlow, rightGlow;
        public Material glowInstance;

        public bool WindowOpen => armed && !punished && Time.time >= windowStart && Time.time < windowEnd;
    }

    readonly List<Watch> watches = new List<Watch>();
    CharacterMotor[] tracked;
    Mesh quad;

    static readonly int s_Colour = Shader.PropertyToID("_Colour");
    static readonly int s_Centre = Shader.PropertyToID("_EyeWorld");
    static readonly int s_Intensity = Shader.PropertyToID("_Intensity");
    static readonly int s_Thickness = Shader.PropertyToID("_Thickness");
    static readonly int s_Softness = Shader.PropertyToID("_Softness");
    static readonly int s_Taper = Shader.PropertyToID("_Taper");
    static readonly int s_CoreSize = Shader.PropertyToID("_CoreSize");
    static readonly int s_CoreBoost = Shader.PropertyToID("_CoreBoost");
    static readonly int s_Aspect = Shader.PropertyToID("_Aspect");
    static readonly int s_Size = Shader.PropertyToID("_Size");

    void Reset() { AutoWire(); }

    /// <summary>
    /// Serialised fields keep whatever value the scene stored when the component was first
    /// added, so editing a default in code never moves an existing instance. Right-click
    /// the component header and run this to pull the current tuning onto this instance.
    /// </summary>
    [ContextMenu("Apply current tuning defaults")]
    void ApplyTuningDefaults()
    {
        crossSeconds = 0.3f;
        crossThickness = 0.0022f;
        crossSoftness = 0.003f;
        crossCoreSize = 0.014f;
        crossCoreBoost = 0.8f;
        punishRange = 10f;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[EnemyTellDirector] tuning applied: flare 0.3s, thickness 0.0022, range 10m.", this);
    }

    void OnEnable()
    {
        AutoWire();
        Rebuild();
    }

    void OnDisable()
    {
        for (int i = 0; i < watches.Count; i++) Dispose(watches[i]);
        watches.Clear();
        tracked = null;
        if (quad != null) { CoreUtils.Destroy(quad); quad = null; }
        AnyWindowOpen = false;
    }

    void AutoWire()
    {
        if (selection == null) selection = GetComponent<PlayerRoster>();
        if (selection == null) selection = FindAnyObjectByType<PlayerRoster>();
        if (outputCamera == null && selection != null) outputCamera = selection.outputCamera;
        if (outputCamera == null) outputCamera = Camera.main;
    }

    void Rebuild()
    {
        for (int i = 0; i < watches.Count; i++) Dispose(watches[i]);
        watches.Clear();

        if (selection == null || selection.characters == null) { tracked = null; return; }
        tracked = selection.characters;

        foreach (var driver in tracked)
        {
            if (driver == null) continue;
            var combo = driver.GetComponent<CharacterComboAttack>();
            if (combo == null) continue;

            var watch = new Watch
            {
                driver = driver,
                combo = combo,
                reaction = driver.GetComponent<CharacterHitReaction>(),
                attackSeen = combo.AttackCount
            };
                watches.Add(watch);
        }
    }

    void LateUpdate()
    {
        if (selection == null) return;
        if (tracked != selection.characters) Rebuild();
        if (outputCamera == null) AutoWire();

        bool any = false;
        for (int i = 0; i < watches.Count; i++)
        {
            var watch = watches[i];
            if (watch.driver == null || watch.combo == null) continue;

            Detect(watch);

            Drive(watch);
            any |= watch.WindowOpen;
        }
        AnyWindowOpen = any;
    }

    bool IsEnemy(Watch watch) => !enemiesOnly || selection.ActiveCharacter != watch.driver;

    void Detect(Watch watch)
    {
        var combo = watch.combo;

        if (combo.AttackCount != watch.attackSeen)
        {
            watch.attackSeen = combo.AttackCount;
            if (combo.IsAttacking && IsEnemy(watch) && (combo.IsRunningAttack || Telegraphs(combo.CurrentStage)))
                Arm(watch);
        }

        if (!watch.armed) return;

        // A knockdown, or losing enemy status, cancels the whole tell.
        if (watch.driver.IsIncapacitated || !IsEnemy(watch))
        {
            watch.armed = false;
            watch.tellEnd = Time.time;
            return;
        }

        if (Time.time >= watch.tellEnd) watch.armed = false;
    }

    bool Telegraphs(int stage)
    {
        if (punishStages == null) return false;
        for (int i = 0; i < punishStages.Length; i++)
            if (punishStages[i] == stage) return true;
        return false;
    }

    void Arm(Watch watch)
    {
        float now = Time.time;
        watch.armed = true;
        watch.punished = false;
        watch.crossEnd = now + crossSeconds;
        watch.crossUntil = watch.crossEnd;
        // The flare IS the window; the grace only forgives input lag at its tail.
        watch.windowStart = now;
        watch.windowEnd = windowSecondsOverride > 0f
            ? now + windowSecondsOverride + windowGrace
            : watch.crossEnd + windowGrace;
        watch.tellEnd = watch.windowEnd + glowHold + glowRelease;
        WindowsOpened++;
    }

    public bool TryParry(CharacterMotor player)
    {
        if(selection==null||player==null||selection.ActiveCharacter!=player)return false;
        var guard=player.GetComponent<CharacterParryAction>();
        if(guard==null||!guard.CanBegin)return false;
        Watch chosen=null;float closest=float.PositiveInfinity;
        foreach(var watch in watches)
        {
            Detect(watch);
            if(!watch.WindowOpen||watch.driver==player||watch.driver.IsActionBlocked||!watch.combo.IsAttacking)continue;
            var delta=Vector3.ProjectOnPlane(watch.driver.transform.position-player.transform.position,Vector3.up);
            if(delta.magnitude>punishRange||delta.sqrMagnitude>=closest)continue;
            if(requireFacing&&delta.sqrMagnitude>.0001f)
            {
                var body=player.visualRoot!=null?player.visualRoot:player.transform;
                if(Vector3.Dot(Vector3.ProjectOnPlane(body.forward,Vector3.up).normalized,delta.normalized)<Mathf.Cos(facingConeDegrees*.5f*Mathf.Deg2Rad))continue;
            }
            if(!PartyEnemyAI.HasLineOfSight(player,watch.driver))continue;
            chosen=watch;closest=delta.sqrMagnitude;
        }
        if(chosen==null||!guard.Begin(chosen.driver))return false;
        chosen.punished=true;chosen.tellEnd=Time.time+glowRelease;
        if(flashOnPunish)chosen.crossUntil=Time.time+crossSeconds;
        PunishesLanded++;return true;
    }
    void Drive(Watch watch)
    {
        ResolveEyes(watch);

        float crossWeight = debugHoldCross && watch == watches[0] ? 1f : 0f;
        if (Time.time < watch.crossUntil)
        {
            float remaining = watch.crossUntil - Time.time;
            float elapsed = crossSeconds - remaining;
            float rise = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, crossAttack));
            float fall = Mathf.Clamp01(remaining / Mathf.Max(0.001f, crossSeconds - crossAttack));
            crossWeight = rise * fall * fall;      // snap in, ease out
        }

        // Red starts rising while the flare is still dying, holds across the window,
        // then drains once the window and its hold are past.
        bool wantGlow = watch.armed
                        && !watch.punished
                        && Time.time >= watch.crossEnd - glowAttack
                        && Time.time <= watch.windowEnd + glowHold;
        float rate = wantGlow ? 1f / Mathf.Max(0.01f, glowAttack) : 1f / Mathf.Max(0.01f, glowRelease);
        watch.glow = Mathf.MoveTowards(watch.glow, wantGlow ? 1f : 0f, rate * Time.unscaledDeltaTime);

        DriveCross(watch, crossWeight);
        DriveGlow(watch);
    }

    void DriveCross(Watch watch, float weight)
    {
        if (weight <= 0.001f)
        {
            if (watch.cross != null && watch.cross.activeSelf) watch.cross.SetActive(false);
            return;
        }
        if (outputCamera == null) return;

        // No anchor means no cross. Never quietly fall back to the middle of the screen -
        // that reads as "working but wrong" instead of "not resolved".
        if (!TryEyeCentre(watch, out Vector3 eye))
        {
            if (watch.cross != null) watch.cross.SetActive(false);
            if (!watch.warnedNoEye)
            {
                watch.warnedNoEye = true;
                Debug.LogWarning($"[EnemyTellDirector] {watch.driver.name}: no eye anchor and no Head bone, cross suppressed.", watch.driver);
            }
            return;
        }




        EnsureCross(watch);
        if (watch.cross == null) return;
        watch.cross.SetActive(true);

        float aspect = outputCamera.pixelHeight > 0
            ? (float)outputCamera.pixelWidth / outputCamera.pixelHeight
            : 1.777f;

        var material = watch.crossInstance;
        material.SetColor(s_Colour, crossColour);
        material.SetVector(s_Centre, new Vector4(eye.x, eye.y, eye.z, 1f));
        material.SetFloat(s_Intensity, crossIntensity * weight);
        material.SetFloat(s_Thickness, crossThickness);
        material.SetFloat(s_Softness, crossSoftness);
        material.SetFloat(s_Taper, crossTaper);
        material.SetFloat(s_CoreSize, crossCoreSize);
        material.SetFloat(s_CoreBoost, crossCoreBoost);
        material.SetFloat(s_Aspect, aspect);
    }

    void DriveGlow(Watch watch)
    {
        if (watch.glow <= 0.001f)
        {
            if (watch.leftGlow != null && watch.leftGlow.activeSelf) watch.leftGlow.SetActive(false);
            if (watch.rightGlow != null && watch.rightGlow.activeSelf) watch.rightGlow.SetActive(false);
            return;
        }

        EnsureGlow(watch);
        if (watch.glowInstance == null) return;

        // Inside the window the eyes burn steady and bright; outside they only smoulder.
        float shape = watch.WindowOpen
            ? windowBoost
            : (glowPulse > 0f
                ? 1f - glowPulseDepth * 0.5f * (1f - Mathf.Cos(Time.unscaledTime * glowPulse * Mathf.PI * 2f))
                : 1f);

        watch.glowInstance.SetColor(s_Colour, glowColour);
        watch.glowInstance.SetFloat(s_Intensity, glowIntensity * watch.glow * shape);
        watch.glowInstance.SetFloat(s_Size, glowSize * (watch.WindowOpen ? 1.15f : 1f));

        Place(watch.leftGlow, watch.leftAnchor, watch.leftOffset);
        Place(watch.rightGlow, watch.rightAnchor, watch.rightOffset);
    }

    static void Place(GameObject glow, Transform anchor, Vector3 offset)
    {
        if (glow == null) return;
        if (anchor == null) { glow.SetActive(false); return; }
        glow.SetActive(true);
        glow.transform.position = anchor.TransformPoint(offset);
    }

    static bool TryEyeCentre(Watch watch, out Vector3 centre)
    {
        bool left = watch.leftAnchor != null, right = watch.rightAnchor != null;
        if (left && right)
        {
            centre = (watch.leftAnchor.TransformPoint(watch.leftOffset) + watch.rightAnchor.TransformPoint(watch.rightOffset)) * 0.5f;
            return true;
        }
        if (left) { centre = watch.leftAnchor.TransformPoint(watch.leftOffset); return true; }
        if (right) { centre = watch.rightAnchor.TransformPoint(watch.rightOffset); return true; }
        centre = default;
        return false;
    }

    /// <summary>
    /// Eye naming differs per rig, and the obvious matches are traps: EyeSurface_Left and
    /// friends are SkinnedMeshRenderer hosts that usually sit at the model root, nowhere
    /// near the face. Search the head bone's own subtree, skip anything carrying a
    /// renderer, and sanity check the result against the head before trusting it.
    /// </summary>
    void ResolveEyes(Watch watch)
    {
        if (watch.eyesResolved) return;
        watch.eyesResolved = true;
        var calibrated=watch.driver.GetComponent<EnemyEyeAnchors>();
        if(calibrated!=null&&calibrated.left!=null&&calibrated.right!=null)
        {
            watch.leftAnchor=calibrated.left;watch.rightAnchor=calibrated.right;
            watch.leftOffset=calibrated.leftOffset;watch.rightOffset=calibrated.rightOffset;
            return;
        }

        Transform head = watch.driver.animator != null
            ? watch.driver.animator.GetBoneTransform(HumanBodyBones.Head)
            : null;

        watch.leftAnchor=watch.driver.animator!=null?watch.driver.animator.GetBoneTransform(HumanBodyBones.LeftEye):null;
        watch.rightAnchor=watch.driver.animator!=null?watch.driver.animator.GetBoneTransform(HumanBodyBones.RightEye):null;
        Transform scope = head != null ? head : watch.driver.transform;
        float reach = head != null ? 0.35f : float.PositiveInfinity;

        foreach (var child in scope.GetComponentsInChildren<Transform>(true))
        {
            if (child == scope) continue;
            string name = child.name;
            if (name.IndexOf("eye", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            // Renderer hosts are meshes, not bones.
            if (child.GetComponent<Renderer>() != null) continue;
            // A bone that is nowhere near the head is not an eye.
            if (head != null && Vector3.Distance(child.position, head.position) > reach) continue;

            bool isLeft = name.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isRight = name.IndexOf("right", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isLeft && watch.leftAnchor == null) watch.leftAnchor = child;
            else if (isRight && watch.rightAnchor == null) watch.rightAnchor = child;
        }

        if (head != null)
        {
            // Roughly where eyes sit on a humanoid head, in the head bone's own space.
            if (watch.leftAnchor == null) { watch.leftAnchor = head; watch.leftOffset = new Vector3(-0.035f, 0.07f, 0.075f); }
            if (watch.rightAnchor == null) { watch.rightAnchor = head; watch.rightOffset = new Vector3(0.035f, 0.07f, 0.075f); }
        }

        if (logEyeResolution)
        {
            Debug.Log($"[EnemyTellDirector] {watch.driver.name}: head={(head != null ? head.name : "none")} " +
                      $"left={(watch.leftAnchor != null ? watch.leftAnchor.name : "none")} " +
                      $"right={(watch.rightAnchor != null ? watch.rightAnchor.name : "none")}", watch.driver);
        }
    }

    void EnsureCross(Watch watch)
    {
        if (watch.cross != null) return;

        var shader = crossMaterial != null ? crossMaterial.shader : Shader.Find("Stylized/Enemy Eye Cross");
        if (shader == null) return;

        watch.crossInstance = crossMaterial != null ? new Material(crossMaterial) : new Material(shader);
        watch.crossInstance.hideFlags = HideFlags.HideAndDontSave;

        watch.cross = NewQuad("Enemy Eye Cross", watch.crossInstance);
        if (outputCamera != null) watch.cross.transform.SetParent(outputCamera.transform, false);
    }

    void EnsureGlow(Watch watch)
    {
        if (watch.glowInstance != null) return;

        var shader = glowMaterial != null ? glowMaterial.shader : Shader.Find("Stylized/Enemy Eye Glow");
        if (shader == null) return;

        watch.glowInstance = glowMaterial != null ? new Material(glowMaterial) : new Material(shader);
        watch.glowInstance.hideFlags = HideFlags.HideAndDontSave;

        watch.leftGlow = NewQuad("Enemy Eye Glow L", watch.glowInstance);
        watch.rightGlow = NewQuad("Enemy Eye Glow R", watch.glowInstance);
    }

    GameObject NewQuad(string name, Material material)
    {
        var go = new GameObject(name) { hideFlags = showRuntimeObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave };
        go.AddComponent<MeshFilter>().sharedMesh = Quad();

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        go.SetActive(false);
        return go;
    }

    Mesh Quad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Eye Tell Quad", hideFlags = HideFlags.HideAndDontSave };
        quad.SetVertices(new List<Vector3>
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),   new Vector3(-0.5f, 0.5f, 0f)
        });
        quad.SetUVs(0, new List<Vector2>
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        });
        quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        // Never let frustum culling drop a quad that ignores its own transform.
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
        return quad;
    }

    static void Dispose(Watch watch)
    {
        if (watch.cross != null) CoreUtils.Destroy(watch.cross);
        if (watch.leftGlow != null) CoreUtils.Destroy(watch.leftGlow);
        if (watch.rightGlow != null) CoreUtils.Destroy(watch.rightGlow);
        if (watch.crossInstance != null) CoreUtils.Destroy(watch.crossInstance);
        if (watch.glowInstance != null) CoreUtils.Destroy(watch.glowInstance);
        watch.cross = watch.leftGlow = watch.rightGlow = null;
        watch.crossInstance = watch.glowInstance = null;
    }
}



