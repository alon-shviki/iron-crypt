namespace IronCrypt.Client.Game;

// First-person weapon sprites, seen from behind the gun. The bottom row sits on the bottom of the screen.
public static partial class Art
{
    private static readonly uint Skin = Rgb(214, 160, 120);
    private static readonly uint Sleeve = Rgb(60, 74, 52);

    /// <summary>Texel row of the muzzle for each weapon — where the flash is centred.</summary>
    public static int MuzzleY(int weapon) => weapon switch { 1 => 6, 2 => 10, _ => 18 };

    public static uint[] WeaponSprite(int weapon)
    {
        var p = new Painter();
        switch (weapon)
        {
            case 0: // pistol: slide, sights, grip in one hand
                p.Rect(22, 46, 22, 18, Sleeve).Ellipse(32, 46, 12, 8, Skin);
                p.Rect(26, 20, 12, 30, Rgb(58, 60, 68)).Rect(27, 20, 10, 4, Rgb(96, 100, 112));
                p.Rect(30, 16, 4, 5, Rgb(30, 30, 34)).Rect(28, 26, 8, 2, Rgb(30, 30, 34));
                p.Ellipse(26, 50, 5, 7, Skin).Ellipse(38, 50, 5, 7, Skin);
                break;
            case 1: // shotgun: two long barrels up the middle, pump under a hand
                p.Rect(18, 50, 28, 14, Sleeve);
                p.Rect(25, 6, 6, 44, Rgb(72, 74, 84)).Rect(33, 6, 6, 44, Rgb(72, 74, 84));
                p.Rect(26, 6, 4, 3, Rgb(18, 18, 20)).Rect(34, 6, 4, 3, Rgb(18, 18, 20));
                p.Rect(22, 34, 20, 12, Rgb(124, 78, 40)).Rect(22, 34, 20, 2, Rgb(160, 104, 56));
                p.Ellipse(32, 52, 14, 8, Skin);
                break;
            default: // chaingun: barrel cluster, heavy body, both hands
                p.Rect(10, 50, 16, 14, Sleeve).Rect(38, 50, 16, 14, Sleeve);
                p.Ellipse(32, 14, 10, 6, Rgb(40, 42, 48));
                for (var i = 0; i < 3; i++) p.Rect(24 + i * 6, 10, 4, 26, Rgb(92, 96, 108)).Rect(25 + i * 6, 10, 2, 2, Rgb(14, 14, 16));
                p.Rect(18, 32, 28, 26, Rgb(54, 56, 64)).Rect(18, 32, 28, 3, Rgb(98, 102, 114));
                p.Rect(28, 40, 8, 10, Rgb(200, 160, 50)); // ammo feed
                p.Ellipse(16, 52, 8, 7, Skin).Ellipse(48, 52, 8, 7, Skin);
                break;
        }
        return p.Finish();
    }

    public static uint[] MuzzleFlashSprite()
    {
        var p = new Painter();
        p.Ellipse(32, 32, 30, 12, Rgb(255, 150, 30)).Ellipse(32, 32, 12, 30, Rgb(255, 150, 30));
        p.Ellipse(32, 32, 20, 20, Rgb(255, 210, 70));
        p.Ellipse(32, 32, 11, 11, Rgb(255, 250, 220));
        return p.Px;
    }
}
