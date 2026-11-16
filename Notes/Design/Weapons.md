# Weapons

Four weapons, picked with `1` `2` `3` `4` (or `Q` / the ⇄ touch button to cycle through the ones you own). All numbers live in `Engine.Weapons` (`Engine.Combat.cs`). Loop context: [[Design/Core Loop]].

| | Pistol | Shotgun | Chaingun | Rocket launcher |
|---|---|---|---|---|
| Damage per hit | 14 | 9 × 7 pellets | 11 | 120 splash (centre) |
| Spread (rad) | 0.012 | 0.085 | 0.035 | 0 |
| Seconds between shots | 0.32 | 0.85 | 0.095 | 0.9 |
| Magazine | 12 | 6 | 50 | 4 |
| Reload | 1.0 s | 1.5 s | 2.0 s | 2.2 s |
| Ammo pool | bullets | shells | bullets | rockets |
| Best-case damage/s | ~44 | ~74 (all pellets, point blank) | ~116 | ~133 per target in the blast |
| Found | start | depth 1 (room 1 or 2) | from depth 2 | from depth 3 |

## Rules

- **Hitscan with height.** A bullet is a ray that follows where you look, up and down included. It hits the nearest enemy in its path; over the enemy's head is a miss; the **top quarter is a headshot for double damage**. The floor and ceiling stop it. A shotgun fires 7 rays in a cone.
- **One ricochet.** A bullet that reaches a wall bounces off once at 60% damage. See [[Design/Trickshots]].
- **Rockets** are real projectiles (10 cells/s) that follow your pitch and explode on walls, floor, ceiling or a body. The blast does up to 120 damage, falling to zero at 2.3 cells, needs line of sight, and **hurts you too** (half damage). Rocket ammo: +4 per pickup, 20 max, from depth 3.
- **Magazines and reserve.** Ammo is split into what is in the gun and a reserve per pool (`MaxBullets` 200, `MaxShells` 50, `MaxRockets` 20). The pistol and chaingun share the bullet pool.
- **Reload** starts on `R`, or automatically when the magazine empties (nobody should need a key on a phone). Firing is blocked while reloading. Switching weapons cancels a reload and takes `SwitchTime` (0.25 s) during which you cannot fire.
- **Pistol is never dry.** If the bullet reserve is empty it still refills its magazine for free, so a run can never soft-lock with no way to hurt anything. The other guns stop when out of ammo (click sound).
- **Start:** pistol, full magazine, 36 bullets in reserve.
- **Picking up a gun you own** just gives its ammo (shotgun +8 shells, chaingun +40 bullets, launcher +4 rockets). A new gun is auto-selected.

## Feel

- **Muzzle flash** for 0.06 s, **recoil** kicks the sprite (shotgun hardest), walking **sways** the weapon in a shallow U, and reloading dips it off-screen.
- The shotgun also shakes the view slightly.
- Each gun has its own synthesized sound ([[Tech/Audio]]).

## Ideas not built yet

Hit markers, knockback and a kill combo: see the open issue on GitHub (#8).
