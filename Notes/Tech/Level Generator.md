# Level Generator

`Game/LevelGenerator.cs`. Pure function of `(seed, depth)`: same inputs, byte-identical level (a test asserts it). Player-facing description: [[Design/Level Design]].

## Algorithm

1. **Grid** of size `min(30 + 2·depth, 46)`, all solid wall.
2. **Rooms:** up to 300 attempts to place `min(5 + depth, 11)` rooms of 4-8 × 4-8 cells, rejecting overlaps (kept 2 cells apart). Each room draws a 1-cell wall ring of one random texture.
3. **Corridors:** chain room *i* to room *i+1* with an L-shaped corridor (horizontal then vertical, or the reverse, chosen randomly). A chain guarantees everything is connected.
4. **Doors:** walk each room's wall ring. A cell that is now floor (a corridor pierced it), has solid wall on both sides along the wall, and open floor outside becomes a door with 80% probability.
5. **Start and exit:** start in room 0 (facing the next room, snapped to 90°). A breadth-first flood from the start picks the room with the greatest walking distance as the exit.
6. **Locks:** see below.
7. **Populate:** pickups and enemies on unused cells.

## Locks

`PlaceLock(target)`:
1. For every plain door, flood from the start with that door treated as a wall. If the target becomes unreachable, the door is a valid **gate**.
2. Pick the valid gate deepest into the level (largest walking distance from start).
3. Turn it into a locked door.
4. Flood again; put the key in the farthest room still reachable on the start side.

Red gates the exit. From depth 2, blue gates the red key. If no gate exists the lock is skipped, so every generated level stays solvable by construction.

## `Flood`

`LevelGenerator.Flood(level, x, y, red, blue, blocked)` is a plain BFS that returns walking distance per cell (-1 = unreachable). Plain doors are passable; red/blue only when the flags say so. The same function powers:
- level generation (gates, exit choice, spawn distance),
- enemy pathfinding ([[Tech/Enemy AI]]),
- tests (solvability, spawn rules, the playthrough bot).

## Population rules

- Pickups and enemies each take a cell from a shared `taken` set, so nothing ever stacks.
- Enemies only spawn at least 7 walking cells from the start.
- Counts and shares: [[Design/Difficulty]].

## Tests that guard it

Determinism, solvability of 1,200 levels (keys reachable in order, exit reachable, the red door really gates the exit, most levels have a lock), door jambs, no spawns in walls or on each other, and the bot that walks real levels to the exit ([[Tech/Testing]]).

## Not done

Cycles in the room graph (it is a chain, so corridors are the only loops), room shapes other than rectangles, and secret rooms.
