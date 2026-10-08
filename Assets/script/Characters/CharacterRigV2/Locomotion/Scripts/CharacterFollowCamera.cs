using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterRigDemo
{
    public sealed class CharacterFollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 3.5f;
        public float targetHeight = 1.05f;
        public float yaw = 180f;
        public float pitch = 12f;
        public float sensitivity = 0.15f;
        public float followSharpness = 16f;
        Vector3 pivot;
        bool initialized;

        void LateUpdate()
        {
            if (!target) return;
            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.isPressed)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    yaw += delta.x * sensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -10, 65);
                }
                distance = Mathf.Clamp(distance - Mouse.current.scroll.ReadValue().y * 0.0015f, 1.8f, 6f);
            }
            if (Gamepad.current != null)
            {
                Vector2 look = Gamepad.current.rightStick.ReadValue();
                yaw += look.x * 130f * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - look.y * 90f * Time.deltaTime, -10, 65);
            }
            Vector3 desiredPivot = target.position + Vector3.up * targetHeight;
            if (!initialized) { pivot = desiredPivot; initialized = true; }
            pivot = Vector3.Lerp(pivot, desiredPivot, 1 - Mathf.Exp(-followSharpness * Time.deltaTime));
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 offset = rotation * Vector3.back;
            float actualDistance = distance;
            if (Physics.SphereCast(pivot, 0.15f, offset, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(target)) actualDistance = Mathf.Max(0.4f, hit.distance - 0.1f);
            }
            transform.SetPositionAndRotation(pivot + offset * actualDistance, rotation);
        }
    }
}
