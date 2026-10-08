using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A pool of frozen character copies left behind during a successful dodge.
///
/// Each copy is a real snapshot, not a trick: every SkinnedMeshRenderer is baked with
/// <see cref="SkinnedMeshRenderer.BakeMesh(Mesh, bool)"/>, which writes the current skinned
/// pose into a static mesh. The copies are parented to nothing, so they stay exactly where
/// the character was while the character keeps moving.
///
/// Not a MonoBehaviour - <see cref="DodgeSuccessDirector"/> owns one and drives it.
/// </summary>
public sealed class DodgeAfterimages
{
    sealed class Ghost
    {
        public GameObject root;
        public Material material;
        public Color colour;
        public readonly List<Mesh> baked = new List<Mesh>();
    }

    static readonly int s_GhostColour = Shader.PropertyToID("_GhostColour");

    readonly List<Ghost> m_Ghosts = new List<Ghost>();

    public int Count => m_Ghosts.Count;

    public void Spawn(CharacterMotor driver, Material source, Color colour)
    {
        if (driver == null || source == null) return;

        var ghost = new Ghost
        {
            root = new GameObject("Dodge Afterimage") { hideFlags = HideFlags.HideAndDontSave },
            material = new Material(source) { hideFlags = HideFlags.HideAndDontSave },
            colour = colour
        };

        foreach (var skinned in driver.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (skinned == null || !skinned.enabled || skinned.sharedMesh == null) continue;

            // Freeze the pose exactly as it is this frame.
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            skinned.BakeMesh(mesh, true);
            ghost.baked.Add(mesh);

            // BakeMesh already applied the renderer's scale, so the piece sits at unit scale.
            AddPiece(ghost, mesh, skinned.transform.position, skinned.transform.rotation, Vector3.one);
        }

        foreach (var filter in driver.GetComponentsInChildren<MeshFilter>(false))
        {
            if (filter == null || filter.sharedMesh == null) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) continue;

            // Rigid pieces - a drawn sword, for instance - are reused rather than baked.
            AddPiece(ghost, filter.sharedMesh, filter.transform.position, filter.transform.rotation, filter.transform.lossyScale);
        }

        if (ghost.root.transform.childCount == 0)
        {
            Dispose(ghost);
            return;
        }

        m_Ghosts.Add(ghost);
    }

    static void AddPiece(Ghost ghost, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var piece = new GameObject(mesh.name) { hideFlags = HideFlags.HideAndDontSave };
        piece.transform.SetParent(ghost.root.transform, false);
        piece.transform.SetPositionAndRotation(position, rotation);
        piece.transform.localScale = scale;

        piece.AddComponent<MeshFilter>().sharedMesh = mesh;

        var renderer = piece.AddComponent<MeshRenderer>();
        int subMeshes = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[subMeshes];
        for (int i = 0; i < subMeshes; i++) materials[i] = ghost.material;
        renderer.sharedMaterials = materials;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    /// <summary>Drives every copy's transparency together, so they lift as one.</summary>
    public void SetAlpha(float alpha)
    {
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        for (int i = 0; i < m_Ghosts.Count; i++)
        {
            var ghost = m_Ghosts[i];
            if (ghost.material == null) continue;

            // Set as a vector, not a colour, so the conversion is explicit rather than
            // depending on how the property type is interpreted.
            Color rgb = linear ? ghost.colour.linear : ghost.colour;
            ghost.material.SetVector(s_GhostColour, new Vector4(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha)));
        }
    }

    public void Clear()
    {
        for (int i = 0; i < m_Ghosts.Count; i++) Dispose(m_Ghosts[i]);
        m_Ghosts.Clear();
    }

    static void Dispose(Ghost ghost)
    {
        for (int i = 0; i < ghost.baked.Count; i++)
            if (ghost.baked[i] != null) CoreUtils.Destroy(ghost.baked[i]);
        ghost.baked.Clear();

        if (ghost.root != null) CoreUtils.Destroy(ghost.root);
        if (ghost.material != null) CoreUtils.Destroy(ghost.material);
    }
}
