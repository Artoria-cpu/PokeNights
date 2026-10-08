using UnityEngine;

// Scene-local painted lighting: no surface normals, highlights or material instances.
[ExecuteAlways, DefaultExecutionOrder(11000)]
public sealed class ParkUnlitEnvironment : MonoBehaviour
{
    public Renderer[] surfaces = new Renderer[0];
    public Transform[] anchors = new Transform[0];
    public Light[] localLights = new Light[0];
    public Color ambientTint = new Color(.52f, .56f, .66f, 1);
    [Range(0, 1)] public float lampTintStrength = .22f;
    [Range(.1f, 10)] public float transitionSpeed = 4;
    Color[] current;
    MaterialPropertyBlock block;
    static readonly int Tint = Shader.PropertyToID("_EnvironmentTint");

    void Start() => CollectSurfaces();

    // Spawners finish in Awake, so include their new enemies before the first rendered frame.
    public void CollectSurfaces()
    {
        var found = new System.Collections.Generic.List<Renderer>();
        var origins = new System.Collections.Generic.List<Transform>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!System.Array.Exists(renderer.sharedMaterials, m => m && m.HasProperty(Tint))) continue;
            found.Add(renderer);
            var actor = renderer.GetComponentInParent<CharacterMotor>();
            origins.Add(actor ? actor.transform : null);
        }
        surfaces = found.ToArray();
        anchors = origins.ToArray();
        Refresh(true);
    }

    public Color Sample(Vector3 position, int layer)
    {
        float weight = 0;
        Color sum = Color.black;
        foreach (var light in localLights)
        {
            if (!light || !light.isActiveAndEnabled || light.type != LightType.Point ||
                light.intensity <= 0 || (light.cullingMask & (1 << layer)) == 0) continue;
            float distance = Vector3.Distance(position, light.transform.position);
            float falloff = Mathf.Clamp01(1 - distance / Mathf.Max(.01f, light.range));
            float w = falloff * falloff * light.intensity * .35f;
            weight += w;
            sum += light.color * w;
        }
        Color lamp = weight > .0001f ? sum / weight : Color.white;
        lamp = Color.Lerp(Color.white, lamp, lampTintStrength);
        Color result = Color.Lerp(ambientTint, lamp, 1 - Mathf.Exp(-weight));
        result.a = 1;
        return result;
    }

    void LateUpdate() => Refresh(!Application.isPlaying);

    public void Refresh(bool immediate)
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (current == null || current.Length != surfaces.Length)
        { current = new Color[surfaces.Length]; immediate = true; }
        float blend = immediate ? 1 : 1 - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        for (int i = 0; i < surfaces.Length; i++)
        {
            var r = surfaces[i];
            if (!r) continue;
            var anchor = i < anchors.Length ? anchors[i] : null;
            Vector3 position = anchor ? anchor.position + Vector3.up : r.bounds.center;
            current[i] = Color.Lerp(current[i], Sample(position, r.gameObject.layer), blend);
            // Preserve eye-gaze and other independently controlled shader properties.
            r.GetPropertyBlock(block);
            block.SetVector(Tint, (Vector4)current[i]);
            r.SetPropertyBlock(block);
        }
    }

    void OnDisable()
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in surfaces)
        {
            if (!r) continue;
            r.GetPropertyBlock(block);
            block.SetVector(Tint, Vector4.one);
            r.SetPropertyBlock(block);
        }
    }
}
