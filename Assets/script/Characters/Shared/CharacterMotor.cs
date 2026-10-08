using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Optional controller for a separate character preview scene; does not replace the game's player.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class CharacterMotor : MonoBehaviour
{
    CharacterAnimationDirector animationDirector;
    public CharacterAnimationDirector Animation => animationDirector ?? (animationDirector = new CharacterAnimationDirector(this));

    [Header("Movement")]
    public bool acceptPlayerInput = true;
    public bool AIControlled { get; private set; }
    Vector3 aiMovement;
    bool aiSprint;
    bool aiDodgeRequested;
    [System.NonSerialized] public bool equipmentBusy;
    public Camera movementCamera;
    [Tooltip("Optional visual object to turn. Leave empty to turn the controller's entire GameObject.")]
    public Transform visualRoot;
    [System.NonSerialized] public Transform combatLookTarget;
    public bool FaceLockTarget {get;private set;}
    [Min(1)] public int slideJumpEndFrames = 5;
    public int SlideJumpCount { get; private set; }
    [Min(0f)] public float walkSpeed = 1.8f;
    [Min(0f)] public float runSpeed = 3.4f;
    [Range(.1f,1f)] public float aiRunSpeedScale = .85f;
    [Min(0f)] public float acceleration = 12f;
    [Min(0f)] public float deceleration = 18f;
    [Min(0f)] public float turnSpeed = 720f;
    [Min(0f)] public float jumpHeight = 1.1f;
    public float gravity = -20f;
    [Min(1f)] public float terminalFallSpeed = 30f;
    [Range(0f, 0.3f)] public float coyoteTime = 0.1f;
    [Range(0f, 0.3f)] public float jumpBuffer = 0.12f;

    [Header("Turning")]
    public bool useTurnAnimations=true;
    [Min(.1f)] public float minimumRunTurnRadius=1.2f;
    public AnimationClip leftTurnClip,rightTurnClip,walkingTurn180Clip,runningTurn180Clip;
    public bool IsTurning {get;private set;}
    bool turningWhileRunning;
    public bool IsRunningTurn=>IsTurning&&turningWhileRunning;
    [Min(0)] public float runningTurnSlideDistance=.35f;
    Vector3 runTurnSlideDirection;
    float runTurnSlideElapsed,runTurnSlideDuration,runTurnSlideTravel;
    string turnState;
    Quaternion turnStartRotation,turnEndRotation;
    float nextTurnAllowed;
    [System.Serializable]
    public sealed class TurnHandoff
    {
        public string state;
        public float exitNormalizedTime, blendDuration, walkStartSeconds, runStartSeconds;
    }
    [HideInInspector] public TurnHandoff[] turnHandoffs;
    TurnHandoff activeTurnHandoff;
    bool turnBlendingOut;
    float turnExitElapsed, turnExitDuration, turnExitStartSpeed, turnExitSpeed;
    Quaternion turnExitStartRotation;
    bool smoothWalkHeading, walkHeadingUpdatedThisFrame;
    float walkTurnYawVelocity;

    [Header("Authored jump timing")]
    public bool useAuthoredJumpTiming;
    [Min(0f)] public float standingTakeoffDelay = 2f / 30f;
    [Min(0f)] public float runningTakeoffDelay = 3f / 30f;
    [Min(0f)] public float runningJumpHeight = .70f;
    public bool JumpPreparing { get; private set; }
    public bool RunningJump { get; private set; }
    private float takeoffTime;

    [Header("Idle and braking")]
    public bool useIdleAndRunStop;
    public float sadIdleDelay = 3f;
    public float runStopDistance = 1.1f;
    public float runStopDuration = .9f;
    [Range(0f,1f)] public float runStopRecoveryCancel = .35f;
    public bool SadIdle { get; private set; }
    public bool RunStopping { get; private set; }
    public float InactiveSeconds { get; private set; }
    private float lastActivity, stopStarted;
    private Vector3 stopSlideDirection;
    private float stopSlideDistance, stopSlideDuration, stopSlideElapsed, stopSlideTravel;
    private float lastRunInputTime = float.NegativeInfinity;

    [Header("Dodge")]
    public bool useDodge;
    [Range(.1f, 2f)] public float dodgeTravelScale = .8f;
    public bool IsDodging { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsIncapacitated { get; private set; }
    public bool IsBallThrowing { get; private set; }
    int ballInputConsumedFrame=-1;
    public bool BallInputBlocked=>IsBallThrowing||ballInputConsumedFrame==Time.frameCount;
    public void SetBallThrowActive(bool active)
    {
        IsBallThrowing=active;ballInputConsumedFrame=Time.frameCount;
        if(!active)return;
        CancelTurn(false);RunStopping=JumpPreparing=SadIdle=false;
        recoveryAction=0;lastJumpPressedTime=lastRunInputTime=float.NegativeInfinity;
        planarVelocity=Vector3.ClampMagnitude(planarVelocity,walkSpeed);
        ResetIdleTimer();
        if(animator!=null)Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.1f,0,0);
    }
    public const string HitPoseHeldParameter="HitPoseHeld",BaseActionSpeedParameter="BaseActionSpeed";
    bool holdingHitPose;
    CharacterPoseTransitions poseTransitions;
    float hitStunUntil=float.NegativeInfinity;
    public bool IsHitStunned=>Time.time<hitStunUntil;
    public bool IsActionBlocked=>IsIncapacitated||IsHitStunned;
    public bool HasPlayerMovementInput
    {
        get{ReadMovementInput(out var movement,out _,out _);return movement.sqrMagnitude>.001f;}
    }
    public bool IsRunning => (acceptPlayerInput||AIControlled) && IsGrounded && !IsAttacking && !IsDodging && !RunStopping && PlanarSpeed > walkSpeed + .25f;
    public bool RunningDodge { get; private set; }
    public int DodgeCount { get; private set; }
    [HideInInspector] public Vector3 sideDodgeMotionAxis=Vector3.left;
    private Vector3 dodgeDirection,dodgeMotionAxis=Vector3.forward;
    private bool dodgeLockedToTarget;
    private bool queuedRunTurnDodge;
    private string dodgeState;
    private int recoveryAction;

    [Header("Optional Animator")]
    public Animator animator;
    [Tooltip("Float, in metres per second. Only written if the parameter exists with this type.")]
    public string speedParameter = "Speed";
    public string groundedParameter = "IsGrounded";
    public string verticalSpeedParameter = "VerticalSpeed";

    [Header("Eyes")]

    public bool IsGrounded { get; private set; }
    public float VerticalSpeed { get { return verticalVelocity; } }
    public float PlanarSpeed { get { return planarVelocity.magnitude; } }

    private CharacterController controller;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();
    private RuntimeAnimatorController cachedAnimatorController;
    private Animator cachedAnimator;

    private void OnEnable()
    {
        controller = GetComponent<CharacterController>();
        poseTransitions=GetComponent<CharacterPoseTransitions>();
        hitStunUntil=float.NegativeInfinity;
        lastActivity = Time.time; SadIdle = RunStopping = false; lastRunInputTime = float.NegativeInfinity;
        if (movementCamera == null) movementCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        planarVelocity = Vector3.zero;
        JumpPreparing = false;
        verticalVelocity = -2f;
        lastGroundedTime = lastJumpPressedTime = float.NegativeInfinity;
        IsDodging = IsAttacking = IsTurning = false; RunningDodge=dodgeLockedToTarget=false; DodgeCount = 0; recoveryAction=0;nextTurnAllowed=0;
        queuedRunTurnDodge=false;
        smoothWalkHeading=false;walkTurnYawVelocity=0f;
        CacheAnimatorParameters();
        SetHitPoseHeld(false);
    }

    private void OnDisable()
    {
        SetHitPoseHeld(false);
        hitStunUntil=float.NegativeInfinity;
        planarVelocity = Vector3.zero;
        JumpPreparing = IsDodging = IsAttacking = IsTurning = false; RunningDodge=dodgeLockedToTarget=false; recoveryAction=0;
        queuedRunTurnDodge=false;
        verticalVelocity = 0f;
    }

    private void Update()
    {
        if (controller == null || !controller.enabled) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if(holdingHitPose&&!IsHitStunned)
        {
            SetHitPoseHeld(false);
            if(!IsIncapacitated)Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, IsGrounded?"Locomotion":"Fall",.18f,0,0);
        }
        if(IsActionBlocked)
        {
            planarVelocity=Vector3.zero;FaceLockTarget=false;
            float next=Mathf.Max(verticalVelocity-Mathf.Abs(gravity)*dt,-terminalFallSpeed);
            var hit=controller.Move(Vector3.up*(verticalVelocity+next)*(.5f*dt));
            verticalVelocity=next;IsGrounded=controller.isGrounded||(hit&CollisionFlags.Below)!=0;
            if(IsGrounded)verticalVelocity=-2;
            if(IsGrounded)lastGroundedTime=Time.time;
            // Preserve the blend-tree inputs as well as its time; zero Speed would replace a running pose with idle.
            if(!holdingHitPose)WriteAnimator(dt);
            return;
        }
        // Attack movement locks must always have a live owner. Recover if a skill or combo was
        // interrupted/disabled before it could clear the shared driver flag.
        if(IsAttacking)
        {
            bool owned=Animation.HasAttackOwner;
            if(!owned){SetAttackActive(false);animator.SetBool("IsAttacking",false);Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, IsGrounded?"Locomotion":"Fall",.18f,0,0);}
        }
        walkHeadingUpdatedThisFrame=false;
        if (movementCamera == null) movementCamera = Camera.main;

        Vector2 input;
        bool sprint;
        bool jump;
        ReadMovementInput(out input, out sprint, out jump);
        if(BallInputBlocked){sprint=false;jump=false;recoveryAction=0;lastJumpPressedTime=lastRunInputTime=float.NegativeInfinity;}
        Vector3 forward = movementCamera != null ? movementCamera.transform.forward : Vector3.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 desiredDirection = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
        if(AIControlled&&!acceptPlayerInput){desiredDirection=aiMovement;input=new Vector2(aiMovement.x,aiMovement.z);sprint=aiSprint;}
        // Running turns own displacement; input only chooses locomotion after their blend-out.
        bool recoveryJump=acceptPlayerInput&&recoveryAction==1;
        bool recoveryDodge=acceptPlayerInput&&recoveryAction==2;
        recoveryAction=0;jump|=recoveryJump;
        if(jump || !sprint || desiredDirection.sqrMagnitude<.001f || !acceptPlayerInput || BallInputBlocked)
            queuedRunTurnDodge=false;
        bool aiDodge=AIControlled&&!acceptPlayerInput&&aiDodgeRequested;
        aiDodgeRequested=false;
        bool dodgePressed=aiDodge||(acceptPlayerInput&&(recoveryDodge||(!recoveryJump&&DodgePressed())));
        if(BallInputBlocked)dodgePressed=false;
        ResolveRunTurnDodge(desiredDirection,sprint,ref dodgePressed);
        bool jumpFromTurn=IsTurning&&jump;
        bool jumpFromRunStop=RunStopping&&jump;
        if(jump||dodgePressed||!IsGrounded||!acceptPlayerInput)CancelTurn(false);
        bool finishingRun=ShouldStartRunStop(input.sqrMagnitude>.001f,sprint,jump);
        bool freeRun=IsRunningTurn||(sprint&&input.sqrMagnitude>.001f)||RunStopping||finishingRun||(IsDodging&&RunningDodge)||(!IsGrounded&&RunningJump);
        var orbDash=IsAttacking?GetComponent<OrbDashAttack>():null;
        bool orbDashActive=orbDash!=null&&orbDash.IsDashing;
        FaceLockTarget=IsBallThrowing||combatLookTarget!=null&&(IsDodging?dodgeLockedToTarget:(IsAttacking||!freeRun));
        if(orbDashActive)FaceLockTarget=false;
        if(IsAttacking&&GetComponent<CharacterParryAction>() is CharacterParryAction guarding&&guarding.OwnsBody)FaceLockTarget=false;
        var skill=IsAttacking?GetComponent<SwordThrowSkill>():null;
        var beam=IsAttacking?GetComponent<OrbBeamSkill>():null;
        bool skillOwnsFacing=skill!=null&&skill.OwnsBody;
        bool beamOwnsFacing=beam!=null&&beam.OwnsBody;
        if(FaceLockTarget||skillOwnsFacing||beamOwnsFacing)
        {
            if(IsTurning||smoothWalkHeading)CancelTurn(true);
            var attack=IsAttacking?GetComponent<CharacterComboAttack>():null;
            // Keep body, displacement and sword FX on the same heading for the entire strike.
            var facing=IsBallThrowing?forward:skillOwnsFacing?skill.ActionForward:beamOwnsFacing?beam.ActionForward:attack!=null&&attack.IsAttacking?attack.AttackForward:Vector3.ProjectOnPlane(combatLookTarget.position-transform.position,Vector3.up);
            if(facing.sqrMagnitude>.001f){var body=visualRoot!=null?visualRoot:transform;body.rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(facing),turnSpeed*dt);}
        }
        if (IsAttacking)
        {
            if(orbDashActive&&orbDash.TickMotion(dt,desiredDirection))
            {WriteAnimator(dt);return;}
            // Ground attacks own the full body and facing; the capsule still follows gravity.
            lastActivity = Time.time; InactiveSeconds = 0;
            planarVelocity = Vector3.zero; SadIdle = RunStopping = JumpPreparing = false;
            lastJumpPressedTime = lastRunInputTime = float.NegativeInfinity;
            float next = Mathf.Max(verticalVelocity - Mathf.Abs(gravity) * dt, -terminalFallSpeed);
            var hit = controller.Move(Vector3.up * (verticalVelocity + next) * (.5f * dt));
            verticalVelocity = next; IsGrounded = controller.isGrounded || (hit & CollisionFlags.Below) != 0;
            if (IsGrounded) { verticalVelocity = -2f; lastGroundedTime = Time.time; }
            WriteAnimator(dt);  return;
        }
        if (IsDodging)
        {
            lastActivity = Time.time; InactiveSeconds = 0;
            lastRunInputTime = float.NegativeInfinity;
            if(jump)lastJumpPressedTime=Time.time;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if(RunningDodge && dodgePressed && state.IsName(dodgeState) && state.normalizedTime>=.8f
                && CanQueueRunTurnDodge(desiredDirection,sprint))
                queuedRunTurnDodge=true;
            if(Time.time-lastJumpPressedTime<=jumpBuffer && TryJumpFromSlide())return;
            if (state.IsName(dodgeState) && state.normalizedTime >= 1f)
            {
                FinishDodge(desiredDirection,sprint,dt);
                if(queuedRunTurnDodge)dodgePressed=false;
            }
            else
            {
                WriteAnimator(dt); 
                return;
            }
        }
        // Camera look and unrelated keys do not reset character inactivity.
        bool activity = IsTurning || input.sqrMagnitude > .001f || jump || JumpPreparing || !IsGrounded || PlanarSpeed > .08f || RunStopping;
        if (activity) lastActivity = Time.time;
        InactiveSeconds = Time.time - lastActivity;
        if (jump) lastJumpPressedTime = Time.time;
        IsGrounded = controller.isGrounded;
        if (IsGrounded && verticalVelocity <= 0f)
        {
            lastGroundedTime = Time.time;
            verticalVelocity = -2f;
        }

        if ((acceptPlayerInput||AIControlled) && useDodge && dodgePressed && IsGrounded && !JumpPreparing && animator != null && animator.runtimeAnimatorController != null)
        {
            RunningDodge = !aiDodge && PlanarSpeed > walkSpeed + .25f && (sprint || Time.time - lastRunInputTime < .35f);
            dodgeState = RunningDodge ? "RunningDodge" : "Dodge";
            // Running dodges keep the original slide and heading; directional clips are for locked walking.
            dodgeLockedToTarget=combatLookTarget!=null&&!RunningDodge&&!sprint;dodgeMotionAxis=Vector3.forward;
            var body=visualRoot!=null?visualRoot:transform;
            Vector3 facing=dodgeLockedToTarget?Vector3.ProjectOnPlane(combatLookTarget.position-transform.position,Vector3.up):Vector3.ProjectOnPlane(body.forward,Vector3.up);
            if(facing.sqrMagnitude<.001f)facing=Vector3.ProjectOnPlane(body.forward,Vector3.up);
            facing.Normalize();
            dodgeDirection=desiredDirection.sqrMagnitude>.001f?desiredDirection.normalized:facing;
            if(dodgeLockedToTarget)
            {
                // Locked dodges use the four authored directions, including diagonal key combinations.
                Vector3 local=LockedDodgeDirection(Quaternion.Inverse(Quaternion.LookRotation(facing,Vector3.up))*dodgeDirection);
                dodgeDirection=Quaternion.LookRotation(facing,Vector3.up)*local;
                string directional=null;Vector3 motionAxis=Vector3.forward;
                if(Mathf.Abs(local.x)>Mathf.Abs(local.z))
                {
                    bool left=local.x<0;directional=left?"DodgeLeft":"DodgeRight";
                    motionAxis=sideDodgeMotionAxis;if(!left)motionAxis.x=-motionAxis.x;
                }
                else if(local.z<-.25f){directional="DodgeBack";motionAxis=Vector3.back;}
                if(directional!=null&&animator.HasState(0,Animator.StringToHash(directional)))
                {dodgeState=directional;dodgeMotionAxis=motionAxis;RunningDodge=false;}
            }
            if (animator.HasState(0, Animator.StringToHash(dodgeState)))
            {
                CancelTurn(false);IsDodging = true; DodgeCount++;
                FaceLockTarget=dodgeLockedToTarget;
                body.rotation=Quaternion.LookRotation(dodgeLockedToTarget?facing:dodgeDirection,Vector3.up);
                SadIdle = RunStopping = JumpPreparing = RunningJump = false;
                planarVelocity = Vector3.zero; verticalVelocity = -2f;
                lastJumpPressedTime = lastRunInputTime = float.NegativeInfinity;
                lastActivity = Time.time; InactiveSeconds = 0;
                WriteAnimator(dt);
                Animation.CrossFade(CharacterAnimationDirector.Action.Dodge, dodgeState, .04f, 0, 0f);
                return;
            }
        }
        float desiredSpeed = Mathf.Max(0f, sprint ? runSpeed : walkSpeed);
        if(sprint&&AIControlled&&!acceptPlayerInput)desiredSpeed*=aiRunSpeedScale;
        Vector3 desiredVelocity = desiredDirection * desiredSpeed;
        bool movingInput = desiredDirection.sqrMagnitude > .001f;
        bool walkFromRunStop=RunStopping && movingInput && !sprint && !jump && !JumpPreparing && RunStopCanRecover();
        if (RunStopping && ((sprint && movingInput) || walkFromRunStop || jump || JumpPreparing
            || (!IsGrounded && Time.time - lastGroundedTime > coyoteTime) || RunStopFinished()))
            RunStopping = false;
        if(walkFromRunStop)
        {
            WriteAnimator(dt);
            if(animator!=null&&animator.runtimeAnimatorController!=null)
                Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.12f,0,0f);
        }
        if (!RunStopping && !IsTurning && sprint && movingInput && PlanarSpeed > walkSpeed + .2f)
            lastRunInputTime = Time.time;
        if (ShouldStartRunStop(movingInput,sprint,jump))
        {
            RunStopping = true; SadIdle = false; stopStarted = Time.time;
            smoothWalkHeading=false;walkTurnYawVelocity=0f;
            lastRunInputTime = float.NegativeInfinity;
            stopSlideDirection = planarVelocity.normalized;
            stopSlideDistance = Mathf.Max(.05f,runStopDistance) * Mathf.Clamp01(PlanarSpeed / Mathf.Max(.01f,runSpeed));
            // Cubic distance easing starts at the incoming speed and gently reaches zero velocity.
            stopSlideDuration = 3f * stopSlideDistance / Mathf.Max(.1f,PlanarSpeed);
            stopSlideElapsed = stopSlideTravel = 0f;
            (visualRoot != null ? visualRoot : transform).rotation = Quaternion.LookRotation(stopSlideDirection, Vector3.up);
            // Enter braking before processing walk turns, even while a direction key stays held.
            WriteAnimator(dt);
            if (animator != null && animator.runtimeAnimatorController != null
                && animator.HasState(0,Animator.StringToHash("RunToStop")))
                Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "RunToStop",.10f,0,0f);
        }
        UpdateTurnAnimation(desiredDirection,sprint,jump,dt);
        float rate = movingInput ? acceleration : deceleration;
        bool steerRun=!FaceLockTarget&&movingInput&&IsGrounded&&(sprint||PlanarSpeed>walkSpeed+.25f);
        if (RunStopping)
        {
            // Integrate the braking curve in world space; turning cannot bend the residual slide.
            stopSlideElapsed += dt;
            float t = Mathf.Clamp01(stopSlideElapsed / Mathf.Max(.01f,stopSlideDuration));
            float remaining = 1f-t;
            float travel = stopSlideDistance * (1f-remaining*remaining*remaining);
            planarVelocity = stopSlideDirection * Mathf.Max(0f,travel-stopSlideTravel) / dt;
            stopSlideTravel = travel;
        }
        else if(IsTurning && turnBlendingOut)
        {
            float t=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(turnExitElapsed/turnExitDuration));
            float speed=Mathf.Lerp(turnExitStartSpeed,turnExitSpeed,t);
            // A running pivot exits along its committed heading, without steering around the target.
            Vector3 heading=turningWhileRunning?turnEndRotation*Vector3.forward:(visualRoot!=null?visualRoot:transform).forward;
            planarVelocity=heading*speed;
        }
        else if(IsRunningTurn)
        {
            // Fixed world direction from before the turn; rotating the body never bends this slide.
            runTurnSlideElapsed+=dt;
            float t=Mathf.Clamp01(runTurnSlideElapsed/Mathf.Max(.01f,runTurnSlideDuration));
            float travel=runningTurnSlideDistance*(1-(1-t)*(1-t));
            planarVelocity=runTurnSlideDirection*Mathf.Max(0,travel-runTurnSlideTravel)/dt;
            runTurnSlideTravel=travel;
        }
        else if(IsTurning)
        {
            // Keep the walk cadence and speed while the authored turn owns the heading.
            float speed=Mathf.MoveTowards(PlanarSpeed,movingInput?walkSpeed:0,Mathf.Max(0,rate)*dt);
            planarVelocity=(visualRoot!=null?visualRoot:transform).forward*speed;
        }
        else if(steerRun)
        {
            smoothWalkHeading=false;walkTurnYawVelocity=0f;
            // Limit the velocity heading itself, so the path obeys r=v/omega.
            float speed=Mathf.MoveTowards(PlanarSpeed,desiredSpeed,Mathf.Max(0,rate)*dt);
            Vector3 heading=PlanarSpeed>.05f?planarVelocity.normalized:Vector3.ProjectOnPlane((visualRoot!=null?visualRoot:transform).forward,Vector3.up).normalized;
            float angle=Vector3.SignedAngle(heading,desiredDirection,Vector3.up);
            float step=(PlanarSpeed+speed)*.5f/Mathf.Max(.1f,minimumRunTurnRadius)*Mathf.Rad2Deg*dt;
            heading=Quaternion.AngleAxis(Mathf.Clamp(angle,-step,step),Vector3.up)*heading;
            planarVelocity=heading*speed;
            if(speed>.01f)(visualRoot!=null?visualRoot:transform).rotation=Quaternion.LookRotation(heading,Vector3.up);
        }
        else if(smoothWalkHeading && !FaceLockTarget && IsGrounded)
        {
            // Keep the same angular velocity after the animation; never snap the leftover angle.
            if(movingInput)turnEndRotation=Quaternion.LookRotation(desiredDirection,Vector3.up);
            AdvanceWalkTurnHeading(dt);
            float speed=Mathf.MoveTowards(PlanarSpeed,movingInput?walkSpeed:0f,Mathf.Max(0,rate)*dt);
            planarVelocity=(visualRoot!=null?visualRoot:transform).forward*speed;
            if(Quaternion.Angle((visualRoot!=null?visualRoot:transform).rotation,turnEndRotation)<.5f && Mathf.Abs(walkTurnYawVelocity)<5f)
            {smoothWalkHeading=false;walkTurnYawVelocity=0f;}
        }
        else
        {
            planarVelocity=Vector3.MoveTowards(planarVelocity,desiredVelocity,Mathf.Max(0,rate)*dt);
            if(!FaceLockTarget&&!IsTurning&&movingInput)
            {
                var body=visualRoot!=null?visualRoot:transform;
                body.rotation=Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(desiredDirection),Mathf.Max(0,turnSpeed)*dt);
            }
        }

        float appliedGravity = -Mathf.Max(0.01f, Mathf.Abs(gravity));
        if (!JumpPreparing && Time.time - lastJumpPressedTime <= jumpBuffer && Time.time - lastGroundedTime <= coyoteTime)
        {
            RunningJump = sprint && PlanarSpeed > walkSpeed + .25f;
            lastJumpPressedTime = float.NegativeInfinity;
            if (useAuthoredJumpTiming)
            {
                JumpPreparing = true;
                var orb = GetComponent<OrbWeapon>();
                takeoffTime = Time.time + (orb != null && orb.IsEquipped ? (RunningJump ? .10f : .18f) : (RunningJump ? runningTakeoffDelay : standingTakeoffDelay));
            }
            else
            {
                verticalVelocity = Mathf.Sqrt(2f * -appliedGravity * Mathf.Max(0f, jumpHeight));
                lastGroundedTime = float.NegativeInfinity;
                IsGrounded = false;
            }
        }
        if (JumpPreparing && Time.time >= takeoffTime)
        {
            JumpPreparing = false;
            float height = RunningJump ? runningJumpHeight : jumpHeight;
            verticalVelocity = Mathf.Sqrt(2f * -appliedGravity * Mathf.Max(0f, height));
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        float nextVertical = Mathf.Max(verticalVelocity + appliedGravity * dt, -Mathf.Max(1f, terminalFallSpeed));
        Vector3 displacement = planarVelocity * dt
            + Vector3.up * ((verticalVelocity + nextVertical) * (0.5f * dt));
        CollisionFlags collisions = controller.Move(displacement);
        verticalVelocity = nextVertical;
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        IsGrounded = controller.isGrounded || (collisions & CollisionFlags.Below) != 0;
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
            lastGroundedTime = Time.time;
        }
        SadIdle = useIdleAndRunStop && InactiveSeconds >= sadIdleDelay && IsGrounded && PlanarSpeed < .08f && !RunStopping && !JumpPreparing && !IsTurning;
        WriteAnimator(dt);
        if((recoveryJump||jumpFromTurn||jumpFromRunStop)&&(JumpPreparing||!IsGrounded)&&animator!=null){string state=JumpPreparing?(RunningJump?"RunningPrepare":"Prepare"):(RunningJump?"RunningJump":"Jump");Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, state,.08f,0,0);}
        
    }

    bool ShouldStartRunStop(bool movingInput,bool sprint,bool jump)
    {
        if(IsBallThrowing||AIControlled&&!acceptPlayerInput)return false;
        return useIdleAndRunStop && !IsTurning && !RunStopping && !IsAttacking && !IsDodging
            && !JumpPreparing && !jump && (!sprint || !movingInput)
            && Time.time-lastRunInputTime<.35f && PlanarSpeed>.2f
            && (IsGrounded || Time.time-lastGroundedTime<coyoteTime);
    }

    bool RunStopCanRecover()
    {
        if(animator!=null&&animator.runtimeAnimatorController!=null)
        {
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            if(state.IsName("RunToStop"))return state.normalizedTime>=runStopRecoveryCancel;
        }
        return Time.time-stopStarted>=Mathf.Max(.1f,runStopDuration)*runStopRecoveryCancel;
    }

    bool RunStopFinished()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            var state = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("RunToStop")) return state.normalizedTime>=1f && stopSlideElapsed>=stopSlideDuration;
        }
        // Fallback for preview controllers that do not contain the braking animation.
        return Time.time-stopStarted>=Mathf.Max(.1f,Mathf.Max(runStopDuration,stopSlideDuration));
    }

    void UpdateTurnAnimation(Vector3 desired,bool sprint,bool jump,float dt)
    {
        if(animator==null||animator.runtimeAnimatorController==null)return;
        var body=visualRoot!=null?visualRoot:transform;
        if(IsTurning)
        {
            if(!turningWhileRunning && desired.sqrMagnitude>.001f)
            {
                // WA can become A after the turn starts: update the goal without resetting the rotation.
                if(turnState!="Turn_Walk180" && walkingTurn180Clip!=null
                    && Mathf.Abs(Vector3.SignedAngle(body.forward,desired,Vector3.up))>110f
                    && animator.HasState(0,Animator.StringToHash("Turn_Walk180")))
                {
                    CancelTurn(false);nextTurnAllowed=Time.time;
                    UpdateTurnAnimation(desired,sprint,jump,dt);return;
                }
                turnEndRotation=Quaternion.LookRotation(desired,Vector3.up);
            }
            if(turnBlendingOut)
            {
                turnExitElapsed+=dt;
                float progress=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(turnExitElapsed/turnExitDuration));
                if(turningWhileRunning)body.rotation=Quaternion.Slerp(turnExitStartRotation,turnEndRotation,progress);
                else AdvanceWalkTurnHeading(dt);
                if(turnExitElapsed>=turnExitDuration)CancelTurn(false,!turningWhileRunning);
                return;
            }
            var state=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0):animator.GetCurrentAnimatorStateInfo(0);
            if(!state.IsName(turnState)){CancelTurn(false);return;}
            float rotationEnd=turningWhileRunning?.82f:.76f;
            float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,rotationEnd,state.normalizedTime));
            if(turningWhileRunning)body.rotation=Quaternion.Slerp(turnStartRotation,turnEndRotation,t);
            else AdvanceWalkTurnHeading(dt);
            // The handoff is authored per clip so its source continues moving for the whole blend.
            float exitTime=activeTurnHandoff!=null?activeTurnHandoff.exitNormalizedTime:.68f;
            if(state.normalizedTime>=exitTime)BeginTurnHandoff(desired.sqrMagnitude>.001f,sprint);
            return;
        }
        if(!useTurnAnimations||!acceptPlayerInput||FaceLockTarget||!IsGrounded||RunStopping||JumpPreparing||jump||IsAttacking||IsDodging||Time.time<nextTurnAllowed||desired.sqrMagnitude<.001f)return;
        float angle=Vector3.SignedAngle(body.forward,desired,Vector3.up),amount=Mathf.Abs(angle);
        bool running=sprint&&PlanarSpeed>walkSpeed+.25f;
        AnimationClip clip;
        if(amount>110f){turnState=running?"Turn_Run180":"Turn_Walk180";clip=running?runningTurn180Clip:walkingTurn180Clip;}
        else if(amount>=35&&!running){turnState=angle>0?"Turn_Right":"Turn_Left";clip=angle>0?rightTurnClip:leftTurnClip;}
        else return;
        if(clip==null||!animator.HasState(0,Animator.StringToHash(turnState)))return;
        IsTurning=true;turningWhileRunning=running;turnBlendingOut=false;
        if(!smoothWalkHeading)walkTurnYawVelocity=0f;
        smoothWalkHeading=!running;
        activeTurnHandoff=null;
        if(turnHandoffs!=null)foreach(var handoff in turnHandoffs)
            if(handoff!=null&&handoff.state==turnState){activeTurnHandoff=handoff;break;}
        turnStartRotation=body.rotation;turnEndRotation=Quaternion.LookRotation(desired,Vector3.up);
        if(running)
        {
            runTurnSlideDirection=PlanarSpeed>.01f?planarVelocity.normalized:Vector3.ProjectOnPlane(body.forward,Vector3.up).normalized;
            runTurnSlideDuration=2*runningTurnSlideDistance/Mathf.Max(.1f,PlanarSpeed);
            runTurnSlideElapsed=runTurnSlideTravel=0;
            lastRunInputTime=float.NegativeInfinity;
        }
        RunStopping=SadIdle=false;lastActivity=Time.time;Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, turnState,running?.12f:.16f,0,0);
    }
    void BeginTurnHandoff(bool moving,bool sprint)
    {
        turnBlendingOut=true;turnExitElapsed=0f;
        turnExitDuration=activeTurnHandoff!=null?activeTurnHandoff.blendDuration:.24f;
        turnExitDuration=Mathf.Max(.05f,turnExitDuration);
        turnExitStartRotation=(visualRoot!=null?visualRoot:transform).rotation;
        turnExitStartSpeed=PlanarSpeed;
        turnExitSpeed=moving?(sprint?runSpeed:walkSpeed):0f;
        float start=activeTurnHandoff==null?0f:(sprint?activeTurnHandoff.runStartSeconds:activeTurnHandoff.walkStartSeconds);
        // Select the destination gait before crossfading, including exits from a stationary running pivot.
        int speedHash=Animator.StringToHash(speedParameter);
        if(floatParameters.Contains(speedHash))animator.SetFloat(speedHash,turnExitSpeed);
        Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",turnExitDuration,0,start);
    }

    void AdvanceWalkTurnHeading(float dt)
    {
        if(walkHeadingUpdatedThisFrame)return;
        walkHeadingUpdatedThisFrame=true;
        var body=visualRoot!=null?visualRoot:transform;
        float yaw=Mathf.SmoothDampAngle(body.eulerAngles.y,turnEndRotation.eulerAngles.y,
            ref walkTurnYawVelocity,.10f,Mathf.Min(220f,Mathf.Max(1f,turnSpeed)),dt);
        body.rotation=Quaternion.Euler(0f,yaw,0f);
    }

    void CancelTurn(bool returnToLocomotion,bool preserveWalkHeading=false)
    {
        if(!preserveWalkHeading){smoothWalkHeading=false;walkTurnYawVelocity=0f;}
        if(!IsTurning)return;
        float blend=turningWhileRunning?.14f:.20f;
        IsTurning=false;turnBlendingOut=false;nextTurnAllowed=Time.time+blend+.04f;
        if(returnToLocomotion&&animator!=null&&animator.isActiveAndEnabled)
            Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",blend,0,0);
    }

    public bool TryQueueRecoveryAction(bool jump)
    {
        if(!enabled||!acceptPlayerInput||!IsGrounded||controller==null||!controller.enabled||animator==null)return false;
        string state=jump?(useAuthoredJumpTiming?"Prepare":"Jump"):"Dodge";
        if((!jump&&!useDodge)||!animator.HasState(0,Animator.StringToHash(state)))return false;
        recoveryAction=jump?1:2;return true;
    }

    public bool TryJumpFromSlide()
    {
        if(!IsDodging||!RunningDodge||!IsGrounded||animator==null)return false;
        var state=animator.GetCurrentAnimatorStateInfo(0);
        var clips=animator.GetCurrentAnimatorClipInfo(0);
        if(!state.IsName("RunningDodge")||clips.Length==0)return false;
        var clip=clips[0].clip;
        float threshold=1f-slideJumpEndFrames/Mathf.Max(1f,clip.length*clip.frameRate);
        if(state.normalizedTime<threshold)return false;
        IsDodging=RunningDodge=dodgeLockedToTarget=false;JumpPreparing=false;RunningJump=true;RunStopping=SadIdle=false;
        verticalVelocity=Mathf.Sqrt(2f*Mathf.Abs(gravity)*runningJumpHeight);
        planarVelocity=dodgeDirection*runSpeed;IsGrounded=false;SlideJumpCount++;
        lastJumpPressedTime=lastGroundedTime=float.NegativeInfinity;lastActivity=Time.time;
        WriteAnimator(Time.deltaTime);Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "RunningJump",.10f,0,0);
        return true;
    }

    void ResolveRunTurnDodge(Vector3 desiredDirection,bool sprint,ref bool dodgePressed)
    {
        // A reverse running dodge must not cancel the pivot and snap the body 180 degrees.
        if(!IsDodging && IsGrounded && !JumpPreparing)
        {
            if(dodgePressed && CanQueueRunTurnDodge(desiredDirection,sprint))
            {
                queuedRunTurnDodge=true;
                if(!IsTurning)nextTurnAllowed=float.NegativeInfinity;
            }
            if(queuedRunTurnDodge)
            {
                var body=visualRoot!=null?visualRoot:transform;
                bool facingRequest=Vector3.Angle(body.forward,desiredDirection)<5f;
                dodgePressed=!IsTurning&&facingRequest;
                if(dodgePressed)queuedRunTurnDodge=false;
            }
        }
    }

    bool CanQueueRunTurnDodge(Vector3 desiredDirection,bool sprint)
    {
        if(!acceptPlayerInput || !useDodge || !useTurnAnimations || !sprint || runningTurn180Clip==null
            || animator==null || !animator.HasState(0,Animator.StringToHash("Turn_Run180"))
            || desiredDirection.sqrMagnitude<.001f)return false;
        var body=visualRoot!=null?visualRoot:transform;
        return IsRunningTurn || ((RunningDodge || PlanarSpeed>walkSpeed+.25f)
            && Vector3.Angle(body.forward,desiredDirection)>110f);
    }

    private void FinishDodge(Vector3 desiredDirection,bool sprint,float dt)
    {
        bool reverseRun=RunningDodge && CanQueueRunTurnDodge(desiredDirection,sprint);
        IsDodging=RunningDodge=dodgeLockedToTarget=false;
        RunStopping=SadIdle=false;lastActivity=Time.time;InactiveSeconds=0;
        bool moving=desiredDirection.sqrMagnitude>.001f;
        // Hand movement and animation the same requested speed before leaving the dodge.
        planarVelocity=moving?desiredDirection*(sprint?runSpeed:walkSpeed):Vector3.zero;
        // Feed the pivot the outgoing slide's momentum, not instant reverse velocity.
        if(reverseRun){planarVelocity=dodgeDirection*runSpeed;nextTurnAllowed=float.NegativeInfinity;}
        lastRunInputTime=moving&&sprint?Time.time:float.NegativeInfinity;
        FaceLockTarget=combatLookTarget!=null&&!(moving&&sprint);
        WriteAnimator(dt,true);
        Animation.CrossFade(CharacterAnimationDirector.Action.Locomotion, "Locomotion",.08f,0,0);
    }

    private void CacheAnimatorParameters()
    {
        floatParameters.Clear();
        boolParameters.Clear();
        cachedAnimator = animator;
        cachedAnimatorController = animator != null ? animator.runtimeAnimatorController : null;
        if (animator == null || cachedAnimatorController == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float) floatParameters.Add(parameter.nameHash);
            if (parameter.type == AnimatorControllerParameterType.Bool) boolParameters.Add(parameter.nameHash);
        }
    }

    private void WriteAnimator(float dt,bool immediateMovement=false)
    {
        if (animator == null) return;
        if (cachedAnimator != animator || cachedAnimatorController != animator.runtimeAnimatorController)
            CacheAnimatorParameters();
        if (cachedAnimatorController == null) return;
        int lockBlend=Animator.StringToHash("LockBlend"),lockX=Animator.StringToHash("LockMoveX"),lockZ=Animator.StringToHash("LockMoveZ");
        Vector3 localVelocity=(visualRoot!=null?visualRoot:transform).InverseTransformDirection(planarVelocity);
        if(immediateMovement)
        {
            if(floatParameters.Contains(lockBlend))animator.SetFloat(lockBlend,FaceLockTarget?1:0);
            if(floatParameters.Contains(lockX))animator.SetFloat(lockX,localVelocity.x);
            if(floatParameters.Contains(lockZ))animator.SetFloat(lockZ,localVelocity.z);
        }
        else
        {
            if(floatParameters.Contains(lockBlend))animator.SetFloat(lockBlend,FaceLockTarget?1:0,.12f,dt);
            if(floatParameters.Contains(lockX))animator.SetFloat(lockX,localVelocity.x,.10f,dt);
            if(floatParameters.Contains(lockZ))animator.SetFloat(lockZ,localVelocity.z,.10f,dt);
        }
        int attackHash = Animator.StringToHash("IsAttacking");
        if (boolParameters.Contains(attackHash)) animator.SetBool(attackHash, IsAttacking);
        int dodgeHash = Animator.StringToHash("IsDodging");
        if (boolParameters.Contains(dodgeHash)) animator.SetBool(dodgeHash, IsDodging);
        int sadHash = Animator.StringToHash("SadIdle"), stopHash = Animator.StringToHash("RunStopping");
        if (boolParameters.Contains(sadHash)) animator.SetBool(sadHash, SadIdle);
        if (boolParameters.Contains(stopHash)) animator.SetBool(stopHash, RunStopping);
        int preparingHash = Animator.StringToHash("JumpPreparing");
        int runningHash = Animator.StringToHash("RunningJump");
        if (boolParameters.Contains(preparingHash)) animator.SetBool(preparingHash, JumpPreparing);
        if (boolParameters.Contains(runningHash)) animator.SetBool(runningHash, RunningJump);
        if (!string.IsNullOrEmpty(speedParameter))
        {
            int hash = Animator.StringToHash(speedParameter);
            if (floatParameters.Contains(hash))
            {
                if(immediateMovement)animator.SetFloat(hash,PlanarSpeed);
                else if(IsTurning&&turnBlendingOut)animator.SetFloat(hash,turnExitSpeed);
                else animator.SetFloat(hash,PlanarSpeed,0.08f,dt);
            }
        }
        if (!string.IsNullOrEmpty(groundedParameter))
        {
            int hash = Animator.StringToHash(groundedParameter);
            if (boolParameters.Contains(hash)) animator.SetBool(hash, IsGrounded);
        }
        if (!string.IsNullOrEmpty(verticalSpeedParameter))
        {
            int hash = Animator.StringToHash(verticalSpeedParameter);
            if (floatParameters.Contains(hash)) animator.SetFloat(hash, VerticalSpeed);
        }
    }

    public void ResetIdleTimer()
    {
        lastActivity=Time.time;InactiveSeconds=0;SadIdle=false;
        int hash=Animator.StringToHash("SadIdle");
        if(animator!=null && animator.isActiveAndEnabled && boolParameters.Contains(hash))animator.SetBool(hash,false);
    }

    public Vector3 ApplyAttackDisplacement(Vector3 displacement)
    {
        if(!IsAttacking || controller==null || !controller.enabled)return Vector3.zero;
        Vector3 before=transform.position;
        controller.Move(Vector3.ProjectOnPlane(displacement,Vector3.up));
        return transform.position-before;
    }

    // The dash owns all three axes until release. Afterwards ordinary gravity resumes
    // from its current velocity and position; there is no ground-position correction.
    public Vector3 ApplyOrbDashDisplacement(Vector3 displacement,float dt)
    {
        if(!IsAttacking||controller==null||!controller.enabled||dt<=0)return Vector3.zero;
        var before=transform.position;var flags=controller.Move(displacement);
        var moved=transform.position-before;
        verticalVelocity=moved.y/dt;planarVelocity=Vector3.ProjectOnPlane(moved,Vector3.up)/dt;
        IsGrounded=(flags&CollisionFlags.Below)!=0&&verticalVelocity<=0;
        if((flags&CollisionFlags.Above)!=0&&verticalVelocity>0)verticalVelocity=0;
        if(IsGrounded){lastGroundedTime=Time.time;verticalVelocity=-2;}
        else lastGroundedTime=float.NegativeInfinity;
        RunningJump=true;JumpPreparing=RunStopping=SadIdle=false;ResetIdleTimer();
        return moved;
    }

    public void ResumeAttackMomentum(Vector3 heading,float speed)
    {
        if(IsAttacking||!IsGrounded||IsActionBlocked)return;
        planarVelocity=Vector3.ProjectOnPlane(heading,Vector3.up).normalized*Mathf.Clamp(speed,0,runSpeed);
    }
    public void SetAttackActive(bool active)
    {
        if(active)queuedRunTurnDodge=false;
        if(active)CancelTurn(false);
        IsAttacking = active; lastActivity = Time.time; InactiveSeconds = 0;
        if (!active) return;
        planarVelocity = Vector3.zero; SadIdle = RunStopping = JumpPreparing = RunningJump = false;
        lastJumpPressedTime = lastRunInputTime = float.NegativeInfinity;
        if (IsGrounded) verticalVelocity = -2f;
    }

    public void SetIncapacitated(bool active)
    {
        if(!active&&CharacterCombatStats.Dead(this))return;
        IsIncapacitated=active;
        if(!active)return;
        queuedRunTurnDodge=false;
        Animation.InterruptActions();
        SetHitPoseHeld(false);
        hitStunUntil=float.NegativeInfinity;
        CancelTurn(false);ClearAIInput();combatLookTarget=null;planarVelocity=Vector3.zero;
        IsAttacking=IsDodging=RunningDodge=RunStopping=JumpPreparing=RunningJump=SadIdle=false;
        recoveryAction=0;lastJumpPressedTime=lastRunInputTime=float.NegativeInfinity;verticalVelocity=-2;
        WriteAnimator(0,true);
    }
    public void InterruptForHit(float seconds,bool holdPose=true)
    {
        if(IsIncapacitated||animator==null)return;
        queuedRunTurnDodge=false;
        Animation.InterruptActions();
        hitStunUntil=Mathf.Max(hitStunUntil,Time.time+Mathf.Max(.1f,seconds));
        SetHitPoseHeld(holdPose);
        CancelTurn(false);ClearAIInput();planarVelocity=Vector3.zero;
        IsAttacking=IsDodging=RunningDodge=RunStopping=JumpPreparing=RunningJump=SadIdle=false;
        recoveryAction=0;lastJumpPressedTime=lastRunInputTime=float.NegativeInfinity;
        ResetIdleTimer();if(IsGrounded)verticalVelocity=-2;
        // Light/big recoil holds the sampled pose; full-body knockback keeps animation time advancing.
    }
    public void EndAnimatedHit()
    {
        // Do not release a subsequent pose-held hit or a poise knockdown owned by another action.
        if(IsIncapacitated||holdingHitPose)return;
        hitStunUntil=float.NegativeInfinity;
    }
    void SetHitPoseHeld(bool held)
    {
        if(held!=holdingHitPose&&poseTransitions!=null&&poseTransitions.isActiveAndEnabled)
        {
            if(held)poseTransitions.HoldHitPose();else poseTransitions.ReleaseHitPose();
        }
        holdingHitPose=held;
        if(animator==null||!animator.isActiveAndEnabled)return;
        int hold=Animator.StringToHash(HitPoseHeldParameter),speed=Animator.StringToHash(BaseActionSpeedParameter);
        if(boolParameters.Contains(hold))animator.SetBool(hold,held);
        if(floatParameters.Contains(speed))animator.SetFloat(speed,held?0:1);
    }
    public void ApplyIncapacitatedRootMotion(Vector3 delta)
    {
        if(IsIncapacitated&&enabled&&controller!=null&&controller.enabled&&Time.deltaTime>0)
            controller.Move(Vector3.ProjectOnPlane(delta,Vector3.up));
    }

    public void SetPlayerInput(bool accepted)
    {
        if(accepted&&!CharacterCombatStats.CanSelect(this))accepted=false;
        if(accepted!=acceptPlayerInput)
        {
            GetComponent<SwordThrowSkill>()?.Cancel(true);
            ClearAIInput();combatLookTarget=null;planarVelocity=Vector3.zero;
            GetComponent<CharacterComboAttack>()?.CancelForControlChange();
        }
        acceptPlayerInput = accepted;
        if (!accepted) {CancelTurn(true);lastJumpPressedTime = float.NegativeInfinity;recoveryAction=0;}
    }
    public void SetAIInput(Vector3 movement,bool sprint,Transform target)
    {
        if(acceptPlayerInput)return;
        AIControlled=true;aiMovement=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(movement,Vector3.up),1);
        aiSprint=sprint;combatLookTarget=target;
        if(target!=null){RunStopping=false;lastRunInputTime=float.NegativeInfinity;ResetIdleTimer();}
    }
    public bool RequestAIDodge(Vector3 direction)
    {
        if(acceptPlayerInput||!AIControlled||!enabled||!useDodge||!IsGrounded||IsDodging||IsAttacking||IsActionBlocked||JumpPreparing||equipmentBusy||animator==null)return false;
        if(!animator.HasState(0,Animator.StringToHash("Dodge")))return false;
        aiMovement=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;aiSprint=false;aiDodgeRequested=true;
        return true;
    }
    public void ClearAIInput(){AIControlled=false;aiMovement=Vector3.zero;aiSprint=false;aiDodgeRequested=false;}
    private void ReadMovementInput(out Vector2 movement, out bool sprint, out bool jump)
    {
        movement = Vector2.zero;
        sprint = jump = false;
        if (!acceptPlayerInput) return;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        movement.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        movement.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        jump = keyboard.spaceKey.wasPressedThisFrame;
#else
        movement.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        movement.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jump = Input.GetKeyDown(KeyCode.Space);
#endif
        movement = Vector2.ClampMagnitude(movement, 1f);
    }

    private static bool DodgePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    public static Vector3 LockedDodgeDirection(Vector3 localInput)
    {
        // Prefer forward/back at a diagonal tie; camera float noise must not flip the clip.
        if (Mathf.Abs(localInput.x) > Mathf.Abs(localInput.z) + .001f)
            return localInput.x < 0 ? Vector3.left : Vector3.right;
        return localInput.z < 0 ? Vector3.back : Vector3.forward;
    }

    public void ApplyDodgeRootMotion(Vector3 delta, float dt)
    {
        if (!IsDodging || !enabled || controller == null || !controller.enabled || dt <= 0) return;
        var body=visualRoot!=null?visualRoot:transform;
        Vector3 authoredDirection=Vector3.ProjectOnPlane(body.TransformDirection(dodgeMotionAxis),Vector3.up).normalized;
        // Extract travel along the clip's axis, then redirect it along the requested dodge.
        // Projecting directly onto a locked sideways input discarded forward-authored root motion.
        Vector3 movement=dodgeDirection*Vector3.Dot(delta,authoredDirection)*dodgeTravelScale;
        planarVelocity = movement / dt;
        float next = Mathf.Max(verticalVelocity - Mathf.Abs(gravity) * dt, -terminalFallSpeed);
        var collisions = controller.Move(movement + Vector3.up * (verticalVelocity + next) * (.5f * dt));
        verticalVelocity = next; IsGrounded = controller.isGrounded || (collisions & CollisionFlags.Below) != 0;
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
        if (IsGrounded) { verticalVelocity = -2f; lastGroundedTime = Time.time; }
    }


}
