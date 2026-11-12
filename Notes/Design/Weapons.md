# Weapons

Three guns, picked with `1` `2` `3` (or `Q` / the ⇄ touch button to cycle through the ones you own). All numbers live in `Engine.Weapons` (`Engine.Combat.cs`). Loop context: [[Design/Core Loop]].

| | Pistol | Shotgun | Chaingun |
|---|---|---|---|
| Damage per hit | 14 | 9 × 7 pellets | 11 |
| Spread (rad) | 0.012 | 0.085 | 0.035 |
| Seconds between shots | 0.32 | 0.85 | 0.095 |
| Magazine | 12 | 6 | 50 |
| Reload | 1.0 s | 1.5 s | 2.0 s |
| Ammo pool | bullets | shells | bullets |
| Best-case damage/s | ~44 | ~74 (all pellets, point blank) | ~116 |
| Found | start | depth 1 (room 1 or 2) | from depth 2 |

## Rules

- **Hitscan.** A shot is a ray; it hits the nearest enemy along it that is closer than the first wall or closed door. A shotgun fires 7 rays with random spread.
- **Magazines and reserve.** Ammo is split into what is in the gun and a reserve per pool (`MaxBullets` 200, `MaxShells` 50). The pistol and chaingun share the bullet pool.
- **Reload** starts on `R`, or automatically when the magazine empties (nobody should need a key on a phone). Firing is blocked while reloading. Switching weapons cancels a reload and takes `SwitchTime` (0.25 s) during which you cannot fire.
- **Pistol is never dry.** If the bullet reserve is empty it still refills its magazine for free, so a run can never soft-lock with no way to hurt anything. The other guns stop when out of ammo (click sound).
- **Start:** pistol, full magazine, 36 bullets in reserve.
- **Picking up a gun you own** just gives its ammo (shotgun +8 shells, chaingun +40 bullets). A new gun is auto-selected.

## Feel

- **Muzzle flash** for 0.06 s, **recoil** kicks the sprite (shotgun hardest), walking **sways** the weapon in a shallow U, and reloading dips it off-screen.
- The shotgun also shakes the view slightly.
- Each gun has its own synthesized sound ([[Tech/Audio]]).

## Ideas not built yet

Rocket launcher, hit markers, knockback: see the open issues on GitHub (#8, #9).
