# Decisions

Product and technical choices that are not obvious from the code, with the reason. Newest at the bottom.

| Decision | Why |
|---|---|
| **Name: Iron Crypt** (Nov 2026) | Needs an original name; the slug (`iron-crypt`) is the repo, URL path and leaderboard key. |
| **Raycaster in C#, not JS** | Same language and structure as the other portal games; the whole game stays testable in `dotnet test`. |
| **No `Blazor.Extensions.Canvas`** (owner's call) | A textured raycaster writes tens of thousands of pixels per frame; one interop call per draw primitive cannot do that. One `byte[]` per frame can. |
| **Audio is in** (owner's call) | Orbit Break's "no sound effects" decision is specific to that game. All sound is synthesized: no files, no library. |
| **No asset files, no new libraries** | The original plan said generate in code and add nothing without approval. Textures, sprites, sounds and music are all code. |
| **Client-only, portal owns scores** | Hard rule for every portal game. Score payload maps to `{Value, Kills, Level}`: score, kills, depth reached. |
| **Never-dry pistol** | A run must not soft-lock with no ammo and no way to hurt anything. |
| **Auto-reload and fire-to-continue** | Phone players should not need a keyboard. |
| **Pause on focus loss** | Enemies keep hunting while a tab is in the background. Pausing is the only fair behaviour. |
| **Own seed hash (FNV-1a)** | `string.GetHashCode` is randomized per process, so typed seeds would not replay. |
| **Level seed = `seed·31 + depth·7919`** | Each depth is its own level but still determined by the one run seed. |
| **Locks are optional per level** | If no single door gates the target, forcing one would make the level unsolvable or pointless. A test asserts most levels still get one. |
| **Merge commit for the first game PR** | Kept the per-phase commit history on `main`. Later PRs use the usual squash. |
| **Fixed dev port 5310** | `dotnet run` otherwise used the .NET default 5000, which collides with other local services. Pinned in `Properties/launchSettings.json`. |
| **`.dockerignore` in this repo** | Without it a local `docker build` copies host `obj/`, and `dotnet publish --no-restore` fails with NETSDK1064. CI is unaffected. |
| **Compose dev port 8082** | 8080 = Bullet Heaven, 8081 = Orbit Break. |
