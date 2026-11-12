# Controls and UI

## Desktop

| Action | Input |
|---|---|
| Move | `W` `A` `S` `D` |
| Look | mouse (click the game once to capture it) or `←` `→` |
| Fire | left click, `Space` or `Ctrl` |
| Weapon | `1` `2` `3`, `Q` cycles |
| Reload | `R` |
| Pause | `P` or `Esc` (also when the mouse is released or the tab loses focus) |
| Mute | `M` or the speaker icon |

## Touch

Controls appear after the first touch (the page adds `body.touch`).

- **Left half of the screen:** a floating move stick, anchored where the thumb lands.
- **Right half:** drag to look.
- **FIRE button** bottom right; the thumb that holds it can keep dragging to aim.
- **⇄** cycles weapons, **❙❙** pauses. Reload is automatic.

## Screens

- **Title:** logo, "How to play" tooltip, seed box (empty = random), Start Game, Leaderboard, sign-in status, best score.
- **HUD:** top-left depth and seed (+ key icons), bottom bar with health, armor, ammo (`mag / reserve` or `RELOADING`) and score, minimap top right, short messages ("Red key", "Shotgun!") in the upper middle, FPS counter top-left.
- **Paused:** frozen frame, Resume / Main Menu. Audio is suspended too.
- **Level complete:** kills, bonus, score, "Descend". Fire (after a short delay) also continues, so a touch player does not need to find a button.
- **Game over:** score, best, the player's best runs from the portal, Retry This Seed / New Crypt / Leaderboard / Main Menu.
- **Leaderboard:** top scores from the portal (Kills and Depth columns).

## Small screens

Below 640 px wide the title, buttons, HUD and leaderboard shrink so a phone held upright fits. The internal resolution follows the viewport aspect ratio ([[Tech/Renderer]]).

## Principles

- Nothing important depends on a keyboard: auto-reload, one-thumb weapon cycling, fire to continue.
- Losing focus never lets you die while away: it pauses.
- The cursor is captured only in play; every menu gives it back.
