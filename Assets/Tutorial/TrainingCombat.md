# Tutorial combat sequence

Existing objects remain in place. New stages are under `Tutorial Sequence`; move these groups and gates to arrange the level.

1. Sword Enemy drops a collectible sword at its body after death.
2. Group 3 enemies remain idle until individually hit. Their gate descends only after all three die.
3. Orb Parry Enemy uses only attack stage 4, a parryable attack. It remains idle until first hit. At the first flash, the game pauses and displays the English parry hint. Left-click resumes into the existing automatic parry, removing 75% of enemy poise. Its death opens its gate and drops a collectible orb.
4. Group 5 enemies remain idle until individually hit. Their gate descends only after all five die.
5. Capture Target starts at 25% health, is invincible, and never fights. Capture it to lower Capture Wall. Killing it does not open that wall.

Scripts:
- `TrainingEnemy`: weapon choice, wait-for-hit, attack index and stationary target option. Attack index 0 is stage 1; index 3 is stage 4.
- `DeathDrop`: spawns a normal weapon pickup at the fallen character's hips after a short delay.
- `GroupDeathDown`: shared by both enemy groups; assign all enemies in the array.
- `DeathDown`: one enemy's death lowers the attached object.
- `CaptureDown`: successful capture lowers the attached wall.
- `DodgeLesson` / `ParryLesson`: one-time English prompts and input handling.

All gate speeds are adjustable. They continue descending without a stop height.
Original pickups remain where you placed them; corpse drops are additional pickups using the same inventory rules.

