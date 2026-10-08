Main menu and patrol — 2026-09-27

Startup scene: Assets/Scenes/MainMenu.unity (first enabled build scene).
Start Game loads CentralPark. Quit Game exits the application; in Editor it stops Play mode.
Background: TealWitch, the hat character, seated on the chair behind the checkout counter.
Sitting.fbx is preserved; SittingMenu.anim is a separate looping 2.1-second Humanoid clip.
Menu character contains no gameplay behaviours. The actual shop scene layout is unchanged.
CentralPark, PokemonShop and CharacterSwitchPlayground now default to TealWitchPlayer.

Enemy patrol: walking within 6 metres of home, pauses 1.5–4 seconds between destinations.
Destinations must be on the existing NavMesh with complete paths. Blocked patrols retry after timeout.
Detection interrupts patrol; lost targets return home before resuming patrol.
Noncontrolled playable companions stay idle. Pursuit/attack caps remain 6 / 3.

Validation: 240 reachable patrol samples across 12 enemies, walking/pause state checks,
menu button bindings, valid looping Humanoid avatar, default player bindings and clean compilation.
Offscreen rendered preview: Work/MainMenu/preview.png. Play mode was not entered.

English UI and menu update — 2026-09-27
All runtime shop/workbench prompts, type labels, rarity labels and scene UI text are English.
Main menu subtitle removed. Menu uses layered lettering, a ball emblem, tilted ticket buttons
and smooth hover/keyboard selection feedback. Start/Quit callbacks retained.
Added three decorative Pokemon headpieces and six saved blood-patch meshes under
MainMenu / Bloodied Pokemon Parts. No capture, combat or cleaning gameplay runs on these props.
User-edited camera, character and furniture transforms retained (423 original non-UI transforms checked).
Camera remains at the user-edited position (1.77, 0.54, -2.59), FOV 42.
Validated scene UI language, persistent stain meshes, English layout renders and 22 care/economy checks.
No Play-mode run performed. Original menu scene snapshot: Work/MainMenu/EnglishBackup/MainMenu.unity.

Canvas / additional remains / scene fades — 2026-09-27
Main menu canvas: Screen Space Overlay, sort 0, display 1, 1920x1080,
Scale With Screen Size, Match Width Or Height = 0 (Width), reference pixels/unit 100.
Shader channels: TexCoord1, Normal, Tangent; pixel perfect and gamma vertex color off.
Existing menu rects and typography scaled proportionally from 1280x720.
Added 2 static baked fallen enemies, 6 more loose parts (9 total), 9 wall splashes and 2 floor stains.
Heavy blood patches on bodies and parts use saved mesh assets. Original scene transforms retained.
All current travel routes use SceneFadeTransition: fade out 0.45 s, fully black frame,
load scene by build index, then fade in 0.4 s. UI clicks blocked during transition.
Fade runs on unscaled time and persistent highest-order overlay. Duplicate transitions ignored.
Verified canvas settings, saved meshes, fade ordering/curves and interrupted-transition cleanup.
No actual scene load or Play-mode run was performed for final verification.
