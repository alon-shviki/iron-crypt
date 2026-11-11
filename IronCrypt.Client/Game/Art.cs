namespace IronCrypt.Client.Game;

/// <summary>
/// Every pixel the game shows is generated here — no image files.
/// Textures are 64x64, stored column-major (index = x * Size + y) so the wall loop reads them sequentially.
/// Colors are 0xAABBGGRR (little-endian RGBA, what ImageData expects).
/// </summary>
public static partial class Art
{
    public const int Size = 64;
    public const int Shades = 16;

    public static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)Math.Clamp(b, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(r, 0, 255);

    /// <summary>Brightness per shade level: 0 = full, last = nearly black.</summary>
    public static double Brightness(int level) => 1.0 - level / (Shades + 2.0);

    public static int ShadeOf(double dist) => Math.Min(Shades - 1, (int)(dist * 1.3));

    public static uint Scale(uint c, double k)
    {
        var r = (int)((c & 0xFF) * k);
        var g = (int)(((c >> 8) & 0xFF) * k);
        var b = (int)(((c >> 16) & 0xFF) * k);
        return (c & 0xFF000000u) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
    }

    /// <summary>Pre-shaded copies of a texture, one per distance level — the wall loop becomes a plain lookup.</summary>
    public static uint[][] Shaded(uint[] tex)
    {
        var levels = new uint[Shades][];
        for (var l = 0; l < Shades; l++)
        {
            var k = Brightness(l);
            var t = new uint[tex.Length];
            for (var i = 0; i < tex.Length; i++) t[i] = Scale(tex[i], k);
            levels[l] = t;
        }
        return levels;
    }

    private static uint[] Make(int seed, Func<int, int, int, (int R, int G, int B)> pixel)
    {
        var rnd = new Random(seed);
        var tex = new uint[Size * Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                var (r, g, b) = pixel(x, y, rnd.Next(-10, 11));
                tex[x * Size + y] = Rgb(r, g, b);
            }
        }
        return tex;
    }

    /// <summary>Wall texture for a cell id (1..WallKinds) or a door id.</summary>
    public static uint[] WallTexture(byte cell) => cell switch
    {
        1 => Brick(),
        2 => StoneBlocks(mossy: false),
        3 => MetalPanel(),
        4 => StoneBlocks(mossy: true),
        Cell.DoorRed => DoorTexture(200, 40, 40),
        Cell.DoorBlue => DoorTexture(50, 110, 230),
        _ => DoorTexture(150, 150, 160),
    };

    private static uint[] Brick() => Make(1, (x, y, n) =>
    {
        var row = y / 8;
        var bx = (x + (row % 2) * 8) % 16;
        if (y % 8 == 0 || bx == 0) return (48 + n, 44 + n, 42 + n); // mortar
        var tint = (row * 7 + (x + (row % 2) * 8) / 16 * 13) % 5 * 6;
        return (120 + tint + n, 52 + tint / 2 + n, 40 + n);
    });

    private static uint[] StoneBlocks(bool mossy) => Make(mossy ? 4 : 2, (x, y, n) =>
    {
        var row = y / 16;
        var bx = (x + (row % 2) * 16) % 32;
        if (y % 16 == 0 || bx == 0) return (30 + n, 30 + n, 34 + n);
        var v = 92 + ((row * 5 + (x + (row % 2) * 16) / 32 * 3) % 4) * 7 + n;
        if (mossy && y > 34 + (x * 7 % 13)) return (v / 3, v - 10 + n, v / 3);
        return (v, v, v + 8);
    });

    private static uint[] MetalPanel() => Make(3, (x, y, n) =>
    {
        var px = x % 32;
        var py = y % 32;
        if (px == 0 || py == 0) return (22, 24, 28);
        if ((px == 4 || px == 28) && (py == 4 || py == 28)) return (170, 175, 185); // rivets
        var rust = (x * 31 + y * 17) % 97 < 6 && y % 32 > 18;
        if (rust) return (120 + n, 70 + n, 36);
        var v = 78 + (px == 1 || py == 1 ? 26 : 0) + n / 2;
        return (v, v + 4, v + 12);
    });

    private static uint[] DoorTexture(int r, int g, int b) => Make(5, (x, y, n) =>
    {
        if (x < 2 || x > 61 || y < 2) return (26, 26, 30);
        if (y is > 26 and < 38) return (r + n, g + n, b + n);                 // colored band = which key
        if (x is > 50 and < 56 && y is > 40 and < 50) return (210, 200, 120); // handle
        var v = 70 + (x % 8 < 2 ? -18 : 0) + n / 2;
        return (v, v, v + 6);
    });
}
