Gameplay audio integration

Source: the 16 MP3 files supplied by the user, retained in Source/. Sitting.fbx is an animation already used by the menu and was not re-imported.
23 mono WAV action clips in Clips/: leading/trailing silence trimmed, short fade edges, peak balanced, footsteps split into individual impacts. Source-to-segment timings: Work/GameplayAudio/segments.json.

Mappings:
- Sword swings / heavy slash / sword skill launch and recall: 54427377, musicholder.
- Fist hits: fist-punch-or-kick, universfield-fist-fight, universfield-punch-impact-hit.
- Fist swing, dodge and thrown-ball whoosh: scratchonix-air-blow.
- Orb projectile: magic-strike. Finisher / magic impact / dash: humordome-magic-burst.
- Beam charge: soumages-magic-spell. Beam discharge: yodguard-dark-magic-6. Interruption stops the dedicated spell voice.
- Player hurt: stepir44. Enemy hurt variants: male_hurt7 and male-hurt-sound.
- Shop footsteps: walk-on-wood. Other maps: run-on-asphalt-road. Steps require grounded movement; running uses a faster cadence.
- Landing: jumplanding, triggered only after a real airborne fall.
- Existing ParryClashAudio is preserved.

Tuning:
Assets/Resources/GameplayAudio.asset: masterVolume and all clip assignments.
CharacterActionAudio.volume: per-character movement/swing/voice level.
GameplayAudio: 24 concurrent one-shot voices, 2-24 m linear falloff, zero Doppler, slight pitch variation, scene-travel cleanup.
No added AudioListener. Each gameplay scene retains one active listener.
Verification: all 15 cues resolve, all 23 clips mono/preloaded, all 34 scene actors wired, Unity compiles. No Play mode entered and no device-build listening test performed.
