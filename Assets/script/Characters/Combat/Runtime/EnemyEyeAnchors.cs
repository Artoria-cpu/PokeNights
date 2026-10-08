using UnityEngine;

/// <summary>Eye-surface centres calibrated from each character's rendered geometry.</summary>
[DisallowMultipleComponent]
public sealed class EnemyEyeAnchors : MonoBehaviour
{
    public Transform left,right;
    public Vector3 leftOffset,rightOffset;
    public Vector3 Centre => (left.TransformPoint(leftOffset)+right.TransformPoint(rightOffset))*.5f;
}
