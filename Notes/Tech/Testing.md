# Testing

`IronCrypt.Tests/EngineTests.cs`, xUnit. Everything runs headless: the simulation, generator and renderer never touch the DOM, so a plain `dotnet test` covers the game without a browser.

```bash
dotnet test IronCrypt.Tests -c Release
```

CI (`ci / build`) runs format check, build, a vulnerable-package gate and these tests on every PR.

## What is covered

| Area | Tests |
|---|---|
| Movement | stops at walls, slides along them, a huge `dt` cannot tunnel, a 20,000-step random walk never ends inside a wall |
| Generator | deterministic per seed, **every level solvable over 1,200 seed/depth pairs** (keys reachable in order, exit reachable, red door truly gates the exit, most levels have a lock), doors always sit between solid jambs, nothing spawns in walls / stacked / near the start |
| Doors, keys, exit | slide open, close behind but never on the player, locked door needs its key, exit completes the level |
| Seeds | text seeds are stable (own FNV-1a hash, not `string.GetHashCode`), levels differ by depth |
| Combat | cooldowns, ammo, auto-reload, pistol never dry, weapon select and cycling, pickups apply (and stay on the floor when full), armor soaks half, death freezes the sim, bullets stop at walls and closed doors |
| Enemies | nearest-target hitscan, no hits through walls or behind, sight vs wall, gunshot hearing, pathfinding round a wall, shooters need line of sight, plain vs locked doors, nothing ever inside a wall in a 40 s simulation per seed, depth scaling |
| Flow | level bonus grows with depth, a new game resets the run, blood falls and is capped |
| Renderer | draws a textured wall, red flash and shake, works at desktop / portrait / landscape / ultrawide sizes with a sprite in the camera's face |
| **Bot** | a scripted player walks 6 seeds × 4 depths to the exit, fetching keys in order, asserting it is never inside a wall |

## Not covered (manual or browser only)

- Audio quality (only that it runs).
- Touch feel on a real phone.
- The portal integration: that was verified by hand against an isolated compose stack (register, login, score submit through the game's nginx, leaderboard, in-iframe launch).

## Browser checks used during the build

Headless Chrome driven over the DevTools protocol: take screenshots of every screen, simulate keyboard and touch events, time the frame, and call the audio functions. Those were throwaway scripts, not committed.

## Tips

- **Make a scenario with `Level.FromRows`**: draw the room in ASCII (`#` wall, `.` floor, `S` start, `X` exit, `D` door, `R`/`B` locked doors) and spawn what you need with `SpawnEnemy`.
- **Hold enemies still** by setting `State = Pain` with a large `Timer`.
- Tests that need a Docker daemon do not exist here (unlike the portal's); a failing Docker login on your machine cannot break these.
