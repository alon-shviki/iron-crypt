namespace IronCrypt.Client.Game;

// Billboards: anything that is not a wall (pickups, the exit, enemies, blood). Collected per frame,
// sorted far-to-near, drawn column by column against the wall depth buffer.
public partial class Renderer
{
    private struct Billboard
    {
        public double X, Y;
        public uint[][] Tex;   // pre-shaded, [shade level][texel]
        public double Size;    // world height, 1 = full wall height
        public double Lift;    // world units above the floor
        public double Depth;   // filled in during projection
        public bool Glow;      // ignores distance shading (light sources)
    }

    private readonly List<Billboard> _bills = new();
    private readonly uint[][][] _pickupTex = new uint[Enum.GetValues<PickupKind>().Length][][];
    private uint[][] _exitTex = Array.Empty<uint[]>();

    private void InitSprites()
    {
        foreach (var kind in Enum.GetValues<PickupKind>()) _pickupTex[(int)kind] = Art.Shaded(Art.PickupSprite(kind));
        _exitTex = Art.Shaded(Art.ExitSprite());
    }

    private void DrawSprites(Engine e, int horizon, double scale)
    {
        _bills.Clear();
        if (e.Level.ExitX >= 0)
            _bills.Add(new Billboard { X = e.Level.ExitX + 0.5, Y = e.Level.ExitY + 0.5, Tex = _exitTex, Size = 0.95, Glow = true });
        foreach (var p in e.Pickups)
        {
            var key = p.Kind is PickupKind.KeyRed or PickupKind.KeyBlue;
            _bills.Add(new Billboard
            {
                X = p.X,
                Y = p.Y,
                Tex = _pickupTex[(int)p.Kind],
                Size = key ? 0.32 : 0.36,
                Lift = key ? 0.14 + 0.05 * Math.Sin(e.Time * 3 + p.X) : 0, // keys hover and bob so they catch the eye
                Glow = key,
            });
        }
        CollectActors(e);

        var dirX = Math.Cos(e.Angle);
        var dirY = Math.Sin(e.Angle);
        var planeX = -dirY * PlaneLen;
        var planeY = dirX * PlaneLen;
        var inv = 1.0 / (planeX * dirY - dirX * planeY);

        // project: Depth = distance along the view direction; cull what is behind the camera
        for (var i = _bills.Count - 1; i >= 0; i--)
        {
            var b = _bills[i];
            b.Depth = inv * (-planeY * (b.X - e.Px) + planeX * (b.Y - e.Py));
            if (b.Depth < 0.12) _bills.RemoveAt(i);
            else _bills[i] = b;
        }
        _bills.Sort(static (a, b) => b.Depth.CompareTo(a.Depth));

        foreach (var b in _bills)
        {
            var side = inv * (dirY * (b.X - e.Px) - dirX * (b.Y - e.Py));
            var full = scale / b.Depth;                    // pixel height of a full wall at this depth
            var size = (int)(full * b.Size);
            if (size < 1) continue;
            var centerX = (int)(W / 2.0 * (1 + side / b.Depth));
            var bottom = horizon + (int)(full / 2) - (int)(full * b.Lift);
            var top = bottom - size;
            var left = centerX - size / 2;
            var tex = b.Tex[b.Glow ? 0 : Art.ShadeOf(b.Depth)];
            var step = (long)Art.Size * 65536 / size;      // 16.16 texels per screen pixel

            var x0 = Math.Max(0, left);
            var x1 = Math.Min(W - 1, left + size - 1);
            var y0 = Math.Max(0, top);
            var y1 = Math.Min(H - 1, bottom - 1);
            for (var x = x0; x <= x1; x++)
            {
                if (b.Depth >= _zbuf[x]) continue;         // a wall is in front of this column
                var texBase = (int)(((x - left) * step) >> 16) * Art.Size;
                var pos = (y0 - top) * step;
                var p = y0 * W + x;
                for (var y = y0; y <= y1; y++)
                {
                    var c = tex[texBase + (int)(pos >> 16)];
                    if (c != 0) Fb[p] = c;
                    pos += step;
                    p += W;
                }
            }
        }
    }

    // enemies and blood join the billboard list in the enemy phase
    private void CollectActors(Engine e) { }
}
