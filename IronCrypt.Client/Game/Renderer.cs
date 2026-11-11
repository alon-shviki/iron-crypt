namespace IronCrypt.Client.Game;

/// <summary>
/// Software raycaster. Draws the engine state into an RGBA framebuffer that the page hands to JS in one call.
/// Head-less like the engine (it only fills arrays), so tests can render a frame.
/// </summary>
public partial class Renderer
{
    public const double PlaneLen = 0.66;   // tan(fov/2) — ~66° horizontal field of view
    public const int MiniCells = 21;       // minimap shows this many cells across, centered on the player
    public const int MiniScale = 2;        // pixels per cell

    public int W { get; private set; }
    public int H { get; private set; }
    public uint[] Fb = Array.Empty<uint>();
    public byte[] Pixels = Array.Empty<byte>();

    private double[] _zbuf = Array.Empty<double>();
    private readonly uint[][]?[] _walls = new uint[32][][];   // [cell id][shade level][texel]
    private readonly uint[] _floor = new uint[Art.Shades];
    private readonly uint[] _ceil = new uint[Art.Shades];

    public Renderer(int w = 320, int h = 200)
    {
        for (byte c = 1; c <= Cell.WallKinds; c++) _walls[c] = Art.Shaded(Art.WallTexture(c));
        for (var c = Cell.Door; c <= Cell.DoorBlue; c++) _walls[c] = Art.Shaded(Art.WallTexture(c));
        for (var l = 0; l < Art.Shades; l++)
        {
            _floor[l] = Art.Scale(Art.Rgb(74, 64, 56), Art.Brightness(l));
            _ceil[l] = Art.Scale(Art.Rgb(44, 42, 52), Art.Brightness(l));
        }
        InitSprites();
        InitWeapons();
        Resize(w, h);
    }

    /// <summary>Internal resolution for a viewport: 200 px on the short side, the long side follows the aspect ratio.</summary>
    public static (int W, int H) SizeFor(double viewW, double viewH)
    {
        const int Short = 200;
        var aspect = viewW / Math.Max(1, viewH);
        return aspect >= 1
            ? (Math.Clamp((int)(Short * aspect) & ~1, Short, 480), Short)
            : (Short, Math.Clamp((int)(Short / aspect) & ~1, Short, 440));
    }

    public void Resize(int w, int h)
    {
        if (w == W && h == H) return;
        W = w;
        H = h;
        Fb = new uint[w * h];
        Pixels = new byte[w * h * 4];
        _zbuf = new double[w];
    }

    public byte[] Render(Engine e)
    {
        var horizon = H / 2;
        var scale = W / (2 * PlaneLen); // wall height in pixels at distance 1

        DrawFloorAndCeiling(horizon, scale);
        DrawWalls(e, horizon, scale);
        DrawSprites(e, horizon, scale);
        DrawWeapon(e);
        DrawMinimap(e);

        Buffer.BlockCopy(Fb, 0, Pixels, 0, Pixels.Length);
        return Pixels;
    }

    // ponytail: flat distance-shaded rows, no per-pixel floor texture — add floor casting if the look needs it
    private void DrawFloorAndCeiling(int horizon, double scale)
    {
        for (var y = 0; y < H; y++)
        {
            var d = Math.Abs(y - horizon) + 0.5;
            var level = Art.ShadeOf(0.5 * scale / d);
            Array.Fill(Fb, y < horizon ? _ceil[level] : _floor[level], y * W, W);
        }
    }

    private void DrawWalls(Engine e, int horizon, double scale)
    {
        var lvl = e.Level;
        var dirX = Math.Cos(e.Angle);
        var dirY = Math.Sin(e.Angle);
        var planeX = -dirY * PlaneLen;
        var planeY = dirX * PlaneLen;

        for (var x = 0; x < W; x++)
        {
            var cam = 2.0 * x / W - 1;
            var rx = dirX + planeX * cam;
            var ry = dirY + planeY * cam;
            var mx = (int)e.Px;
            var my = (int)e.Py;
            var ddx = rx == 0 ? 1e30 : Math.Abs(1 / rx);
            var ddy = ry == 0 ? 1e30 : Math.Abs(1 / ry);
            var stepX = rx < 0 ? -1 : 1;
            var stepY = ry < 0 ? -1 : 1;
            var sdx = rx < 0 ? (e.Px - mx) * ddx : (mx + 1 - e.Px) * ddx;
            var sdy = ry < 0 ? (e.Py - my) * ddy : (my + 1 - e.Py) * ddy;

            var dist = 1e30;
            var wallX = 0.0;
            var side = 0;
            byte cell = 0;

            for (var i = 0; i < 96; i++)
            {
                if (sdx < sdy)
                {
                    sdx += ddx;
                    mx += stepX;
                    side = 0;
                }
                else
                {
                    sdy += ddy;
                    my += stepY;
                    side = 1;
                }

                cell = lvl.At(mx, my);
                if (cell == Cell.Floor) continue;

                if (Cell.IsDoor(cell))
                {
                    // the door is a thin slab in the middle of its cell, sliding sideways as it opens
                    var d = side == 0 ? sdx - ddx * 0.5 : sdy - ddy * 0.5;
                    var hit = side == 0 ? e.Py + d * ry : e.Px + d * rx;
                    if ((int)Math.Floor(hit) != (side == 0 ? my : mx)) continue; // leaves the cell before the slab
                    var frac = hit - Math.Floor(hit);
                    var open = lvl.DoorOpen[my * lvl.W + mx];
                    if (frac < open) continue;                                   // through the opened gap
                    dist = d;
                    wallX = frac - open;
                    break;
                }

                dist = side == 0 ? sdx - ddx : sdy - ddy;
                var h = side == 0 ? e.Py + dist * ry : e.Px + dist * rx;
                wallX = h - Math.Floor(h);
                break;
            }

            _zbuf[x] = dist;
            if (dist >= 1e29) continue;
            if (dist < 0.01) dist = 0.01;

            var lineH = (int)(scale / dist);
            var top = horizon - lineH / 2;
            var y0 = Math.Max(0, top);
            var y1 = Math.Min(H - 1, top + lineH);
            var shade = Math.Min(Art.Shades - 1, Art.ShadeOf(dist) + side); // y-facing walls one step darker
            var tex = (_walls[cell] ?? _walls[1]!)[shade];
            var texBase = ((int)(wallX * Art.Size) & (Art.Size - 1)) * Art.Size;

            // 16.16 fixed point walk down the texture column
            var step = (long)Art.Size * 65536 / Math.Max(1, lineH);
            var pos = (y0 - top) * step;
            var p = y0 * W + x;
            for (var y = y0; y <= y1; y++)
            {
                Fb[p] = tex[texBase + (int)((pos >> 16) & (Art.Size - 1))];
                pos += step;
                p += W;
            }
        }
    }

    private void DrawMinimap(Engine e)
    {
        var lvl = e.Level;
        var ox = W - MiniCells * MiniScale - 4;
        var oy = 4;
        var half = MiniCells / 2;
        var cx = (int)e.Px;
        var cy = (int)e.Py;

        for (var j = 0; j < MiniCells; j++)
        {
            for (var i = 0; i < MiniCells; i++)
            {
                var gx = cx + i - half;
                var gy = cy + j - half;
                uint color = lvl.At(gx, gy) switch
                {
                    Cell.Floor => gx == lvl.ExitX && gy == lvl.ExitY ? Art.Rgb(60, 220, 110) : Art.Rgb(14, 14, 18),
                    Cell.Door => Art.Rgb(190, 190, 120),
                    Cell.DoorRed => Art.Rgb(220, 50, 50),
                    Cell.DoorBlue => Art.Rgb(60, 120, 240),
                    _ => Art.Rgb(84, 84, 96),
                };
                FillRect(ox + i * MiniScale, oy + j * MiniScale, MiniScale, MiniScale, color);
            }
        }

        // player dot + a pixel showing where they face
        var pxc = ox + half * MiniScale;
        var pyc = oy + half * MiniScale;
        FillRect(pxc, pyc, MiniScale, MiniScale, Art.Rgb(255, 255, 255));
        FillRect(pxc + (int)Math.Round(Math.Cos(e.Angle) * 3), pyc + (int)Math.Round(Math.Sin(e.Angle) * 3),
            MiniScale, MiniScale, Art.Rgb(250, 204, 21));
    }

    private void FillRect(int x, int y, int w, int h, uint color)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(W, x + w);
        if (x1 <= x0) return;
        for (var j = Math.Max(0, y); j < Math.Min(H, y + h); j++)
            Array.Fill(Fb, color, j * W + x0, x1 - x0);
    }
}
