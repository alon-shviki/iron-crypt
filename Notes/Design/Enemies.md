# Enemies

Three types, all defined in `Engine.EnemyDefs` (`Engine.Enemies.cs`) and drawn in `Art.Enemies.cs`. How they think: [[Tech/Enemy AI]].

| | Grunt | Shooter | Tank |
|---|---|---|---|
| Role | melee rusher | ranged | slow bruiser |
| Health | 40 | 30 | 180 |
| Speed (cells/s) | 2.3 | 1.7 | 1.05 |
| Damage | 10 | 8 | 30 |
| Attack range | 0.9 | 9.0 | 1.1 |
| Wind-up (telegraph) | 0.30 s | 0.60 s | 0.55 s |
| Rest between attacks | 0.7 s | 1.1 s | 1.0 s |
| Flinch chance when hit | 60% | 70% | 20% |
| Score | 100 | 150 | 400 |
| First appears | depth 1 | depth 1 | depth 2 |

## What each one asks of the player

- **Grunt:** raises its claws before swiping. The wind-up is short, but stepping back *during* it dodges the hit entirely.
- **Shooter:** keeps its distance (holds at 60% of its range once it has a clear shot), raises its rifle (the telegraph), then fires. Accuracy falls with distance (85% close, never below 25%). Breaking line of sight during the wind-up cancels the shot, which is why corners matter.
- **Tank:** soaks damage, barely flinches, and one hit costs 30 health. Meant to be kited around walls or taken down with the shotgun/chaingun.

## Shared behaviour

- Enemies start **idle** and wake when they see you (within 12 cells, with line of sight), hear a gunshot (within 16 walking cells), or are shot.
- They chase around walls, open plain doors, and are held back by locked doors until you have the key.
- They collide with walls, you and each other, so a crowd queues up in corridors.
- A flinch (Pain state) interrupts an attack, so fast weapons can stun-lock weak enemies.

## Scaling

+8% health and +5% damage per depth below the first. Spawn counts and the shooter/tank share grow with depth: see [[Design/Difficulty]].
