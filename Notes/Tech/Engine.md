# Engine

Layout of the game code in `IronCrypt.Client/` and where the tuning knobs live. Stack overview: [[Tech/Architecture]]. Design intent: [[Design/Core Loop]].

## Files

| File | Owns |
|------|------|
| `Game/Engine.cs` | Core loop: `Tick`, player movement + wall collision, door state machine, pickup collection, exit, seeds (`NewGame`, `NextLevel`, `SeedFromText`, `LevelSeed`). Emits `Sfx` events into `Sounds`. |
| `Game/Engine.Combat.cs` | `WeaponDef` table, magazines + reloads, weapon switching, `RayToWall`, `Hurt` (armor), item effects, score. |
| `Game/Engine.Enemies.cs` | `EnemyDef` table, AI state machine, flow-field pathfinding, `HitScan`, `Damage`, blood droplets. |
| `Game/Level.cs` | Cell grid, door open/timer arrays, `Blocks` / `BlocksSight`, `LineOfSight`, `FromRows` (text maps for tests). |
| `Game/LevelGenerator.cs` | Seeded generator + `Flood` (BFS distance field, also used for pathfinding and by tests). |
| `Game/Renderer.cs` | Framebuffer, floor/ceiling, wall raycasting, minimap, damage tint, shake, `SizeFor`. |
| `Game/Renderer.Sprites.cs` | Billboards (pickups, exit, enemies, blood): project, depth-sort, draw against the wall z-buffer. |
| `Game/Renderer.Weapon.cs` | First-person weapon, muzzle flash, crosshair. |
| `Game/Art*.cs` | All pixels: wall/door textures, pickup, enemy and weapon sprites (`Painter` = tiny pixel canvas). |
| `Pages/Game.razor` | Game states, the `Tick` entry point, DOM HUD, portal score submit/leaderboard. |
| `Components/*.razor` | Presentational overlays — parameters in, `EventCallback`s out. |
| `wwwroot/js/gameInterop.js` | rAF loop, input (keyboard, mouse + pointer lock, touch), canvas blit, WebAudio synth + music. |

`Game/` is headless: it only fills arrays and lists, so `IronCrypt.Tests` drives it directly (including rendering a frame).

## Frame flow

```
rAF (JS) ── Tick(timestamp, input[12]) ──▶ Game.razor
                                             ├─ Engine.Tick(dt, PlayerInput)      simulation
                                             ├─ Renderer.Render(engine) → byte[]  RGBA framebuffer
                                             └─ gameInterop.present(bytes, w, h, soundIds)
                                                   ├─ putImageData  (canvas is CSS-upscaled, pixelated)
                                                   └─ sfx(id) for each sound raised this tick
```

One interop call in, one out, per frame. The input array is documented above `readInput()` in `gameInterop.js`.

## Renderer

- **Resolution**: 200 px on the short side, long side follows the viewport aspect (capped 480 landscape / 440 portrait) — `Renderer.SizeFor`. Recomputed every frame, so resizing and rotating just work.
- **Walls**: classic grid DDA per screen column. Textures are 64×64, stored column-major and **pre-shaded into 16 distance levels** (`Art.Shaded`), so the inner loop is one array read + one write. Y-facing walls are one level darker.
- **Doors**: a thin slab in the middle of the cell (half-step inside the DDA) that slides sideways by `DoorOpen`. Requires solid jambs on both sides — the generator guarantees it, a test enforces it.
- **Floor/ceiling**: flat distance-shaded rows (no floor casting — `ponytail:` comment marks the upgrade path).
- **Sprites**: camera-space projection, far-to-near sort, per-column z-buffer test, alpha 0 = transparent.
- **Effects**: damage flash = whole-frame red tint; shake = horizon offset; both driven by engine timers.
- **Cost**: ~3 ms per frame (Release, no AOT, desktop Chrome) at 386×200 — measured Nov 2026. AOT is deliberately not used (needs the `wasm-tools` workload in CI and Docker).

## Level generator

1. Rejection-sample rooms (4–8 cells a side, 2 cells apart). Each room paints its wall ring with one of 4 textures.
2. Chain room *i* → *i+1* with L-shaped corridors → everything is connected.
3. A corridor cell that pierces a room's wall ring with solid jambs becomes a door (80%).
4. Start = room 0. Exit = room farthest by walking distance.
5. **Locks**: find a door whose removal cuts the start off from the exit → red door; drop the red key in the farthest room still reachable. From depth 2, repeat with the red key as the target → blue door + blue key. If no single door gates the target, the level simply has no lock (a test asserts that most levels do have one).
6. Pickups and enemies go on unused room cells; enemies at least `MinEnemyDistance` path cells from the start.

Deterministic per `(seed, depth)`; the level seed is `seed * 31 + depth * 7919`.

## Enemy AI

`Idle → Chase → Windup → (attack) → Chase`, with `Pain` interrupting on a per-type chance and `Dead` leaving a corpse.

- Wake: line of sight within `SightRange`, or a gunshot within `HearingRange` walking cells, or being hit.
- Pathfinding: one BFS distance field from the player's cell (`LevelGenerator.Flood`), refreshed when the player changes cell or every 0.5 s. Each enemy steps to the neighbouring cell with the lowest distance; within 3.5 cells and in sight it runs straight at the player instead.
- Solid bodies: enemies collide with walls, the player and each other, but a move that *separates* two bodies is always allowed (no deadlocks).
- Doors never close on a body (`DoorwayOccupied`) — that is the guard against things stuck in walls.

## Tuning knobs

| What | Where |
|------|-------|
| Move speed, door reach/speed/hold, pickup reach | constants at the top of `Engine.cs` |
| Weapon damage, spread, fire rate, magazine, reload | `Engine.Weapons` in `Engine.Combat.cs` |
| Health/armor/ammo caps, level bonus | constants in `Engine.Combat.cs` |
| Enemy health, speed, damage, range, wind-up, score | `Engine.EnemyDefs` in `Engine.Enemies.cs` |
| Sight/hearing range, per-depth scaling | constants in `Engine.Enemies.cs` |
| Map size, room count, enemy count/mix, drop rates | `LevelGenerator.Generate` / `Populate` |
| Mouse sensitivity, key turn speed | constants in `Pages/Game.razor` |
| Touch stick radius, touch look speed | `initTouch()` in `gameInterop.js` |
| Field of view, minimap size | constants in `Renderer.cs` |
| Sound design, music generator | `sfx()` / `startMusic()` in `gameInterop.js` |

## Tests

`IronCrypt.Tests/EngineTests.cs` — 44 tests, all headless: movement/collision (incl. a 20k-step random walk that must never end in a wall), generator determinism, **solvability of 1,200 generated levels** (keys reachable before their doors, exit reachable), door jambs, spawn placement, doors/keys/exit, weapons and reloads, pickups, armor, enemy sight/hearing/pathfinding/doors, a 40-second simulation per seed asserting nobody is ever inside a wall, and a **bot that plays 6 seeds × 4 depths to the exit**.

## Known limits

- Audio and feel were verified by automation only (sounds fire, 60 FPS in headless Chrome at desktop and phone sizes). Not yet played on a physical phone.
- Local `docker build` needs the repo's `.dockerignore` (keeps host `bin/`/`obj/` out of the build context — without it the host's restore state breaks `dotnet publish` in the container).
