# Trickshots

Style bonuses on top of the normal kill score. Each one is judged when an enemy **dies**, they stack, and the callout appears in the middle of the screen with a rising chime (for example `HEADSHOT + LONG SHOT  +125`). All numbers are constants at the top of `Engine.Combat.cs`; the logic is `AwardTricks`.

| Trick | How | Bonus |
|---|---|---|
| **HEADSHOT** | The killing bullet hits the top quarter of the enemy. Headshots also do double damage and play a "ding" even when they do not kill. | +50 |
| **BANK SHOT** | The killing bullet bounced off a wall first. | +150 |
| **LONG SHOT** | The bullet travelled 9 cells or more (bounces count toward the distance). | +75 |
| **SPIN SHOT** | Kill right after a fast spin (about a full 360° within half a second). Holding an arrow key is too slow to count. | +200 |
| **DOUBLE KILL** | Two kills from one trigger pull or one explosion. | +100 |
| **MULTI KILL** | Three or more from one pull or blast (awarded on the third and each one after). | +200 |

`Engine.Tricks` counts how many bonuses a run has earned.

## The rules that make them possible

- **Looking up and down.** The mouse (or a finger drag) tilts the view up to a slope of ±0.3. A shot rises or falls with that tilt, so it has a height when it reaches an enemy: over its head is a miss, the top quarter is the head, the floor and ceiling stop bullets.
- **Level aim is a body shot**, but only just: eye height is 0.5 and a grunt's head starts at 0.54. Nudge the aim up for the head. Taller enemies need more.
- **Bullets ricochet once.** Every bullet that reaches a wall reflects off it and keeps going at 60% damage, then stops at the next wall. That is what makes shots around corners possible, and it also means a point-blank shot into a wall comes straight back past you.
- **Shotgun pellets** each bounce and each can be a headshot; the spread is a cone (sideways and vertical).
- **Rockets** do not bounce and cannot headshot, but their blast is the easiest way to a double or multi kill.

## Design intent

Reward *how* you kill, not just how many: the score chase gets a skill ceiling without changing the base difficulty. Nothing here is required to finish a level.

## Ideas

A combo multiplier for chained kills and on-screen hit markers are open GitHub issues (#8). Possible future tricks: kill while a charger is mid-charge, kill with your own ricochet after dodging, point-blank shotgun kill.
