namespace IronCrypt.Client.Game;

// First-person overlay: the held weapon (sway, recoil, reload dip), its muzzle flash and the crosshair.
public partial class Renderer
{
    private readonly uint[][] _weaponTex = new uint[Engine.Weapons.Length][];
    private uint[] _flashTex = Array.Empty<uint>();

    private void InitWeapons()
    {
        for (var i = 0; i < _weaponTex.Length; i++) _weaponTex[i] = Art.WeaponSprite(i);
        _flashTex = Art.MuzzleFlashSprite();
    }

    private void DrawWeapon(Engine e)
    {
        var size = (int)(Math.Min(W, H * 1.4) * 0.52);
        // walking sways it in a shallow U; firing kicks it back; reloading and switching drop it out of view
        var swayX = e.Moving ? Math.Sin(e.Bob) * size * 0.035 : 0;
        var swayY = e.Moving ? Math.Abs(Math.Cos(e.Bob)) * size * 0.03 : 0;
        var drop = 0.0;
        if (e.SwitchLeft > 0) drop = e.SwitchLeft / Engine.SwitchTime;
        else if (e.ReloadLeft > 0) drop = 0.55 * Math.Sin(Math.PI * Math.Min(1, e.ReloadLeft / e.Gun.ReloadTime));
        var left = W / 2 - size / 2 + (int)swayX;
        var top = H - size + (int)(swayY + e.Recoil * size * 0.07 + drop * size * 0.8);

        if (e.MuzzleFlash > 0)
        {
            var f = size * 2 / 5;
            Blit(_flashTex, W / 2 - f / 2 + (int)swayX, top + size * Art.MuzzleY(e.Weapon) / Art.Size - f / 2, f);
        }
        Blit(_weaponTex[e.Weapon], left, top, size);

        // crosshair
        var cx = W / 2;
        var cy = H / 2;
        var white = Art.Rgb(240, 240, 240);
        FillRect(cx - 4, cy, 3, 1, white);
        FillRect(cx + 2, cy, 3, 1, white);
        FillRect(cx, cy - 4, 1, 3, white);
        FillRect(cx, cy + 2, 1, 3, white);
    }

    /// <summary>Nearest-neighbour scaled sprite straight onto the screen, skipping transparent texels.</summary>
    private void Blit(uint[] tex, int left, int top, int size)
    {
        if (size < 1) return;
        var step = (long)Art.Size * 65536 / size;
        var x0 = Math.Max(0, left);
        var x1 = Math.Min(W - 1, left + size - 1);
        var y0 = Math.Max(0, top);
        var y1 = Math.Min(H - 1, top + size - 1);
        for (var x = x0; x <= x1; x++)
        {
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
