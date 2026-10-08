using UnityEngine;
[DisallowMultipleComponent, DefaultExecutionOrder(10030)]
public sealed class CharacterEyeGaze : MonoBehaviour
{
    public Renderer[] surfaces;
    public CharacterMotor driver;
    public float horizontalTravel = .018f, verticalTravel = .003f;
    [Range(.5f, 1f)] public float movingAmplitude = .9f;
    public Vector2 fixationSeconds = new Vector2(.85f, 1.6f);
    public bool automatic = true;
    public Vector2 previewDirection;
    public Vector2 CurrentOffset { get; private set; }
    public int GazeChanges { get; private set; }
    MaterialPropertyBlock block;
    Vector2 from, target;
    float start, next;
    System.Random random;
    static readonly int Offset = Shader.PropertyToID("_GazeOffset");
    static readonly float[] Directions = { -1, 0, 1, 0 };
    void OnEnable()
    {
        block = new MaterialPropertyBlock(); random = new System.Random(GetInstanceID());
        from = target = CurrentOffset = Vector2.zero; GazeChanges = 0; start = Time.time; next = Time.time + .3f;
    }
    void LateUpdate()
    {
        if (block == null || random == null) OnEnable();
        if (automatic && Time.time >= next)
        {
            from = CurrentOffset;
            float amplitude = driver != null && driver.PlanarSpeed > .2f ? movingAmplitude : 1;
            float horizontal = Directions[GazeChanges % Directions.Length];
            target = new Vector2(horizontal * horizontalTravel * amplitude,
                horizontal == 0 ? 0 : ((float)random.NextDouble() * 2 - 1) * verticalTravel);
            start = Time.time; next = start + Mathf.Lerp(fixationSeconds.x, fixationSeconds.y, (float)random.NextDouble()); GazeChanges++;
        }
        CurrentOffset = automatic ? Vector2.Lerp(from, target, Mathf.SmoothStep(0, 1, (Time.time - start) / .14f))
            : Vector2.Scale(previewDirection, new Vector2(horizontalTravel, verticalTravel));
        Apply(CurrentOffset);
    }
    void Apply(Vector2 offset)
    {
        if (surfaces == null) return; if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in surfaces) { if (r == null) continue; r.GetPropertyBlock(block); block.SetVector(Offset, new Vector4(offset.x, offset.y, 0, 0)); r.SetPropertyBlock(block); }
    }
    void OnDisable() { CurrentOffset = Vector2.zero; Apply(Vector2.zero); }
}
