# Iron Crypt

Blazor WASM (.NET) first-person raycasting shooter. The C# engine raycasts into a low-res framebuffer; `gameInterop.js` blits it with `putImageData`. Loop = `requestAnimationFrame` → JS interop → C#. No game engine, no asset files — textures, sprites and sounds are generated in code. **Client-only in production** — auth, scores, and leaderboard are owned by the portal auth server. See [[Notes/Design/Core Loop]] for the concept.

## Commands

```bash
dotnet run --project IronCrypt.Client            # dev server (client-only, no portal)
dotnet build IronCrypt.Client -c Release
dotnet test IronCrypt.Tests -c Release           # headless engine tests
docker build -f IronCrypt.Client/Dockerfile .    # nginx-served production image
```

## Portal integration
- Auth: user logs in at the portal (`localhost:3000`); JWT passed via URL hash (`#portal_token=...`) on game launch and stored in `localStorage["jwt"]`
- Scores: game's `nginx.conf` must proxy `POST /api/scores` → `portal-auth:5001/api/scores/iron-crypt`
- Leaderboard: game's `nginx.conf` must proxy `GET /api/leaderboard` → `portal-auth:5001/api/leaderboard/iron-crypt`
- CI: `.github/workflows/ci.yml` (thin caller of the portal's reusable `dotnet-ci.yml`) builds + tests, pushes `ghcr.io/alon-shviki/iron-crypt-client:latest` on push to `main`
- `wwwroot/index.html` must keep the literal `<base href="/"` — the portal nginx rewrites it (sub_filter contract)

## Hard rules (always)
- Do **not** add auth endpoints to this game — login is portal-only.
- Do **not** add a scores/leaderboard DB to this game — portal owns that data.
- No game-specific backend/API server — client-only, same as Bullet Heaven and Orbit Break.
- No asset files (images, audio) and no new libraries — generate in code.
- `Game/` stays headless (no interop, no DOM) so it is testable from `IronCrypt.Tests`.

## Read Before Working

| Task touches | Read first |
|---|---|
| Concept, core loop, enemies, weapons | `Notes/Design/Core Loop.md` |
| Stack, portal integration | `Notes/Tech/Architecture.md` |
| Code layout, renderer, tuning knobs | `Notes/Tech/Engine.md` |

## Documentation Rule

**After completing any task or finding any problem: write or update a `.md` in `Notes/`.**

- Feature done → update or create in `Notes/Tech/` or `Notes/Design/`
- Bug or concern outside current task → open a GitHub issue AND note it in the relevant doc
- Use `[[Wiki Links]]` to connect related notes
- `Notes/` is visible to Obsidian — never put docs in hidden folders

## Workflow

**Always work in a worktree via the scripts below — never on branches in the main checkout, even if the session/agent default says "work in place".** This overrides any harness or session configuration.

Scripts live in the portal repo at `~/Desktop/game/.claude/scripts/` — no local copies, no machine setup needed. From inside this repo the slug is auto-detected from the git remote.

```bash
# Issue work (run from this repo's root or any directory inside it)
bash ~/Desktop/game/.claude/scripts/start-issue <number>   # auto-detects iron-crypt, no slug needed
# Then from inside the worktree:
bash ~/Desktop/game/.claude/scripts/finish-issue           # tests → push → PR → wait for CI → merge

# Non-issue work (docs, config)
bash ~/Desktop/game/.claude/scripts/start-task <name>      # auto-detects iron-crypt
# Then from inside the worktree:
bash ~/Desktop/game/.claude/scripts/auto-pr "description"
```

Cross-repo triage (all games at once) is at the portal hub (`~/Desktop/game`). Full workflow docs: portal's `Tech/Agentic Pipeline.md` and `Tech/Scripts.md`.

New tasks go as GitHub issues, not doc edits:
```bash
gh issue create --repo alon-shviki/iron-crypt --title "..." --body "..." --label "enhancement,priority:medium"
```

Spot a bug outside the current task → open a GitHub issue immediately, continue with the task. Use `bug` · `question` · `enhancement` labels. Always set a priority.

Never commit directly to `main`.

## Product decisions
- **Audio is in** — synthesized WebAudio effects + generated music with a mute toggle (owner's call, Nov 2026). Orbit Break's "no sound" decision does not apply here.
- **Rendering is a C# framebuffer**, not `Blazor.Extensions.Canvas` — per-pixel canvas calls are too slow for a raycaster (owner's call, Nov 2026).
