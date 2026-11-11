namespace IronCrypt.Client.Game;

public enum EnemyFrame { Walk0, Walk1, Attack, Pain, Dead }

// Enemy sprites, drawn from a shared skeleton (legs, torso, arms, head) with per-type proportions and palette.
public static partial class Art
{
    public static uint[] EnemySprite(EnemyKind kind, EnemyFrame frame)
    {
        // body, dark (legs/shadow side), accent (eyes/claws), torso width, head radius
        var (body, dark, accent, w, head) = kind switch
        {
            EnemyKind.Grunt => (Rgb(150, 74, 48), Rgb(92, 42, 30), Rgb(255, 214, 60), 24, 9),
            EnemyKind.Shooter => (Rgb(84, 104, 78), Rgb(46, 58, 46), Rgb(255, 70, 50), 22, 8),
            _ => (Rgb(92, 96, 112), Rgb(48, 50, 62), Rgb(110, 220, 255), 40, 7),
        };
        var p = new Painter();

        if (frame == EnemyFrame.Dead)
        {
            p.Ellipse(32, 58, w * 0.9 + 6, 5, Rgb(120, 14, 14)); // blood pool
            p.Ellipse(32, 55, w * 0.6 + 4, 6, body).Ellipse(32 - w / 2, 53, head * 0.8, 5, dark);
            return p.Finish();
        }

        // legs — the second walk frame lifts one, which reads as a stride at game scale
        var lift = frame == EnemyFrame.Walk1 ? 5 : 0;
        p.Rect(32 - w / 4 - 4, 44, 8, 20 - lift, dark).Rect(32 + w / 4 - 4, 44, 8, 20 - (frame == EnemyFrame.Walk0 ? 3 : 0), dark);

        // torso + head
        p.Ellipse(32, 34, w / 2.0, 15, body).Rect(32 - w / 2 + 3, 28, w - 6, 16, body);
        var headY = kind == EnemyKind.Tank ? 16 : 14;
        p.Ellipse(32, headY, head, head, kind == EnemyKind.Shooter ? dark : body);
        if (kind == EnemyKind.Shooter) p.Rect(32 - head, headY - 1, head * 2, 4, Rgb(20, 22, 24)); // visor
        p.Rect(32 - head / 2 - 2, headY - 1, 3, 2, accent).Rect(32 + head / 2 - 1, headY - 1, 3, 2, accent);
        if (kind == EnemyKind.Grunt) p.Rect(28, headY + 4, 8, 2, Rgb(240, 240, 220)); // teeth
        if (kind == EnemyKind.Tank) p.Rect(32 - w / 2 + 4, 30, w - 8, 3, accent);     // chest light

        // arms
        var ax = 32 - w / 2 - 5;
        var bx = 32 + w / 2;
        if (frame != EnemyFrame.Attack)
        {
            p.Rect(ax, 26, 5, 18, body).Rect(bx, 26, 5, 18, body);
            if (kind == EnemyKind.Shooter) p.Rect(18, 32, 28, 4, Rgb(34, 36, 40)); // rifle carried across the chest
        }
        else
        {
            switch (kind)
            {
                case EnemyKind.Grunt: // claws up, about to swipe
                    p.Rect(ax, 8, 5, 22, body).Rect(bx, 8, 5, 22, body);
                    p.Rect(ax - 1, 4, 7, 5, accent).Rect(bx - 1, 4, 7, 5, accent);
                    break;
                case EnemyKind.Shooter: // rifle levelled straight at the camera, muzzle lit
                    p.Rect(ax, 26, 5, 12, body).Rect(bx, 26, 5, 12, body);
                    p.Rect(27, 30, 10, 8, Rgb(34, 36, 40));
                    p.Ellipse(32, 34, 8, 8, Rgb(255, 170, 40)).Ellipse(32, 34, 4, 4, Rgb(255, 250, 210));
                    break;
                default: // both fists forward
                    p.Ellipse(ax + 1, 36, 9, 9, body).Ellipse(bx + 4, 36, 9, 9, body);
                    p.Ellipse(ax + 1, 36, 4, 4, accent).Ellipse(bx + 4, 36, 4, 4, accent);
                    break;
            }
        }

        var px = p.Finish();
        if (frame == EnemyFrame.Pain)
        {
            // hit flash: wash the whole sprite toward white
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c == 0) continue;
                px[i] = Rgb((int)((c & 0xFF) + 255) / 2, (int)(((c >> 8) & 0xFF) + 255) / 2, (int)(((c >> 16) & 0xFF) + 255) / 2);
            }
        }
        return px;
    }
}
