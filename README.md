# Iron Crypt

A browser first-person shooter built with **Blazor WebAssembly** and **.NET 10** — a software raycaster in C#. Descend through procedurally generated crypts, find the keys, clear the garrison, reach the exit. Every level comes from a seed, so any run can be replayed.

No game engine, no asset files, no external services: wall textures, sprites, sound effects and music are all generated in code. Once built it runs entirely in the browser.

## Tech Stack

| Layer                       | Technology                                                                        |
| --------------------------- | --------------------------------------------------------------------------------- |
| Frontend                    | Blazor WASM (.NET 10)                                                             |
| Rendering                   | C# raycaster → RGBA framebuffer → one `putImageData` per frame (`gameInterop.js`) |
| Game Loop                   | `requestAnimationFrame` → JS interop → C# `Tick`                                  |
| Simulation                  | Headless in `Game/` (no rendering/interop — directly testable)                    |
| Audio                       | WebAudio synthesis in `gameInterop.js` — no audio files                           |
| Auth / Scores / Leaderboard | Portal auth server — **no game-specific backend or database**                     |
| Tests                       | xUnit (`IronCrypt.Tests`) — headless engine, generator and renderer tests         |
| Deployment                  | Docker (nginx-served WASM image)                                                  |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (optional, for the production-style image and the full portal stack)

### Run the client (game only, no portal auth)

```bash
dotnet run --project IronCrypt.Client
```

Opens http://localhost:5310 (fixed in `Properties/launchSettings.json`, so it never collides with whatever else uses the .NET default port 5000) — do not open `index.html` directly, the game requires an HTTP context. Scores stay local (`localStorage`) in this mode.

### Run the full stack (with auth + leaderboard)

```bash
cd ~/Desktop/game && docker compose up --build
```

Visit `http://localhost:3000/iron-crypt/` (dev-only direct port: `http://localhost:8082`), or press **Play** on the Iron Crypt card in the portal.

### Run tests

```bash
dotnet test IronCrypt.Tests -c Release
```

### Build the production image

```bash
docker build -f IronCrypt.Client/Dockerfile -t iron-crypt-client .
```

CI (`.github/workflows/ci.yml`, a thin caller of the portal's reusable workflow) runs format → build → test on every PR and pushes `ghcr.io/alon-shviki/iron-crypt-client:latest` on merge to `main`.

## Controls

| Action  | Keyboard / mouse                                      | Touch                                  |
| ------- | ----------------------------------------------------- | -------------------------------------- |
| Move    | `W` `A` `S` `D`                                       | left thumb — floating stick            |
| Look    | mouse (click the game to capture it) or `←` `→`       | right thumb — drag                     |
| Fire    | left click, `Space` or `Ctrl`                         | **FIRE** button (keep dragging to aim) |
| Weapons | `1` `2` `3`, `Q` cycles                               | **⇄** button                           |
| Reload  | `R` (also automatic on empty)                         | automatic                              |
| Pause   | `P` / `Esc` (also automatic when the tab loses focus) | **❙❙** button                          |
| Mute    | `M`                                                   | speaker icon                           |

Doors open as you approach. Red and blue doors need the matching key.

## Project Structure

```
iron-crypt/
├── IronCrypt.Client/
│   ├── Game/
│   │   ├── Engine.cs            # Core loop: player movement, doors, pickups, progression, seeds
│   │   ├── Engine.Combat.cs     # Weapons, magazines/reloads, damage taken, items, score
│   │   ├── Engine.Enemies.cs    # Enemy AI, pathfinding, hitscan, blood
│   │   ├── Level.cs             # Grid, door state, line of sight
│   │   ├── LevelGenerator.cs    # Seeded rooms + corridors, doors, locks/keys, spawns
│   │   ├── Renderer*.cs         # Raycaster: walls, sprites, weapon overlay, minimap, effects
│   │   └── Art*.cs              # Every texture and sprite, drawn in code
│   ├── Pages/Game.razor         # Loop glue, game states, HUD, portal score submit
│   ├── Components/              # Menu / pause / level-complete / game-over / leaderboard overlays
│   └── wwwroot/js/gameInterop.js  # rAF loop, input (keys, mouse, touch), canvas blit, WebAudio synth
├── IronCrypt.Tests/             # xUnit — engine, generator, renderer, bot playthroughs
├── nginx.conf                   # serves the WASM app; proxies scores/leaderboard to the portal
└── Notes/                       # Obsidian vault (design + tech notes)
```

## Portal Integration

Client-only, same contract as every portal game:

- Login happens at the portal; the JWT arrives via `#portal_token=` and is kept in `localStorage["jwt"]`.
- `nginx.conf` proxies `POST /api/scores`, `GET /api/scores/me` and `GET /api/leaderboard` to the portal auth server under the slug `iron-crypt`.
- A finished run submits `{ Value: score, Kills, Level: depth reached }`.
- `wwwroot/index.html` must keep the literal `<base href="/"` — the portal nginx rewrites it to `/iron-crypt/`.

See `Notes/Tech/Architecture.md` and `Notes/Tech/Engine.md` for the details.
