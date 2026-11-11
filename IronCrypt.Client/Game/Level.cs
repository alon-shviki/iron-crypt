namespace IronCrypt.Client.Game;

/// <summary>Grid cell ids. 0 = floor, 1..WallKinds = wall textures, 16+ = doors.</summary>
public static class Cell
{
    public const byte Floor = 0;
    public const int WallKinds = 4;
    public const byte Door = 16, DoorRed = 17, DoorBlue = 18;

    public static bool IsDoor(byte c) => c >= Door;
}

public enum EnemyKind { Grunt, Shooter, Tank }

public enum PickupKind { Health, Armor, Bullets, Shells, Shotgun, Chaingun, KeyRed, KeyBlue }

public record struct EnemySpawn(EnemyKind Kind, double X, double Y);

public record struct PickupSpawn(PickupKind Kind, double X, double Y);

/// <summary>One map: wall grid, door state, start, exit and what spawns where. Pure data — no rendering.</summary>
public class Level
{
    public readonly int W, H;
    public readonly byte[] Cells;
    public readonly float[] DoorOpen;   // 0 = closed .. 1 = fully slid open (door cells only)
    public readonly float[] DoorTimer;  // seconds until an open door starts closing
    public double StartX, StartY, StartAngle;
    public int ExitX = -1, ExitY = -1;
    public readonly List<EnemySpawn> Enemies = new();
    public readonly List<PickupSpawn> Pickups = new();

    public Level(int w, int h)
    {
        W = w;
        H = h;
        Cells = new byte[w * h];
        DoorOpen = new float[w * h];
        DoorTimer = new float[w * h];
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

    /// <summary>Out of bounds reads as solid wall, so nothing ever needs a bounds check.</summary>
    public byte At(int x, int y) => InBounds(x, y) ? Cells[y * W + x] : (byte)1;

    /// <summary>Blocks movement: walls, and doors until they are almost fully open.</summary>
    public bool Blocks(int x, int y)
    {
        var c = At(x, y);
        if (c == Cell.Floor) return false;
        return !Cell.IsDoor(c) || DoorOpen[y * W + x] < 0.8f;
    }

    /// <summary>Blocks sight and bullets: walls, and doors that are less than half open.</summary>
    public bool BlocksSight(int x, int y)
    {
        var c = At(x, y);
        if (c == Cell.Floor) return false;
        return !Cell.IsDoor(c) || DoorOpen[y * W + x] < 0.5f;
    }

    /// <summary>True when the straight segment between two points crosses no sight-blocking cell.</summary>
    public bool LineOfSight(double x0, double y0, double x1, double y1)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        // ponytail: sampled every 1/8 cell — switch to an exact grid walk if thin corners ever leak
        var steps = (int)(Math.Max(Math.Abs(dx), Math.Abs(dy)) * 8) + 1;
        for (var i = 1; i < steps; i++)
        {
            var t = (double)i / steps;
            if (BlocksSight((int)(x0 + dx * t), (int)(y0 + dy * t))) return false;
        }
        return true;
    }

    /// <summary>
    /// Builds a level from text rows — used by tests.
    /// '#' wall, '.' floor, 'D' door, 'R'/'B' locked doors, 'S' start, 'X' exit.
    /// </summary>
    public static Level FromRows(params string[] rows)
    {
        var lvl = new Level(rows[0].Length, rows.Length);
        for (var y = 0; y < lvl.H; y++)
        {
            for (var x = 0; x < lvl.W; x++)
            {
                var ch = rows[y][x];
                lvl.Cells[y * lvl.W + x] = ch switch
                {
                    '#' => 1,
                    'D' => Cell.Door,
                    'R' => Cell.DoorRed,
                    'B' => Cell.DoorBlue,
                    _ => Cell.Floor,
                };
                if (ch == 'S')
                {
                    lvl.StartX = x + 0.5;
                    lvl.StartY = y + 0.5;
                }
                if (ch == 'X')
                {
                    lvl.ExitX = x;
                    lvl.ExitY = y;
                }
            }
        }
        return lvl;
    }
}
