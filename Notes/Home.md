# Iron Crypt — Vault Home

First-person raycasting shooter. Descend through procedurally generated crypts, find the keys, clear out the garrison, reach the exit. Every level comes from a seed, so runs are replayable.

## Quick Start

```bash
dotnet run --project IronCrypt.Client   # → http://localhost:5310, client-only (no portal — scores stay local)
dotnet test IronCrypt.Tests             # headless engine tests
```

Full portal stack (auth + leaderboard): `cd ~/Desktop/game && docker compose up`, then http://localhost:3000/iron-crypt/.

## Vault Map

### Design
- [[Design/Core Loop]] — loop, seeds, scoring
- [[Design/Weapons]] — the three guns, magazines, reload rules
- [[Design/Enemies]] — grunt, shooter, tank: stats and what each asks of you
- [[Design/Difficulty]] — per-depth curve and supply rates
- [[Design/Level Design]] — rooms, doors, keys, exit, minimap
- [[Design/Visual Identity]] — palette, rendering style, sprites
- [[Design/Controls and UI]] — keyboard, mouse, touch, every screen

### Tech
- [[Tech/Architecture]] — stack, portal integration contract, build & run
- [[Tech/Engine]] — code layout, frame flow, tuning knobs
- [[Tech/Renderer]] — raycaster, sprites, resolution, effects
- [[Tech/Level Generator]] — algorithm, locks, flood fill
- [[Tech/Enemy AI]] — state machine, pathfinding, collision
- [[Tech/Input and Interop]] — frame contract, input array, touch
- [[Tech/Audio]] — synthesized effects and generated music
- [[Tech/Testing]] — what the tests cover and how to write one
- [[Tech/Performance]] — measurements and the budget
- [[Tech/Decisions]] — choices and the reasons

### Work
- [[Tasks]] — build phases and what is next (new work goes in as GitHub issues)
