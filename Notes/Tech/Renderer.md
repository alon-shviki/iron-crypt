# Renderer

Software raycaster in `Game/Renderer*.cs`. It only fills arrays, so tests can render a frame. The page hands the result to JS ([[Tech/Input and Interop]]). Design side: [[Design/Visual Identity]].

## Pipeline per frame

1. **Floor and ceiling** as flat rows, shaded by distance from the horizon.
2. **Walls:** for each screen column cast a ray with DDA over the grid. Record the distance in a z-buffer, then draw the textured column.
3. **Sprites** (pickups, exit, enemies, blood): see below.
4. **Overlay** (only in play): weapon, muzzle flash, crosshair, minimap.
5. **Tint** red if hurt or dead.
6. Copy the 32-bit pixels into a `byte[]` for `putImageData`.

## Resolution

`Renderer.SizeFor(viewW, viewH)`: 200 px on the short side, the long side follows the aspect ratio, clamped to 480 (landscape) or 440 (portrait), kept even. Recomputed every frame so rotating a phone or resizing the window just works. The canvas is CSS-stretched with pixelated scaling.

## Walls

- Field of view is fixed by `PlaneLen = 0.66` (about 66° horizontal).
- Textures are 64 × 64, stored **column-major** (`x * 64 + y`) so the column loop reads memory sequentially, and **pre-shaded** into 16 brightness levels (`Art.Shaded`), so the inner loop is one lookup and one store. Texture stepping uses 16.16 fixed point.
- Distance is perpendicular (no fisheye).
- **Doors** are handled inside the DDA: half a cell in, test where the ray crosses the slab, and skip the slab if the ray passes through the open part. The texture offset slides with the open fraction.

## Sprites

Camera-space projection using the inverse of the camera matrix, then:
- cull anything behind the camera,
- sort far to near,
- per screen column, skip if the wall is closer (z-buffer),
- skip transparent texels (alpha 0).

Each sprite has a world `Size` (1 = full wall height) and `Lift` above the floor: keys hover and bob, blood droplets arc.

## Effects

- **Screen shake:** shifts the horizon by a few pixels while `Engine.Shake` > 0.
- **Damage flash:** whole-frame tint toward red proportional to `HurtFlash`.
- **Title backdrop:** the same renderer with the overlay off, slowly turning.

## Cost

About 3 ms of C# per frame (Release build, no AOT) at 386 × 200, 2.3 ms for a phone portrait frame and 3.7 ms at 480 × 200. 60 FPS has roughly 5× headroom. Details: [[Tech/Performance]].

## Known simplifications

Flat floor/ceiling (a `ponytail:` comment marks the upgrade to floor casting; GitHub issue open), no sprite lighting beyond distance shade, and sprites are always camera-facing (no rotation frames).
