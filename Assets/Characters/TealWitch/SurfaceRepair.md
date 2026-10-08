# Teal Witch: surface and motion repair

Open `Assets/Scenes/CharacterSwitchPlayground.unity`; the selected character is `TealWitchPlayer`.
The reusable character is `Assets/Characters/TealWitch/Rigged/TealWitchPlayer.prefab`.

## Geometry and binding

- Keep the generated FBX and original texture in `Source` unchanged.
- Hair, hat and the two painted eye surfaces remain separate renderers.
- The skirt is now a separate renderer with all 5,200 original skirt triangles. The original body/clothing triangle total is preserved across BodyClothes and Skirt.
- Assign the complete skirt, including the hem below the old height cutoff, to skirt bones. Upper thighs blend into the pelvis; the crotch is not split into rigid left/right leg weights.
- Shoulder cloth blends between UpperChest and UpperArm instead of the neck. Sleeve volume is preserved with dual-quaternion deformation.
- The skirt/hair seam has independent backing surfaces and independent weights.

## Runtime components

- `TealWitchHairDynamics`: five rear particle strands with nine joints each and two short front strands. Equal-mass particles integrate velocity, gravity and air drag at 120 Hz. Anchored roots, distance constraints, soft bending and soft transverse links transmit motion through the mesh. Animated ellipsoid contacts and the rear-body guide interpolate through the frame. Contact-relative normal velocity is removed (zero restitution); exact bone lengths and a stable reference orientation prevent stretching and twist spikes. The same surface collision pass runs after skinning. This is a custom position-based physics solver, not Unity Cloth or PhysX rigid bodies.
- `TealWitchClothDynamics`: small damped sleeve/cloth offsets. No lift based on thigh angle and no oversized leg sphere pushing cloth bones.
- `TealWitchSurfaceDeformation`: 24 skirt sectors lift only after sampled contact. Collision profiles come from the original leg cross-sections, with the original rest fit as the contact baseline. Sectors rotate about their waistband anchors and ease down along a collision-checked path. Sleeve and leg joints use dual-quaternion volume preservation. Temporary meshes are restored on disable.
- `BlueHaloEyeGaze`: moves the painted pupils within a fixed eye opening. Face triangles and eye patches share the gaze masks; this is not a spherical eyeball rig.

Physical hair settings: gravity 7, air drag/damping 4.5, root attachment stiffness 0.28, bend constraint strength 0.045, weak rest-shape force 8, strand contact radius 0.035 m and 12 constraint passes. The old angle-lift and delayed-follow settings are compatibility fields and no longer drive hair. Hair movement now arises from root motion, particle inertia, gravity and contacts. `CurrentLiftDegrees` measures strand tilt relative to world-down, rather than a commanded lift angle.

Eye travel remains 0.015 horizontally and 0.003 vertically, with full amplitude during movement and 0.65–1.15 second fixations. These gaze settings are specific to the witch prefab.

## Appearance

The warm-shadow shader uses flat colours and hard boundaries anchored in rest coordinates; shadows have no gradient. It preserves the teal central back hair. Burgundy accents are restricted to lower tips and the undersides of branching locks. The hat underside uses a brighter rose-red tone. Clothing uses warm flat shadows and restrained fold lines. Skirt materials use part 4 so the skin palette cannot overwrite them.

Current body mesh, eye surfaces, skirt closure and opaque briefs are in `Rigged/DetailV6`. The body preserves the ContactV5 leg weights and slightly broadens the lower cheeks / raises the chin; eye surfaces receive the identical displacement while retaining the original gaze coordinates. The independent briefs renderer shares the body volume-preserving skinning. Hair weights blend continuously across the front/back junction. The generated source FBX remains unchanged.

The skirt closure uses a concave-polygon triangulation. Both skirt surfaces share geometric rear pleats and a clean gold stripe, replacing the mismatched separation patch and texture speckles. Part 5 is the opaque briefs palette.

`TealWitchHatContact` uses a 65 × 65 lower-envelope height field sampled from the actual hat mesh, including the tilted rear brim. Both particle and surface jobs use this head-relative shape. Contact removes inward relative velocity; no spring parameters changed. A conservative lower-plane rejection avoids height-field reads for hair below the hat. This is an underside contact shape for the attached hair, not a general closed mesh collider. Height samples belong to the hair component and are disposed on disable.

## Verification

`Work/TealWitch/ContactRepair/PoseQA` contains isolated neutral, spread-leg, raised-knee and forward-lean views and displacement measurements. `ContactRepair/MovementQA` contains 21 actual movement stages and an independently baked waist-to-hem length measurement. `Work/TealWitch/SoftMotion/EquipmentQA` contains pickup, lock, sword attack, draw/stow and slot checks. The input harness restores physical devices, camera input and input-system settings on completion, Play exit or assembly reload. Collision profiles are approximations; their counters alone do not establish visual correctness. Review the rendered poses as well.

Controls: WASD movement, Shift run, Space jump, right mouse dodge, left mouse attack, middle mouse lock, 0 character cycle, 1 empty slot, 2 sword slot after pickup, R camera reset.

`Work/TealWitch/ExpressiveMotion` contains the subsequent movement regression, left/centre/right eye renders, fixed-pose settling check, and a baked-mesh comparison with motion offsets disabled/enabled at identical poses.

`Work/TealWitch/WholeHairLift` contains the whole-strand update: actual movement renders, 21-stage input regression, matched-pose baked hair comparison, and a run-to-stop settling measurement. The matched 5 m/s case moves the middle hair by 0.213 m RMS and tips by 0.428 m RMS; its lift decays from 56.2 degrees to 0.52 degrees after one second stopped, without repeated rebound. Those measurements describe the sampled test, not a guarantee for every animation or obstacle.

Recovery prefabs/scenes are in `Rigged/Recovery`.

## Performance

`TealWitchSurfaceJob` runs the same per-vertex skinning, volume preservation, skirt rotation and hair contacts through the project's existing Burst/Collections packages. Mesh buffers persist until disable and are explicitly disposed; bones and weld groups are cached. Face contacts still run each frame, but their second vertex projection is skipped when no face correction occurred. Skirt search reuses its current-angle penetration result.

`Work/TealWitch/Performance` records the before/after benchmark and movement checks. On this Editor, three matched poses reduced surface deformation from 61–71 ms to 6–8 ms per call. Maximum baked-position difference was 0.00000264 m. A separate input run without screenshots, capped at 60 FPS, averaged about 59 FPS walking/idle, 52–54 FPS running and 59 FPS dodging. These are Editor measurements, not standalone build performance. All 21 visual regression stages completed; Play exit left no temporary deformed meshes or test input devices.

`Work/TealWitch/PhysicalHair` contains the physical-chain regression, inspected running/rolling/sliding views, stopping-response samples and a separate frame-timing run. The body collision shapes approximate the character; they do not guarantee correctness for all poses or arbitrary environment collisions.

`Work/TealWitch/DetailFix` contains the next four-detail refinement: before/after face and skirt renders, a 21-stage movement report, timing samples and original runtime file backups. The sampled movement run completed without invalid mesh coordinates, had zero residual contacts for the tested lower-hair vertices, and a maximum skirt waist-to-hem ratio of 1.000035. Running, jumping, rolling and attack renders were inspected; sampling is not a guarantee for every pose.
