# Level Design

What a generated crypt looks like and the rules the player experiences. How it is built: [[Tech/Level Generator]].

## Shape

Scattered rectangular rooms (4-8 cells a side) connected by 1-cell-wide L-shaped corridors. Every room has its own wall texture (brick, stone, riveted metal, mossy stone), which doubles as a landmark: "the green room".

## Doors

- Doors are thin slabs that **slide sideways** as you come within 1.5 cells, stay open 3 seconds, then close again. A door never closes on a body.
- A doorway only exists where a corridor meets a room wall with solid walls on both sides (the "jambs"), otherwise it would look wrong.
- A few archways are left doorless on purpose.
- **Locked doors** are red or blue, with a colored band across the door. Walking up to one without the key shows "You need the red key" and plays a refusal sound.

## Keys and the exit

1. The **exit** is a glowing green archway in the room farthest from the start by walking distance.
2. The **red door** is placed on a door that cuts the exit off from the start. The red key lies in the farthest room still reachable without it.
3. From depth 2 a **blue door** does the same for the red key.
4. If no single door gates the target, that level simply has no lock. Most levels do.

Result: a chain start → (blue key) → red key → exit that forces exploring without ever being unsolvable.

## The minimap

21 × 21 cells around the player, showing walls, doors (colored when locked), the exit in green, and the player with a facing marker. It shows the real layout, not just what you have seen: this is a pacing choice (the game is about fighting, not getting lost) and could become a modifier later.

## Seeds

A level is `(seed, depth)`. Same seed means the same maps, keys and pickups every time. See [[Design/Core Loop]].

## Possible additions

Secret rooms (GitHub issue), torch-lit and dark rooms, and per-room themes beyond wall textures.
