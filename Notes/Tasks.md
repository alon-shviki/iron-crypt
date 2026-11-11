# Tasks

Built from the owner's 8-phase plan (Nov 2026). New work goes in as GitHub issues, not edits here.

- [x] Repo scaffold: CLAUDE.md, vault, CI caller, nginx contract
- [x] Phase 1 — engine core: raycaster with distance shading, movement + collision, mouse look, procedural wall textures, minimap, FPS counter
- [x] Phase 2 — levels: seeded room/corridor generator, sliding doors, red/blue keys, exit
- [x] Phase 3 — combat: pistol, shotgun, chaingun; magazines + reload timing; ammo/health/armor pickups; muzzle flash, weapon sway
- [x] Phase 4 — enemies: code-drawn depth-sorted sprites; grunt, shooter, tank; idle/chase/attack AI, line of sight, pathfinding
- [x] Phase 5 — audio: synthesized effects, generated music, mute toggle
- [x] Phase 6 — flow + polish: title, pause, level-complete, game over; difficulty scaling; damage flash, shake, blood; portal high score
- [x] Phase 7 — portal integration: nginx/compose/shell card/leaderboard tab; auto-resize, touch controls, pause on focus loss
- [x] Phase 8 — testing: 44 headless tests incl. bot playthroughs; 60 FPS measured on the Release image

## Next (open as issues when picked up)

- Playtest on a real phone; tune touch stick radius and look speed
- Listen to the synth: tune effect volumes and the music generator
- Balance pass: ammo scarcity, enemy mix per depth
- Textured floor/ceiling (floor casting) if the flat shading feels bare

See [[Tech/Engine]] for where each knob lives.
