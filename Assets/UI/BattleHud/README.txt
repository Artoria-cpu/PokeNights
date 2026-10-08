Battle HUD
- CentralPark and CharacterSwitchPlayground: 1920 x 1080 overlay, match width.
- Bottom center: rounded green HP, no numbers/ATK/DEF. Weapon cards show Fists / Sword / Orb types and highlight the current slot.
- Left: shared Poke Ball stock. Right: actual SwordThrowSkill / OrbBeamSkill cooldown and E key. Fists have no E skill.
- CaptureCollection starts with 20 balls per session. PokeBallThrow deducts on release, not on aim/cancel. Empty stock blocks aiming; after the last throw the upper-body pose fades out.
- Cashier > SHOP & SUPPLIES: buy 5 balls for 50 coins. Constants: CaptureCollection.BallPackSize / BallPackPrice. Stock survives map and character changes alongside existing session progression.
- Attribute icons: partywhale/pokemon-type-icons, MIT. See Icons/LICENSE.txt and ATTRIBUTION.txt.
- Verified in isolated editor previews at 1920x1080, 1280x720 and 1024x768. Gameplay stock, purchase, health, selection, type and cooldown checks: Work/BattleHud/qa.cs. No Play mode entered.
