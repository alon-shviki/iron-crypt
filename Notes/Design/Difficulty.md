# Difficulty

There is no final level: difficulty rises with **depth** until the player dies, and the score is the chase. Everything below is computed in `LevelGenerator` and `Engine.Enemies.cs`. Retune from [[Tech/Engine]]'s knob table. Balance is **first-pass and unplaytested** (tracked in the GitHub issue "Balance pass").

## Per-depth curve (d = depth)

| Thing | Formula | d=1 | d=3 | d=6 | cap |
|---|---|---|---|---|---|
| Map size (cells a side) | min(30 + 2d, 46) | 32 | 36 | 42 | 46 (d ≥ 8) |
| Rooms | min(5 + d, 11) | 6 | 8 | 11 | 11 |
| Enemies | min(5 + 3d, 40) | 8 | 14 | 23 | 40 (d ≥ 12) |
| Tank share of enemies | 0 at d=1, else min(5d, 25)% | 0% | 15% | 25% | 25% |
| Shooter share | min(15 + 5d, 45)% | 20% | 30% | 45% | 45% |
| Enemy health | × (1 + 0.08 (d-1)) | ×1.0 | ×1.16 | ×1.40 | none |
| Enemy damage | × (1 + 0.05 (d-1)) | ×1.0 | ×1.10 | ×1.25 | none |

The rest of the share is grunts.

## What keeps the player supplied

Per room (except the start room), independent chances: bullets 55%, shells 35%, health 40%, armor 18%. Plus bullets in the start room, the shotgun in one of the first two rooms, and the chaingun from depth 2. Enemies grow faster (count and toughness) than supplies, so ammo gets tighter the deeper you go.

## Scoring

Kills pay 100 / 150 / 400. Clearing depth *n* pays 500 × *n*, so going deeper pays more per level and rewards pressing on.

## Safety rules the generator keeps

- Nothing spawns within 7 walking cells of the start.
- Every level is solvable: keys are always reachable before the doors they open (tested over 1,200 generated levels).
- Pickups and enemies never share a cell and never land in walls.

## Tuning checklist (when playtesting)

1. Do players run dry of ammo by depth 3-4 even when playing well?
2. Is the first tank fight (depth 2) survivable with the pistol alone?
3. Does the 8% / 5% scaling feel gentle early and brutal late, or flat?
4. Is the 40-enemy cap too crowded or too empty on a 46-cell map?
