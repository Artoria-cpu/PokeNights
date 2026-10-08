R: raise a Poke Ball directly into upper-body aim (source frames 56 -> 68; skips the pickup).
Mouse: free camera yaw/pitch. WASD: walk/strafe. Sprint, jump, dodge and weapon attacks are suppressed during the throw action.
Left click: continue from source frame 68; release at frame 73; finish at frame 126, then raise the next ball and remain in aiming mode. Right click while aiming exits via reverse animation.
Right click before committing the throw: reverse the same upper-body animation back to frame 56 and blend out.
An interruption or character switch removes the held ball and clears aim immediately.

Upper Throw.anim contains only torso/head/arm/finger muscles, with a separate AvatarMask excluding the root and legs.
PokeBallTime explicitly samples the clip, allowing a held aiming pose and reverse playback without pausing the locomotion Animator.
Source FBX is preserved at Source/Throw Object.fbx.
PokeBall.prefab uses the supplied FBX's red, white and dark materials; diameter is 0.13 m.
Trajectory and flight use the same 0.02-second ballistic samples, gravity and swept sphere collision. The marker shows the first impact point (floor, wall or character).
Flight is compensated for the hand's movement between aim and release. A chest-to-hand sweep prevents spawning through an obstacle.
After the first swept impact, balls use a sphere collider and continuous dynamic Rigidbody for bounce, roll and gravity. Empty balls expire after 20 seconds; occupied balls persist for the current session.
Living enemies below 50% HP have a 50% capture chance per ball/enemy contact attempt. PokeBallCapture.captureChance is editable on the prefab. Successful capture rises, opens the shell, envelops the enemy in a light cone, shrinks and absorbs it, then closes and falls using physics. CapturedEnemy retains the inactive enemy; no save/inventory system is implied.
Aiming camera uses a raised right shoulder offset; throw speed is 20 m/s.
Original R camera reset binding is removed; R now belongs to Poke Ball aiming.


Capture keeps the ball at its original scale. Aim camera right offset: 0.90 m.
