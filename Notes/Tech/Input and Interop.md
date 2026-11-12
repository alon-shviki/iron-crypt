# Input and Interop

`wwwroot/js/gameInterop.js` is the only JS. `Pages/Game.razor` is the only caller. One call in, one call out per frame.

## Frame contract

```
rAF ─▶ dotnetRef.invokeMethodAsync('Tick', timestamp, readInput())      (JS → C#, input array rides along)
Tick ─▶ Engine.Tick + Renderer.Render
     ─▶ gameInterop.present(bytes, w, h, soundIds)                      (C# → JS, in-process call)
```

A **busy guard** in the loop skips a frame instead of queueing overlapping ticks if C# is still working; the skipped time is absorbed by `dt` (which the engine clamps to 50 ms so a long frame can never tunnel through a wall). `Game.razor` casts the runtime to `IJSInProcessRuntime` so `present` is a synchronous call with no promise per frame.

## Input array (`readInput`)

| Index | Meaning |
|---|---|
| 0 | forward (-1..1, keyboard or touch stick) |
| 1 | strafe (-1..1) |
| 2 | key turn (-1, 0, 1 from ← →) |
| 3 | mouse/touch-look delta in pixels since last frame |
| 4 | fire held |
| 5 | weapon request (1-3 select, -1 cycle, 0 none), edge |
| 6 | reload, edge |
| 7 | pause, edge (P / Esc / pause button) |
| 8 | mute, edge |
| 9, 10 | viewport width, height (drives the internal resolution) |
| 11 | forced pause, edge (focus, visibility or pointer lock lost) |

"Edge" values are latched in JS and cleared when read, so a quick tap between frames is never lost.

## Mouse look

Pointer lock on the canvas. Any element with `data-lock` (the canvas, Start, Resume, Retry) requests the lock on click. Losing the lock (Esc) triggers a forced pause; menus call `releasePointer()` so the cursor comes back.

## Touch

`initTouch()` tracks touches by identifier: left half = move stick (radius 55 px, anchored at the touch-down point), right half = look drag (2.4 × mouse pixels), buttons carry `data-touch="fire|weapon|pause"`. The FIRE thumb doubles as the look finger. The first touch adds `body.touch`, which reveals the buttons and hides the cursor.

## Focus loss

`blur` and `visibilitychange` clear held keys and set the forced-pause edge; hiding the tab also suspends audio immediately (rAF stops when hidden, so C# cannot do it).

## Portal bridge

`index.html` copies `#portal_token=` from the URL hash into `localStorage["jwt"]` and strips it from the address bar. The game then reads the JWT in `Game.razor` for the username and for score calls. See [[Tech/Architecture]].

## Local storage keys

`jwt` (portal), `ic_highscore`, `ic_muted`.
