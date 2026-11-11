# Architecture

Matches the pattern of the other portal games (Bullet Heaven, Orbit Break). See [[Tech/Engine]] for the game code layout.

## Stack

| Layer | Technology |
|-------|-----------|
| Game client | Blazor WASM (.NET 10), `IronCrypt.Client/` |
| Rendering | C# raycaster → RGBA framebuffer → `putImageData` in `wwwroot/js/gameInterop.js` (CSS upscales, `image-rendering: pixelated`) |
| Game loop | `requestAnimationFrame` → JS interop → C# `Tick(timestamp, input[])` |
| Audio | WebAudio synthesis in `gameInterop.js` — no audio files |
| Assets | None. Wall textures, sprites and the weapon are drawn in code (`Game/Art.cs`) |
| Tests | `IronCrypt.Tests/` — headless xunit tests against the engine |
| Auth / Scores / Leaderboard | Portal auth server — no game-specific backend or DB |

**Why not `Blazor.Extensions.Canvas`** (what BH and OB use): a textured raycaster writes tens of thousands of pixels per frame; one interop call per draw primitive cannot do that. One `byte[]` handed to JS per frame can.

## Portal Integration Contract

Same contract every game follows — see the portal's `Tech/Architecture.md`:

- No `/register`/`/login` of its own — JWT arrives via `#portal_token=` URL hash (picked up in `index.html`, stored in `localStorage["jwt"]`).
- `index.html` keeps the literal `<base href="/"` (portal nginx `sub_filter` rewrites it to `/iron-crypt/`).
- `nginx.conf` proxies:
  - `POST /api/scores` → `portal-auth:5001/api/scores/iron-crypt`
  - `GET /api/scores/me` → `portal-auth:5001/api/leaderboard/iron-crypt/me`
  - `GET /api/leaderboard` → `portal-auth:5001/api/leaderboard/iron-crypt`
- Score payload maps to the portal's generic `{ Value, Kills, Level }`: Value = score, Kills = enemies killed, Level = crypt depth reached.
- Local high score lives in `localStorage["ic_highscore"]`, mute preference in `localStorage["ic_muted"]`.

## Build & Run

See `CLAUDE.md` → Commands. `IronCrypt.Client/Dockerfile` produces the nginx-served image. CI is `.github/workflows/ci.yml`, a thin caller of the portal's shared reusable workflow — cache→format→build→test and, on push to `main`, pushes `ghcr.io/alon-shviki/iron-crypt-client:latest`. Required check: `ci / build`.

Portal wiring: service `iron-crypt-client` (dev port **8082**), path `/iron-crypt/`, script slug `ic`.
