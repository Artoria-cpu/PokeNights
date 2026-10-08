Tutorial / September update
- MainMenu StartGame -> Tutorial -> CentralPark. Tutorial has its own courtyard, practice target, sword and orb pickups. The hat character remains the player.
- Lessons observe movement distance, DodgeCount, AttackCount, actual equipment ownership, completed skill use, and released balls. F near the green gate finishes; hold Enter 1.2 sec to skip.
- Sword cooldown: 12 seconds after ending/interrupting. Orb beam: 16 seconds after ending/interrupting. Follow-up E actions during an active skill remain available.
- Battle HUD: removed weapon/inventory/skill panel backgrounds; selected type is mint colored. Cooldown uses a 30 px orange timer, dim icon and radial cover.
- New session starts with 100 coins. Existing session balances are not overwritten on scene travel.
- First loaded scene starts fully black, then fades in over 1 second. Tutorial scene transition fades in over 1.1 seconds.
- Shop: CinemachineDeoccluder pulls camera forward against ShopCameraObstacles. Static room/furniture colliders use that layer; actors are excluded. Zoom range 0.45-1.05, vertical range 0.1-0.9. Existing placement is preserved.
- Verified using isolated editor previews: 192 shop camera viewpoints (0 out-of-room results), tutorial action progression / skill timers / HUD / initial economy, and fade coroutine ordering. Play mode was not entered.
