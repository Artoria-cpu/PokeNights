Pokedex source: https://github.com/Purukitto/pokemon-data.json
Downloaded: 2026-09-21. File: pokedex.json (unmodified upstream JSON).
Assignment: Data Sources, Game Jam #1, Here, use this data (page 10).

CharacterCombatStats reads the JSON at runtime using PokemonCombatDatabase and the existing Newtonsoft.Json dependency.
Pokemon ID 1 = Bulbasaur; ID 4 = Charmander.
HP, Attack, Defense, Sp. Attack, Sp. Defense and Speed use database values directly without multipliers.
Speed is retained for future balancing; it does not change authored animation timing.
Player defaults: HP 240, ATK 60, DEF 50, special ATK 60, special DEF 50.
Physical hits use Attack/Defense; orb attacks use Sp. Attack/Sp. Defense.
Damage = max(1, round(offense * power * 100 / (100 + defense))). Default power 0.35; orb dash 0.6.
This is an action-game balance formula, not the official turn-based Pokemon damage formula.
Poise remains independent and keeps its existing tuning, regeneration and fall/get-up behavior.
A lethal hit skips poise and hit reactions and permanently enters the directional death state.

