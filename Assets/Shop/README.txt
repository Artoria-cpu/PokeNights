Shop runtime
- The overview buttons open the workshop and cashier. Escape closes the current panel.
- Workshop: browse captures, click PROCESS, complete three timed W/A/S/D prompts, then sell.
- Processing clears blood, hides the human body while retaining Pokemon parts, and plays the assigned particle at the enemy.
- CharacterBlood adds mesh stains when players and enemies take damage. CleanUnder removes them after processing.
- Cashier: change weapon types, view type matchups, buy balls, and purchase player/shop upgrades.
- CaptureCollection retains captures, money and upgrades across scenes for the current session; no disk save.
- SceneFadeTransition uses a fullscreen black UI Image and changes its color alpha for scene transitions.
Checks: Work/Cashier/qte-effect-qa.cs, Work/EnemyCombat/image-fade-qa.cs, CashierQA.Run().
