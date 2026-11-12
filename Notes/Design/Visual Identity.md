# Visual Identity

All art is generated in code (`Art*.cs`): no image files. The look is deliberately low-resolution and chunky: the game renders at **200 px on the short side** and the browser upscales it with `image-rendering: pixelated`.

## Palette

| Use | Color |
|---|---|
| Menu accent, title, borders | blood red `#ef4444` / dark red `#7f1d1d` |
| HUD numbers, messages, key text | amber `#fbbf24` / `#fde68a` |
| Health | red `#f87171` |
| Armor | green `#4ade80` |
| Page background | near-black `#07060a` |
| Locked doors | red `#c82828`, blue `#326ee6` |
| Exit | glowing green |

Orbit Break uses a violet/teal palette; Iron Crypt is deliberately red/amber so the portal's three games are easy to tell apart.

## Rendering style

- **Distance shading:** textures are pre-shaded into 16 brightness levels, darker with distance, so corridors fade into black. Walls facing along the y axis are one level darker, which gives walls depth without real lighting.
- **Sprites:** drawn on a 64 × 64 grid with a tiny pixel painter, each with a 1 px dark outline and a darker right edge so they read against dark walls.
- **Enemies** share a skeleton (legs, torso, head, arms) with per-type proportions and palette: grunt = brown with yellow claws, shooter = green with a red visor, tank = wide steel with cyan chest light.
- **Telegraphs** are part of the art: raised claws, a levelled rifle with a lit muzzle, raised fists.
- **Feedback:** the screen tints red when you are hit and fades to a deeper red when dead; blood droplets fly off enemies.

## UI

Menus reuse Orbit Break's overlay layout and class names (`menu-overlay`, `menu-buttons`, tooltips on hover) recolored for the crypt. The HUD is DOM text over the canvas, rebuilt only when a value changes.

## Where to change things

Textures: `Art.cs`. Pickup/exit sprites: `Art.Sprites.cs`. Enemies: `Art.Enemies.cs`. Weapons: `Art.Weapons.cs`. UI colors: `wwwroot/css/app.css`.
