# Iron Crypt — Vault Home

First-person raycasting shooter. Descend through procedurally generated crypts, find the keys, clear out the garrison, reach the exit. Every level comes from a seed, so runs are replayable.

## Quick Start

```bash
dotnet run --project IronCrypt.Client   # client-only dev server (no portal — scores stay local)
dotnet test IronCrypt.Tests             # headless engine tests
```

Full portal stack (auth + leaderboard): `cd ~/Desktop/game && docker compose up`, then http://localhost:3000/iron-crypt/.

## Vault Map

### Design
- [[Design/Core Loop]] — loop, weapons, enemies, pickups, scoring

### Tech
- [[Tech/Architecture]] — stack, portal integration contract, build & run
- [[Tech/Engine]] — code layout, renderer, generator, AI, tuning knobs

### Work
- [[Tasks]] — build phases and status
