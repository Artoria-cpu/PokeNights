using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(9000)]
public sealed class CharacterMotionSupport : MonoBehaviour
{
    public Transform model;
    public CharacterMotor driver;
    public SkinnedMeshRenderer body;
    public bool randomGaze = true;
    public float horizontalGaze = 0.012f;
    public float verticalGaze = 0.0032f;
    public Vector2 holdSeconds = new Vector2(1.0f, 2.6f);
    public Vector2 CurrentGaze { get; private set; }
    public int GazeChanges { get; private set; }
    private readonly RaycastHit[] hits = new RaycastHit[12];
    private MaterialPropertyBlock block;
    private Vector2 gazeStart, gazeTarget;
    private float nextGaze, gazeStartTime, modelOffset;
    private System.Random random;
    private Mesh cachedMesh;
    private Vector3[] solePoints, dodgeContactPoints;
    private BoneWeight[] dodgeContactWeights;
    private BoneWeight[] soleWeights;
    private Vector3[] backContactPoints;
    private BoneWeight[] backContactWeights;
    private Matrix4x4[] bindMatrices, skinMatrices;
    private float floorY;
    private static readonly int GazeOffset = Shader.PropertyToID("_GazeOffset");

    private void OnEnable()
    {
        if (driver == null) driver = GetComponent<CharacterMotor>();
        if (model == null && driver != null && driver.animator != null) model = driver.animator.transform;
        block = new MaterialPropertyBlock();
        random = new System.Random(GetInstanceID());
        nextGaze = Time.time + .7f;
        gazeStartTime = Time.time;
        gazeStart = gazeTarget = CurrentGaze = Vector2.zero;
        modelOffset = model != null ? model.localPosition.y : 0f;
    }

    private void LateUpdate()
    {
        if (model == null || driver == null) return;
        if (block == null || random == null) OnEnable();
        if (driver.IsGrounded)
        {
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .35f, Vector3.down, hits, .7f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(transform) || hits[i].normal.y < .5f) continue;
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    floorY = hits[i].point.y;
                    modelOffset = Mathf.Clamp(floorY - transform.position.y, -.10f, .10f);
                }
            }
        }
        Vector3 local = model.localPosition;
        local.y = modelOffset;
        model.localPosition = local;
        if (body == null) return;
        if (driver.IsGrounded || transform.position.y - floorY < .35f) PreventSolePenetration();
        if (randomGaze && Time.time >= nextGaze)
        {
            gazeStart = CurrentGaze;
            float range = driver.PlanarSpeed > .2f ? .75f : 1f;
            float side = GazeChanges % 2 == 0 ? 1f : -1f;
            gazeTarget = random.NextDouble() < .2 ? Vector2.zero : new Vector2(
                side * Mathf.Lerp(.6f, 1f, (float)random.NextDouble()) * horizontalGaze * range,
                ((float)random.NextDouble() * 2f - 1f) * verticalGaze * range);
            gazeStartTime = Time.time;
            nextGaze = Time.time + Mathf.Lerp(holdSeconds.x, holdSeconds.y, (float)random.NextDouble());
            GazeChanges++;
        }
        float t = Mathf.SmoothStep(0f, 1f, (Time.time - gazeStartTime) / .11f);
        CurrentGaze = randomGaze ? Vector2.Lerp(gazeStart, gazeTarget, t) : Vector2.zero;
        body.GetPropertyBlock(block);
        block.SetVector(GazeOffset, new Vector4(CurrentGaze.x, CurrentGaze.y, 0f, 0f));
        body.SetPropertyBlock(block);
    }

    private void PreventSolePenetration()
    {
        if (body.sharedMesh != cachedMesh || solePoints == null || soleWeights == null || skinMatrices == null || bindMatrices == null)
        {
            cachedMesh = body.sharedMesh;
            var vertices = cachedMesh.vertices;
            var weights = cachedMesh.boneWeights;
            dodgeContactPoints = vertices; dodgeContactWeights = weights;
            var points = new System.Collections.Generic.List<Vector3>();
            var entries = new System.Collections.Generic.List<BoneWeight>();
            for (int i = 0; i < vertices.Length; i++) if (vertices[i].y < .116f) { points.Add(vertices[i]); entries.Add(weights[i]); }
            solePoints = points.ToArray(); soleWeights = entries.ToArray();
            points.Clear(); entries.Clear();
            var rigAnimator = driver.animator;
            var hips = rigAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var spine = rigAnimator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = rigAnimator.GetBoneTransform(HumanBodyBones.Chest);
            var contactBones = body.bones;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i]; var bone = contactBones[w.boneIndex0];
                if (w.weight0 > .5f && (bone == hips || bone == spine || bone == chest))
                { points.Add(vertices[i]); entries.Add(w); }
            }
            backContactPoints = points.ToArray(); backContactWeights = entries.ToArray();
            bindMatrices = cachedMesh.bindposes; skinMatrices = new Matrix4x4[bindMatrices.Length];
        }
        var bones = body.bones;
        for (int i = 0; i < skinMatrices.Length; i++) skinMatrices[i] = bones[i].localToWorldMatrix * bindMatrices[i];
        var activePoints = driver.IsDodging ? dodgeContactPoints : solePoints;
        var activeWeights = driver.IsDodging ? dodgeContactWeights : soleWeights;
        float lowest = LowestPoint(activePoints, activeWeights);
        float correction = Mathf.Clamp(floorY + .002f - lowest,
            !driver.IsDodging && driver.IsGrounded && driver.PlanarSpeed < (driver.walkSpeed + driver.runSpeed) * .5f ? -.06f : 0f,
            driver.IsDodging ? .6f : .25f);
        var animator = driver.animator;
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName(CharacterCombatStats.BackDeath) && backContactPoints.Length > 0)
        {
            // Once the fall lands, support the back rather than lifting the torso to plant the toes.
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .70f, state.normalizedTime));
            float backCorrection = Mathf.Clamp(floorY + .002f - LowestPoint(backContactPoints, backContactWeights), -.25f, .25f);
            correction = Mathf.Lerp(correction, backCorrection, settle);
        }
        Vector3 local = model.localPosition;
        local.y += correction;
        model.localPosition = local;
    }
    float LowestPoint(Vector3[] points, BoneWeight[] weights)
    {
        float lowest = float.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i]; var w = weights[i];
            float y = skinMatrices[w.boneIndex0].MultiplyPoint3x4(p).y * w.weight0;
            if (w.weight1 > 0f) y += skinMatrices[w.boneIndex1].MultiplyPoint3x4(p).y * w.weight1;
            if (w.weight2 > 0f) y += skinMatrices[w.boneIndex2].MultiplyPoint3x4(p).y * w.weight2;
            if (w.weight3 > 0f) y += skinMatrices[w.boneIndex3].MultiplyPoint3x4(p).y * w.weight3;
            lowest = Mathf.Min(lowest, y);
        }
        return lowest;
    }
    private void OnDisable()
    {
        if (body == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        body.GetPropertyBlock(block);
        block.SetVector(GazeOffset, Vector4.zero);
        body.SetPropertyBlock(block);
    }
}
