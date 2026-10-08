using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterRigDemo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterLocomotionController : MonoBehaviour
    {
        public enum MovementMode { Walk, Run }
        [Header("References")]
        public Animator animator;
        public Transform viewTransform;
        [Header("Movement")]
        public float walkSpeed = 1.25f;
        public float runSpeed = 2.8f;
        public float acceleration = 10f;
        public float rotationSharpness = 14f;
        public MovementMode movementMode = MovementMode.Walk;
        [Header("Jump")]
        public float jumpHeight = 1.1f;
        public float gravity = -24f;
        public float coyoteTime = 0.1f;
        public float jumpBufferTime = 0.12f;
        [Range(0f, 0.2f)] public float jumpAnticipation = 7f / 60f;

        public bool Grounded { get; private set; }
        public float Speed { get; private set; }
        public float VerticalSpeed { get; private set; }
        public string StateName => !Grounded ? (VerticalSpeed > 0 ? "Jump" : "Fall") : Speed < 0.08f ? "Idle" : Speed > (walkSpeed + runSpeed) * 0.5f ? "Run" : "Walk";
        CharacterController capsule;
        Vector3 horizontalVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpRequestTime = float.NegativeInfinity;
        float groundSuppressionUntil;
        bool jumpConsumed;
        bool preparingJump;
        float launchAt;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalId = Animator.StringToHash("VerticalSpeed");
        static readonly int JumpId = Animator.StringToHash("Jump");
        static readonly int PrepareId = Animator.StringToHash("JumpPrepare");

        void Awake()
        {
            capsule = GetComponent<CharacterController>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!viewTransform && Camera.main) viewTransform = Camera.main.transform;
            if (animator) animator.applyRootMotion = false;
        }

        public void RequestJump() => lastJumpRequestTime = Time.time;
        public void SetMovementMode(bool run) => movementMode = run ? MovementMode.Run : MovementMode.Walk;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0 || dt > 0.2f) return;
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            Vector2 input = Vector2.zero;
            bool sprint = false;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                if (keyboard.digit1Key.wasPressedThisFrame) movementMode = MovementMode.Walk;
                if (keyboard.digit2Key.wasPressedThisFrame) movementMode = MovementMode.Run;
                if (keyboard.spaceKey.wasPressedThisFrame) RequestJump();
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
                sprint |= pad.rightShoulder.isPressed;
                if (pad.buttonSouth.wasPressedThisFrame) RequestJump();
            }
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 forward = viewTransform ? Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = forward * input.y + right * input.x;
            float targetSpeed = sprint || movementMode == MovementMode.Run ? runSpeed : walkSpeed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish * targetSpeed, acceleration * dt);
            if (wish.sqrMagnitude > 0.005f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), 1 - Mathf.Exp(-rotationSharpness * dt));

            bool onGround = capsule.isGrounded && Time.time >= groundSuppressionUntil;
            if (onGround)
            {
                lastGroundedTime = Time.time;
                if (!preparingJump) jumpConsumed = false;
                if (VerticalSpeed < 0) VerticalSpeed = -2f;
            }
            if (!jumpConsumed && Time.time - lastJumpRequestTime <= jumpBufferTime && Time.time - lastGroundedTime <= coyoteTime)
            {
                jumpConsumed = true;
                lastJumpRequestTime = float.NegativeInfinity;
                if (onGround && jumpAnticipation > 0f)
                {
                    preparingJump = true;
                    launchAt = Time.time + jumpAnticipation;
                    if (animator) animator.SetTrigger(PrepareId);
                }
                else LaunchJump();
            }
            if (preparingJump && Time.time >= launchAt)
            {
                if (onGround || Time.time - lastGroundedTime <= coyoteTime) LaunchJump();
                else preparingJump = false;
            }
            VerticalSpeed += gravity * dt;
            Vector3 before = transform.position;
            CollisionFlags hit = capsule.Move((horizontalVelocity + Vector3.up * VerticalSpeed) * dt);
            if ((hit & CollisionFlags.Above) != 0 && VerticalSpeed > 0) VerticalSpeed = 0;
            Grounded = (capsule.isGrounded || (hit & CollisionFlags.Below) != 0) && Time.time >= groundSuppressionUntil;
            Speed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / dt;
            if (animator)
            {
                animator.SetFloat(SpeedId, Speed, 0.08f, dt);
                animator.SetBool(GroundedId, Grounded);
                animator.SetFloat(VerticalId, VerticalSpeed);
            }
        }

        void LaunchJump()
        {
            preparingJump = false;
            VerticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            groundSuppressionUntil = Time.time + 0.12f;
            if (animator) { animator.ResetTrigger(PrepareId); animator.SetTrigger(JumpId); }
        }

        void OnDisable()
        {
            horizontalVelocity = Vector3.zero;
            lastJumpRequestTime = float.NegativeInfinity;
            preparingJump = false;
        }
    }
}
