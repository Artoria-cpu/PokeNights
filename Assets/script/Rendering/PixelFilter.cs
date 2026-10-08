using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class PixelFilter : MonoBehaviour
{
    [Tooltip("Fixed vertical pixel count. Lower values give larger, coarser pixels regardless of window size.")]
    [Range(144, 720)] public int referenceHeight = 360;
    [Tooltip("Subtle colour grouping, applied in display colour space to preserve dark tones.")]
    [Range(8, 64)] public int colourLevels = 32;
    [Range(0, 1)] public float colourStrength = .65f;
}
