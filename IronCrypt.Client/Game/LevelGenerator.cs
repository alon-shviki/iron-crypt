namespace IronCrypt.Client.Game;

/// <summary>
/// Procedural crypts: scattered rooms joined by L-shaped corridors, doors where a corridor pierces a room wall,
/// then locks and keys laid out so the level is always solvable. Fully deterministic per (seed, depth).
/// </summary>
public static class LevelGenerator
{
    public const int MinEnemyDistance = 7; // path cells from the start — nothing spawns in the player's face

    private record struct Room(int X, int Y, int W, int H)
    {
        public readonly int Cx => X + W / 2;
        public readonly int Cy => Y + H / 2;
    }

    public static Level Generate(int seed, int depth)
    {
        var rnd = new Random(seed);
        var size = Math.Min(30 + depth * 2, 46);
        var lvl = new Level(size, size);
        Array.Fill(lvl.Cells, (byte)1);

        // ── rooms: rejection-sampled, kept 2 cells apart so every room has its own wall ring ──
        var rooms = new List<Room>();
        var want = Math.Min(5 + depth, 11);
        for (var attempt = 0; attempt < 300 && rooms.Count < want; attempt++)
        {
            var w = rnd.Next(4, 9);
            var h = rnd.Next(4, 9);
            var x = rnd.Next(2, size - w - 2);
            var y = rnd.Next(2, size - h - 2);
            if (rooms.Any(r => x < r.X + r.W + 2 && r.X < x + w + 2 && y < r.Y + r.H + 2 && r.Y < y + h + 2)) continue;

            rooms.Add(new Room(x, y, w, h));
            var kind = (byte)rnd.Next(1, Cell.WallKinds + 1); // each room gets its own wall texture
            for (var yy = y - 1; yy <= y + h; yy++)
            {
                for (var xx = x - 1; xx <= x + w; xx++)
                {
                    var inside = xx >= x && xx < x + w && yy >= y && yy < y + h;
                    lvl.Cells[yy * size + xx] = inside ? Cell.Floor : kind;
                }
            }
        }
        if (rooms.Count < 2) return Generate(seed + 1, depth); // practically unreachable; keeps the contract total

        // ── corridors: chain room i → i+1, so everything is connected ──
        for (var i = 0; i + 1 < rooms.Count; i++)
        {
            var (a, b) = (rooms[i], rooms[i + 1]);
            if (rnd.Next(2) == 0)
            {
                CarveH(lvl, a.Cx, b.Cx, a.Cy);
                CarveV(lvl, a.Cy, b.Cy, b.Cx);
            }
            else
            {
                CarveV(lvl, a.Cy, b.Cy, a.Cx);
                CarveH(lvl, a.Cx, b.Cx, b.Cy);
            }
        }

        // ── doors: a corridor cell in a room's wall ring with solid jambs on both sides ──
        var doors = new List<int>();
        foreach (var r in rooms)
        {
            for (var x = r.X; x < r.X + r.W; x++)
            {
                TryDoor(lvl, rnd, doors, x, r.Y - 1, 1, 0, 0, -1);
                TryDoor(lvl, rnd, doors, x, r.Y + r.H, 1, 0, 0, 1);
            }
            for (var y = r.Y; y < r.Y + r.H; y++)
            {
                TryDoor(lvl, rnd, doors, r.X - 1, y, 0, 1, -1, 0);
                TryDoor(lvl, rnd, doors, r.X + r.W, y, 0, 1, 1, 0);
            }
        }

        // ── start in room 0, exit in the room farthest away by walking distance ──
        var start = rooms[0];
        lvl.StartX = start.Cx + 0.5;
        lvl.StartY = start.Cy + 0.5;
        lvl.StartAngle = Math.Round(Math.Atan2(rooms[1].Cy - start.Cy, rooms[1].Cx - start.Cx) / (Math.PI / 2)) * (Math.PI / 2);

        var open = Flood(lvl, start.Cx, start.Cy, true, true);
        var exitRoom = rooms.Skip(1).MaxBy(r => open[r.Cy * size + r.Cx]);
        lvl.ExitX = exitRoom.Cx;
        lvl.ExitY = exitRoom.Cy;

        var taken = new HashSet<int> { start.Cy * size + start.Cx, lvl.ExitY * size + lvl.ExitX };

        // ── locks: red gates the exit; from depth 2 a blue door gates the red key ──
        var redKey = PlaceLock(lvl, rnd, rooms, doors, taken, open, Cell.DoorRed, PickupKind.KeyRed, lvl.ExitY * size + lvl.ExitX);
        if (redKey >= 0 && depth >= 2)
            PlaceLock(lvl, rnd, rooms, doors, taken, open, Cell.DoorBlue, PickupKind.KeyBlue, redKey);

        Populate(lvl, rnd, rooms, taken, open, depth);
        return lvl;
    }

    /// <summary>
    /// Breadth-first walking distance from a cell (-1 = unreachable). Plain doors are passable;
    /// locked doors only when the matching flag is set. <paramref name="blocked"/> treats one extra cell as a wall.
    /// </summary>
    public static int[] Flood(Level lvl, int sx, int sy, bool red, bool blue, int blocked = -1)
    {
        var dist = new int[lvl.W * lvl.H];
        Array.Fill(dist, -1);
        var queue = new Queue<int>();
        dist[sy * lvl.W + sx] = 0;
        queue.Enqueue(sy * lvl.W + sx);
        Span<int> steps = stackalloc int[] { 1, -1, lvl.W, -lvl.W };
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var s in steps)
            {
                var n = cur + s;
                if (n < 0 || n >= dist.Length || dist[n] >= 0 || n == blocked) continue;
                var c = lvl.Cells[n];
                var passable = c == Cell.Floor || c == Cell.Door || (c == Cell.DoorRed && red) || (c == Cell.DoorBlue && blue);
                if (!passable) continue;
                dist[n] = dist[cur] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    private static void CarveH(Level lvl, int x0, int x1, int y)
    {
        for (var x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++) lvl.Cells[y * lvl.W + x] = Cell.Floor;
    }

    private static void CarveV(Level lvl, int y0, int y1, int x)
    {
        for (var y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++) lvl.Cells[y * lvl.W + x] = Cell.Floor;
    }

    // (lx, ly) = along the wall, (ox, oy) = pointing out of the room
    private static void TryDoor(Level lvl, Random rnd, List<int> doors, int x, int y, int lx, int ly, int ox, int oy)
    {
        if (lvl.At(x, y) != Cell.Floor) return;
        var jambs = lvl.At(x - lx, y - ly) is > 0 and < Cell.Door && lvl.At(x + lx, y + ly) is > 0 and < Cell.Door;
        if (!jambs || lvl.At(x + ox, y + oy) != Cell.Floor) return; // outside must be open corridor, not another door
        if (rnd.Next(5) == 0) return;                              // leave the odd archway doorless
        lvl.Cells[y * lvl.W + x] = Cell.Door;
        doors.Add(y * lvl.W + x);
    }

    /// <summary>
    /// Turns one plain door into a locked one such that <paramref name="target"/> is unreachable without the key,
    /// then drops the key on the start side of it. Returns the key's cell, or -1 when no door gates the target.
    /// </summary>
    private static int PlaceLock(Level lvl, Random rnd, List<Room> rooms, List<int> doors, HashSet<int> taken,
        int[] open, byte lockKind, PickupKind key, int target)
    {
        var sx = (int)lvl.StartX;
        var sy = (int)lvl.StartY;
        var gate = -1;
        foreach (var d in doors)
        {
            if (lvl.Cells[d] != Cell.Door) continue;
            if (Flood(lvl, sx, sy, false, false, d)[target] >= 0) continue;  // target still reachable around it
            if (gate < 0 || open[d] > open[gate]) gate = d;                  // prefer the gate deepest into the level
        }
        if (gate < 0) return -1;
        lvl.Cells[gate] = lockKind;

        // key goes in the farthest room still reachable without it (the start room if nothing else is)
        var side = Flood(lvl, sx, sy, false, false);
        var room = rooms.Where(r => side[r.Cy * lvl.W + r.Cx] >= 0).MaxBy(r => side[r.Cy * lvl.W + r.Cx]);
        var cell = TakeCell(lvl, rnd, room, taken);
        lvl.Pickups.Add(new PickupSpawn(key, cell % lvl.W + 0.5, cell / lvl.W + 0.5));
        return cell;
    }

    /// <summary>A random unused floor cell inside the room; marks it used so nothing ever stacks.</summary>
    private static int TakeCell(Level lvl, Random rnd, Room r, HashSet<int> taken)
    {
        for (var i = 0; i < 64; i++)
        {
            var cell = rnd.Next(r.Y, r.Y + r.H) * lvl.W + rnd.Next(r.X, r.X + r.W);
            if (taken.Add(cell)) return cell;
        }
        return -1; // room is full
    }

    private static void Populate(Level lvl, Random rnd, List<Room> rooms, HashSet<int> taken, int[] open, int depth)
    {
        void Drop(PickupKind kind, Room room)
        {
            var cell = TakeCell(lvl, rnd, room, taken);
            if (cell >= 0) lvl.Pickups.Add(new PickupSpawn(kind, cell % lvl.W + 0.5, cell / lvl.W + 0.5));
        }

        Drop(PickupKind.Bullets, rooms[0]);
        Drop(PickupKind.Shotgun, rooms[1 + rnd.Next(Math.Min(2, rooms.Count - 1))]); // early, so it is found
        if (depth >= 2) Drop(PickupKind.Chaingun, rooms[rnd.Next(1, rooms.Count)]);
        for (var i = 1; i < rooms.Count; i++)
        {
            if (rnd.Next(100) < 55) Drop(PickupKind.Bullets, rooms[i]);
            if (rnd.Next(100) < 35) Drop(PickupKind.Shells, rooms[i]);
            if (rnd.Next(100) < 40) Drop(PickupKind.Health, rooms[i]);
            if (rnd.Next(100) < 18) Drop(PickupKind.Armor, rooms[i]);
        }

        // ── enemies: more and nastier with depth, never close to the start ──
        var count = Math.Min(5 + depth * 3, 40);
        var tankChance = depth < 2 ? 0 : Math.Min(5 * depth, 25);
        var shooterChance = Math.Min(15 + 5 * depth, 45);
        for (var attempt = 0; attempt < count * 6 && lvl.Enemies.Count < count; attempt++)
        {
            var room = rooms[rnd.Next(1, rooms.Count)];
            var cell = rnd.Next(room.Y, room.Y + room.H) * lvl.W + rnd.Next(room.X, room.X + room.W);
            if (open[cell] < MinEnemyDistance || !taken.Add(cell)) continue;
            var roll = rnd.Next(100);
            var kind = roll < tankChance ? EnemyKind.Tank : roll < tankChance + shooterChance ? EnemyKind.Shooter : EnemyKind.Grunt;
            lvl.Enemies.Add(new EnemySpawn(kind, cell % lvl.W + 0.5, cell / lvl.W + 0.5));
        }
    }
}
