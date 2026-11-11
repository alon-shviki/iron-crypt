namespace IronCrypt.Client.Game;

/// <summary>Tiny pixel canvas for drawing sprites in code. Alpha 0 = transparent. Feet go at the bottom row.</summary>
public sealed class Painter
{
    public readonly uint[] Px = new uint[Art.Size * Art.Size];

    public void Set(int x, int y, uint c)
    {
        if (x >= 0 && y >= 0 && x < Art.Size && y < Art.Size) Px[x * Art.Size + y] = c;
    }

    public uint Get(int x, int y) => x >= 0 && y >= 0 && x < Art.Size && y < Art.Size ? Px[x * Art.Size + y] : 0;

    public Painter Rect(int x, int y, int w, int h, uint c)
    {
        for (var i = x; i < x + w; i++)
            for (var j = y; j < y + h; j++) Set(i, j, c);
        return this;
    }

    public Painter Ellipse(double cx, double cy, double rx, double ry, uint c)
    {
        for (var i = (int)(cx - rx); i <= (int)(cx + rx); i++)
        {
            for (var j = (int)(cy - ry); j <= (int)(cy + ry); j++)
            {
                var nx = (i - cx) / rx;
                var ny = (j - cy) / ry;
                if (nx * nx + ny * ny <= 1) Set(i, j, c);
            }
        }
        return this;
    }

    /// <summary>Cheap volume: darken the right third and bottom edge of everything drawn so far, then outline it.</summary>
    public uint[] Finish()
    {
        var src = (uint[])Px.Clone();
        for (var x = 0; x < Art.Size; x++)
        {
            for (var y = 0; y < Art.Size; y++)
            {
                var c = src[x * Art.Size + y];
                if (c == 0)
                {
                    // 1px dark outline wherever a transparent pixel touches an opaque one
                    var edge = (x > 0 && src[(x - 1) * Art.Size + y] != 0) || (x < Art.Size - 1 && src[(x + 1) * Art.Size + y] != 0) ||
                               (y > 0 && src[x * Art.Size + y - 1] != 0) || (y < Art.Size - 1 && src[x * Art.Size + y + 1] != 0);
                    if (edge) Px[x * Art.Size + y] = Art.Rgb(12, 10, 14);
                    continue;
                }
                // light comes from the upper left: pixels with nothing to their right are in shadow
                var rim = x < Art.Size - 2 && src[(x + 2) * Art.Size + y] == 0;
                if (rim) Px[x * Art.Size + y] = Art.Scale(c, 0.68);
            }
        }
        return Px;
    }
}

public static partial class Art
{
    public const uint Clear = 0;

    public static uint[] PickupSprite(PickupKind kind)
    {
        var p = new Painter();
        switch (kind)
        {
            case PickupKind.KeyRed:
            case PickupKind.KeyBlue:
                var k = kind == PickupKind.KeyRed ? Rgb(235, 60, 60) : Rgb(70, 140, 250);
                p.Ellipse(32, 16, 13, 13, k).Ellipse(32, 16, 6, 6, Clear);       // bow
                p.Rect(29, 28, 6, 32, k).Rect(35, 44, 9, 5, k).Rect(35, 53, 7, 5, k); // shaft + teeth
                break;
            case PickupKind.Health:
                p.Rect(10, 20, 44, 40, Rgb(232, 232, 236));
                p.Rect(27, 26, 10, 28, Rgb(220, 38, 38)).Rect(18, 35, 28, 10, Rgb(220, 38, 38));
                break;
            case PickupKind.Armor:
                p.Ellipse(32, 30, 22, 20, Rgb(40, 150, 90)).Rect(10, 8, 44, 24, Rgb(40, 150, 90));
                p.Ellipse(32, 46, 14, 16, Rgb(40, 150, 90)).Rect(28, 14, 8, 34, Rgb(150, 230, 180));
                break;
            case PickupKind.Bullets:
                p.Rect(8, 34, 48, 28, Rgb(96, 84, 52)).Rect(8, 34, 48, 6, Rgb(136, 120, 70));
                for (var i = 0; i < 5; i++) p.Rect(13 + i * 8, 22, 5, 14, Rgb(222, 180, 60));
                break;
            case PickupKind.Shells:
                p.Rect(10, 38, 44, 24, Rgb(110, 40, 36));
                for (var i = 0; i < 4; i++) p.Rect(13 + i * 10, 22, 7, 18, Rgb(200, 50, 44)).Rect(13 + i * 10, 22, 7, 5, Rgb(222, 180, 60));
                break;
            case PickupKind.Shotgun:
                p.Rect(4, 36, 44, 6, Rgb(70, 72, 80)).Rect(4, 43, 30, 4, Rgb(50, 52, 58)); // barrel + pump tube
                p.Rect(40, 36, 20, 12, Rgb(124, 78, 40)).Rect(22, 42, 12, 7, Rgb(124, 78, 40)); // stock + pump
                break;
            case PickupKind.Chaingun:
                for (var i = 0; i < 3; i++) p.Rect(2, 30 + i * 6, 36, 4, Rgb(84, 88, 98));
                p.Rect(34, 26, 22, 22, Rgb(58, 60, 68)).Rect(42, 48, 8, 12, Rgb(40, 42, 48));
                break;
        }
        return p.Finish();
    }

    /// <summary>The way down: a glowing green archway.</summary>
    public static uint[] ExitSprite()
    {
        var p = new Painter();
        p.Ellipse(32, 26, 26, 26, Rgb(20, 90, 50)).Rect(6, 26, 52, 38, Rgb(20, 90, 50));
        p.Ellipse(32, 28, 20, 20, Rgb(40, 190, 100)).Rect(12, 28, 40, 36, Rgb(40, 190, 100));
        p.Ellipse(32, 31, 13, 13, Rgb(170, 255, 200)).Rect(19, 31, 26, 33, Rgb(170, 255, 200));
        return p.Px; // no outline or rim shadow — it is a light source
    }
}
