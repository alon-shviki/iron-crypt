# Enemy AI

`Game/Engine.Enemies.cs`. Enemy stats and what they feel like: [[Design/Enemies]].

## State machine

```
Idle ──sees/hears/shot──▶ Chase ──in range, cooled down, can see──▶ Windup ──timer──▶ attack ──▶ Chase
                            ▲                                          │
                            └──────────── Pain (flinch) ◀──── hit ─────┘ (chance per type)

any state ── hp ≤ 0 ──▶ Dead (corpse sprite, no collision, no AI)
```

- **Idle → Chase:** line of sight within 12 cells, a gunshot within 16 *walking* cells (flood distance, so sound does not pass through walls), or taking damage.
- **Chase → Windup:** only when it can see the player, is within its attack range, and its cooldown is over. The wind-up is the telegraph.
- **Attack** resolves after the wind-up using the player's position *then*: melee needs `range + 0.35`, so stepping away dodges it; ranged needs line of sight, rolls a hit chance that falls with distance (`0.85 - 0.06·dist`, clamped to 0.25-0.85), and deals damage.
- **Pain** interrupts a wind-up, lasts 0.18 s.

## Pathfinding

One shared **flow field**: a BFS distance-from-the-player over the grid (`LevelGenerator.Flood`), recomputed when the player changes cell or every 0.5 s. Each enemy looks at its 4 neighbouring cells and steps toward the lowest distance. Passing a door opens it; the field only crosses locked doors once the player holds the key, so enemies are held back exactly as the player is.

Shortcut: within 3.5 cells and in sight, an enemy steers straight at the player instead (smoother than cell-hopping; falls back to the field if it gets stuck on a corner).

Cost: one BFS over at most 46 × 46 cells every half second for any number of enemies.

## Body collision

- Walls: same 4-corner check as the player, with the enemy's own radius.
- Player and other enemies are solid, **except** a move that increases the distance between two overlapping bodies is always allowed. That one rule stops enemies being deadlocked inside each other.
- Movement is axis-separated so enemies slide along walls.
- Doors never close on a living body (`DoorwayOccupied`).

## Spawning

`SpawnEnemy` refuses a position that overlaps a wall and returns null, so an enemy can never start inside geometry. A test simulates 40 seconds per seed with enemies chasing and asserts nobody (player included) ever ends inside a wall.

## Hits and damage

`HitScan` finds the nearest living enemy along the ray closer than the wall (perpendicular distance against the enemy's radius), then `Damage` handles health, kills (+score, +kills), sounds, waking, flinching and blood.

## Ideas

Charger, exploder, summoner and boss are open GitHub issues; the state machine would gain a `Charge` state for the first.
