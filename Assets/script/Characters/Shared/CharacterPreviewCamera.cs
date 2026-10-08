using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Small orbit/follow camera for the independent Redhorn preview scene.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(11000)]
public sealed class CharacterPreviewCamera : MonoBehaviour
{
    public Transform target;
    [Tooltip("Optional preview controller. Its Movement Camera is wired to this Camera.")]
    public CharacterMotor previewDriver;
    [Tooltip("Offset in target local coordinates; normally aim at the chest.")]
    public Vector3 targetOffset = new Vector3(0f, 1f, 0f);
    [Min(0.1f)] public float distance = 3.2f;
    [Min(0.1f)] public float minimumDistance = 1.2f;
    [Min(0.2f)] public float maximumDistance = 6f;
    public float yaw = 180f;
    [Range(-80f, 80f)] public float pitch = 12f;
    [Range(-80f, 80f)] public float minimumPitch = -10f;
    [Range(-80f, 80f)] public float maximumPitch = 65f;
    [Min(0f)] public float orbitSensitivity = 0.2f;
    [Min(0f)] public float zoomStep = 0.3f;
    [Min(0f)] public float followSpeed = 14f;
    [Min(0.1f)] public float teleportDistance = 3f;

    private Vector3 pivot;
    private Vector2 previousPointer;
    private bool previousOrbitHeld;
    private Transform cachedTarget;
    private Camera ownCamera;

    private void OnEnable()
    {
        ownCamera = GetComponent<Camera>();
        if (target == null && previewDriver != null) target = previewDriver.transform;
        BindDriver();
        SnapToTarget();
    }

    private void BindDriver()
    {
        if (ownCamera == null) ownCamera = GetComponent<Camera>();
        if (previewDriver == null && target != null) previewDriver = target.GetComponentInParent<CharacterMotor>();
        if (previewDriver != null) previewDriver.movementCamera = ownCamera;
    }

    public void SetTarget(Transform followTarget)
    {
        target = followTarget;
        BindDriver();
        SnapToTarget();
    }

    [ContextMenu("Snap camera to target")]
    public void SnapToTarget()
    {
        cachedTarget = target;
        previousOrbitHeld = false;
        if (target == null) return;
        pivot = target.TransformPoint(targetOffset);
        ApplyPose();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (cachedTarget != target)
        {
            BindDriver();
            SnapToTarget();
        }
        Vector2 pointer;
        bool orbitHeld;
        float scroll;
        if (ReadPointer(out pointer, out orbitHeld, out scroll))
        {
            if (orbitHeld && previousOrbitHeld)
            {
                Vector2 delta = pointer - previousPointer;
                yaw += delta.x * orbitSensitivity;
                pitch -= delta.y * orbitSensitivity;
            }
            previousPointer = pointer;
            previousOrbitHeld = orbitHeld;
            distance -= scroll * zoomStep;
        }
        else previousOrbitHeld = false;

        Vector3 desiredPivot = target.TransformPoint(targetOffset);
        if ((desiredPivot - pivot).sqrMagnitude > teleportDistance * teleportDistance)
            pivot = desiredPivot;
        else
            pivot = Vector3.Lerp(pivot, desiredPivot, 1f - Mathf.Exp(-Mathf.Max(0f, followSpeed) * Time.deltaTime));
        ApplyPose();
    }

    private void ApplyPose()
    {
        float near = Mathf.Max(0.1f, minimumDistance);
        distance = Mathf.Clamp(distance, near, Mathf.Max(near, maximumDistance));
        pitch = Mathf.Clamp(pitch, Mathf.Min(minimumPitch, maximumPitch), Mathf.Max(minimumPitch, maximumPitch));
        yaw = Mathf.Repeat(yaw, 360f);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
    }

    private static bool ReadPointer(out Vector2 pointer, out bool orbitHeld, out float scroll)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            pointer = Vector2.zero;
            orbitHeld = false;
            scroll = 0f;
            return false;
        }
        pointer = mouse.position.ReadValue();
        orbitHeld = mouse.rightButton.isPressed;
        scroll = mouse.scroll.ReadValue().y / 120f;
        return true;
#else
        pointer = Input.mousePosition;
        orbitHeld = Input.GetMouseButton(1);
        scroll = Input.mouseScrollDelta.y;
        return Input.mousePresent;
#endif
    }
}
