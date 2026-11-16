# Enemies

Four types, all defined in `Engine.EnemyDefs` (`Engine.Enemies.cs`) and drawn in `Art.Enemies.cs`. How they think: [[Tech/Enemy AI]].

| | Grunt | Shooter | Tank | Charger |
|---|---|---|---|---|
| Role | melee rusher | ranged | slow bruiser | straight-line rammer |
| Health | 40 | 30 | 180 | 60 |
| Speed (cells/s) | 2.3 | 1.7 | 1.05 | 2.0 (8 while charging) |
| Damage | 10 | 8 | 30 | 25 |
| Attack range | 0.9 | 9.0 | 1.1 | starts a charge within 7 |
| Wind-up (telegraph) | 0.30 s | 0.60 s | 0.55 s | 0.60 s + roar |
| Rest between attacks | 0.7 s | 1.1 s | 1.0 s | 1.4 s |
| Flinch chance when hit | 60% | 70% | 20% | 30% |
| Score | 100 | 150 | 400 | 250 |
| First appears | depth 1 | depth 1 | depth 2 | depth 2 |

## What each one asks of the player

- **Grunt:** raises its claws before swiping. The wind-up is short, but stepping back *during* it dodges the hit entirely.
- **Shooter:** keeps its distance (holds at 60% of its range once it has a clear shot), raises its rifle (the telegraph), then fires. Accuracy falls with distance (85% close, never below 25%). Breaking line of sight during the wind-up cancels the shot, which is why corners matter.
- **Tank:** soaks damage, barely flinches, and one hit costs 30 health. Meant to be kited around walls or taken down with the shotgun/chaingun.
- **Charger:** horned and purple. When it sees you within 7 cells it roars, lowers its head for 0.6 s, then sprints in a **straight line** at 8 cells/s toward where you were standing. It cannot steer: sidestep and it rams the wall and is **stunned for 0.9 s**, which is the moment to punish it. It is faster than you, so running straight away does not work.

**Heads:** every enemy's top quarter is its head. A headshot does double damage ([[Design/Trickshots]]).

## Shared behaviour

- Enemies start **idle** and wake when they see you (within 12 cells, with line of sight), hear a gunshot (within 16 walking cells), or are shot.
- They chase around walls, open plain doors, and are held back by locked doors until you have the key.
- They collide with walls, you and each other, so a crowd queues up in corridors.
- A flinch (Pain state) interrupts an attack, so fast weapons can stun-lock weak enemies.

## Scaling

+8% health and +5% damage per depth below the first. Spawn counts and the shooter/tank share grow with depth: see [[Design/Difficulty]].
