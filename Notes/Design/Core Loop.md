# Core Loop

First-person crypt crawl. One run = descend as deep as you can; death ends the run and the score goes to the portal leaderboard.

## The loop

1. Spawn in the start room of a generated level.
2. Explore: rooms joined by corridors, doors slide open as you approach.
3. Find the **blue key** (depth 2+) → it opens the blue door guarding the **red key** → which opens the red door guarding the **exit**.
4. Fight the garrison on the way. Pick up health, armor, ammo and weapons.
5. Step into the green archway → level-complete screen → next depth: bigger map, more and tougher enemies.

A run is defined by its **seed**. Type one on the title screen (a number or any word) to replay exactly the same crypts; leave it empty for a random one. The seed is shown on the HUD, pause and game-over screens.

## Weapons

| Weapon | Ammo | Magazine | Feel |
|--------|------|----------|------|
| Pistol | bullets | 12 | Accurate, slow. **Never dry** — if the bullet reserve is empty it still reloads, so a run cannot soft-lock. |
| Shotgun | shells | 6 | 7 pellets, wide spread. Brutal up close, useless at range. Found on depth 1. |
| Chaingun | bullets | 50 | Very fast, loose spread, eats the bullets the pistol shares. Found from depth 2. |

Every gun has a magazine and a reload time (auto-reload on empty, `R` to reload early). Switching weapons takes a moment and cancels a reload.

## Enemies

| Enemy | Role | Behaviour |
|-------|------|-----------|
| **Grunt** | melee, fast | Charges and claws. Step back during the wind-up to dodge. |
| **Shooter** | ranged | Keeps its distance, raises its rifle (the telegraph), fires. Accuracy drops with range; breaking line of sight during the wind-up cancels the hit. |
| **Tank** | slow, strong | Huge health, hits like a truck, rarely flinches. Appears from depth 2. |

All of them idle until they **see** the player (line of sight) or **hear** a gunshot nearby, then chase using a pathfinding field — they go around walls and open plain doors. Locked doors hold them until the player has the key.

## Pickups

Health +25 · Armor +50 (soaks half of incoming damage) · Bullets +30 · Shells +8 · Shotgun · Chaingun · keys. A pickup you cannot use (full health, full ammo) stays on the floor for later.

## Scoring

Grunt 100 · Shooter 150 · Tank 400 · clearing depth *n* pays 500 × *n*. Portal payload: `Value` = score, `Kills`, `Level` = depth reached.

## Difficulty curve

Per depth: the map grows (32 → 46 cells a side), rooms 6 → 11, enemies 8 → 40, the shooter and tank share rises, enemy health +8% and damage +5% per depth. There is no last level — the question is only how deep.

## Open questions

- Sounds and music are synthesized and have only been verified to *play*; whether they sound good needs human ears. Tuning lives in `gameInterop.js` (`sfx`, `startMusic`).
- Balance (ammo scarcity, enemy counts) is first-pass and untuned by playtests — knobs are listed in [[Tech/Engine]].
- The player can walk through enemies (they cannot walk through the player). Fine so far; revisit if it feels cheap.
