namespace IronCrypt.Client.Game;

public enum EnemyState { Idle, Chase, Windup, Pain, Dead }

/// <summary>Stats per enemy type. Range is how close it must be to start an attack; Windup is the telegraph before it lands.</summary>
public record EnemyDef(int Hp, double Speed, double Radius, int Damage, double Range, double Windup, double Rest,
    bool Ranged, double PainChance, int Score, double Size);

public class Enemy
{
    public EnemyKind Kind;
    public double X, Y;
    public int Hp;
    public EnemyState State;
    public double Timer;     // time left in Windup / Pain
    public double Cooldown;  // rest before the next attack
    public double Anim;      // walk-cycle phase
    public bool Alive => State != EnemyState.Dead;
}

// Enemies: spawning, AI (idle → chase → windup → attack), pathfinding, taking hits.
public partial class Engine
{
    // ── tuning knobs ──
    public static readonly EnemyDef[] EnemyDefs =
    {
        /* Grunt   */ new(Hp: 40,  Speed: 2.3,  Radius: 0.28, Damage: 10, Range: 0.9, Windup: 0.30, Rest: 0.7, Ranged: false, PainChance: 0.6, Score: 100, Size: 0.72),
        /* Shooter */ new(Hp: 30,  Speed: 1.7,  Radius: 0.28, Damage: 8,  Range: 9.0, Windup: 0.60, Rest: 1.1, Ranged: true,  PainChance: 0.7, Score: 150, Size: 0.78),
        /* Tank    */ new(Hp: 180, Speed: 1.05, Radius: 0.36, Damage: 30, Range: 1.1, Windup: 0.55, Rest: 1.0, Ranged: false, PainChance: 0.2, Score: 400, Size: 0.98),
    };
    public const double SightRange = 12;     // an idle enemy notices the player within this distance, given line of sight
    public const int HearingRange = 16;      // …or hears a gunshot within this many walking cells
    public const double PainTime = 0.18;
    public const double HpPerDepth = 0.08;   // +8% enemy health per level below the first
    public const double DamagePerDepth = 0.05;

    public readonly List<Enemy> Enemies = new();

    private int[] _flow = Array.Empty<int>(); // walking distance to the player from every cell — the pathfinding field
    private int _flowCell = -1;
    private double _flowAge;

    private void OnLevelLoaded()
    {
        Enemies.Clear();
        _flowCell = -1;
        foreach (var s in Level.Enemies) SpawnEnemy(s.Kind, s.X, s.Y);
    }

    /// <summary>Adds an enemy, unless the spot is inside a wall — then nothing spawns (returns null).</summary>
    public Enemy? SpawnEnemy(EnemyKind kind, double x, double y)
    {
        var def = EnemyDefs[(int)kind];
        if (!IsFree(x, y, def.Radius)) return null;
        var enemy = new Enemy { Kind = kind, X = x, Y = y, Hp = (int)(def.Hp * (1 + HpPerDepth * Math.Max(0, Depth - 1))) };
        Enemies.Add(enemy);
        return enemy;
    }

    private bool EnemyNear(double x, double y, double range)
    {
        foreach (var en in Enemies)
            if (en.Alive && Math.Abs(en.X - x) < range && Math.Abs(en.Y - y) < range) return true;
        return false;
    }

    private void TickEnemies(double dt)
    {
        if (Enemies.Count == 0) return;
        RefreshFlow(dt);

        var alertPlayed = false;
        foreach (var en in Enemies)
        {
            if (!en.Alive) continue;
            var def = EnemyDefs[(int)en.Kind];
            var dx = Px - en.X;
            var dy = Py - en.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            en.Cooldown -= dt;

            switch (en.State)
            {
                case EnemyState.Idle:
                    if (dist < SightRange && Level.LineOfSight(en.X, en.Y, Px, Py))
                    {
                        en.State = EnemyState.Chase;
                        if (!alertPlayed) Sounds.Add(Sfx.EnemyAlert);
                        alertPlayed = true;
                    }
                    break;

                case EnemyState.Pain:
                    en.Timer -= dt;
                    if (en.Timer <= 0) en.State = EnemyState.Chase;
                    break;

                case EnemyState.Windup:
                    en.Timer -= dt;
                    if (en.Timer <= 0) Attack(en, def, dist);
                    break;

                case EnemyState.Chase:
                    var sees = Level.LineOfSight(en.X, en.Y, Px, Py);
                    if (sees && dist <= def.Range && en.Cooldown <= 0)
                    {
                        en.State = EnemyState.Windup;
                        en.Timer = def.Windup;
                        break;
                    }
                    // shooters hold their ground once they have a clear shot; brawlers stop at arm's length
                    var holds = sees && dist < (def.Ranged ? def.Range * 0.6 : def.Range * 0.8);
                    if (!holds) Advance(en, def, sees, dist, dt);
                    break;
            }
        }
    }

    private void Attack(Enemy en, EnemyDef def, double dist)
    {
        en.State = EnemyState.Chase;
        en.Cooldown = def.Rest;
        var damage = (int)(def.Damage * (1 + DamagePerDepth * Math.Max(0, Depth - 1)));

        if (!def.Ranged)
        {
            if (dist <= def.Range + 0.35) Hurt(damage); // stepping back during the windup dodges it
            return;
        }

        Sounds.Add(Sfx.EnemyShot);
        if (!Level.LineOfSight(en.X, en.Y, Px, Py)) return; // ducked behind cover during the windup
        var hitChance = Math.Clamp(0.85 - dist * 0.06, 0.25, 0.85);
        if (_rnd.NextDouble() < hitChance) Hurt(damage);
    }

    private void Advance(Enemy en, EnemyDef def, bool sees, double dist, double dt)
    {
        // close and visible: run straight at the player. Otherwise (or if that got stuck on a corner) follow the flow field.
        if (sees && dist < 3.5 && MoveToward(en, def, Px, Py, dt)) return;

        var w = Level.W;
        var cell = (int)en.Y * w + (int)en.X;
        if (cell < 0 || cell >= _flow.Length || _flow[cell] <= 0) return; // no route (locked away) or already there

        var best = -1;
        Span<int> steps = stackalloc int[] { 1, -1, w, -w };
        foreach (var s in steps)
        {
            var n = cell + s;
            if (n < 0 || n >= _flow.Length || _flow[n] < 0) continue;
            if (best < 0 || _flow[n] < _flow[best]) best = n;
        }
        if (best < 0 || _flow[best] >= _flow[cell]) return;

        if (Cell.IsDoor(Level.Cells[best])) OpenDoor(best); // the flow field only crosses doors this enemy may open
        MoveToward(en, def, best % w + 0.5, best / w + 0.5, dt);
    }

    /// <summary>Steps toward a point with wall sliding. False when it barely moved (blocked).</summary>
    private bool MoveToward(Enemy en, EnemyDef def, double tx, double ty, double dt)
    {
        var dx = tx - en.X;
        var dy = ty - en.Y;
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1e-6) return true;
        var step = Math.Min(d, def.Speed * dt);
        var (ox, oy) = (en.X, en.Y);
        var mx = dx / d * step;
        var my = dy / d * step;
        if (CanStand(en, def, en.X + mx, en.Y)) en.X += mx;
        if (CanStand(en, def, en.X, en.Y + my)) en.Y += my;
        en.Anim += dt * 4;
        var moved = Math.Abs(en.X - ox) + Math.Abs(en.Y - oy);
        return moved > step * 0.3;
    }

    /// <summary>Walls, the player and other enemies are solid — but a move that separates two bodies is always allowed.</summary>
    private bool CanStand(Enemy en, EnemyDef def, double x, double y)
    {
        if (!IsFree(x, y, def.Radius)) return false;
        if (Overlaps(en.X, en.Y, x, y, Px, Py, def.Radius + PlayerRadius)) return false;
        foreach (var other in Enemies)
        {
            if (other == en || !other.Alive) continue;
            if (Overlaps(en.X, en.Y, x, y, other.X, other.Y, def.Radius + EnemyDefs[(int)other.Kind].Radius)) return false;
        }
        return true;
    }

    private static bool Overlaps(double fromX, double fromY, double toX, double toY, double bx, double by, double minDist)
    {
        var now = (toX - bx) * (toX - bx) + (toY - by) * (toY - by);
        if (now >= minDist * minDist) return false;
        var before = (fromX - bx) * (fromX - bx) + (fromY - by) * (fromY - by);
        return now < before; // only blocked when getting closer
    }

    private void RefreshFlow(double dt)
    {
        _flowAge += dt;
        var cell = (int)Py * Level.W + (int)Px;
        if (cell == _flowCell && _flowAge < 0.5 && _flow.Length == Level.Cells.Length) return;
        _flowCell = cell;
        _flowAge = 0;
        // enemies can pass exactly what the player can: plain doors, and locked ones once the key is held
        _flow = LevelGenerator.Flood(Level, (int)Px, (int)Py, HasRedKey, HasBlueKey);
    }

    /// <summary>A gunshot wakes every idle enemy within walking earshot.</summary>
    private void AlertEnemies()
    {
        RefreshFlow(0);
        foreach (var en in Enemies)
        {
            if (en.State != EnemyState.Idle) continue;
            var d = _flow[(int)en.Y * Level.W + (int)en.X];
            if (d >= 0 && d <= HearingRange) en.State = EnemyState.Chase;
        }
    }

    /// <summary>One bullet along <paramref name="angle"/>: damages the nearest enemy in its path, if any is closer than the wall.</summary>
    private void HitScan(double angle, double wallDist, int damage)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        Enemy? hit = null;
        var hitDist = wallDist;
        foreach (var en in Enemies)
        {
            if (!en.Alive) continue;
            var dx = en.X - Px;
            var dy = en.Y - Py;
            var along = dx * cos + dy * sin;
            if (along <= 0 || along >= hitDist) continue;
            var across = Math.Abs(-dx * sin + dy * cos);
            if (across > EnemyDefs[(int)en.Kind].Radius) continue;
            hit = en;
            hitDist = along;
        }
        if (hit != null) Damage(hit, damage);
    }

    public void Damage(Enemy en, int damage)
    {
        if (!en.Alive) return;
        var def = EnemyDefs[(int)en.Kind];
        en.Hp -= damage;
        OnEnemyHit(en);
        if (en.Hp <= 0)
        {
            en.State = EnemyState.Dead;
            Kills++;
            Score += def.Score;
            Sounds.Add(Sfx.EnemyDie);
            return;
        }
        Sounds.Add(Sfx.EnemyHurt);
        // being shot always wakes it; flinching (which interrupts an attack) is a per-type chance
        if (en.State == EnemyState.Idle) en.State = EnemyState.Chase;
        if (_rnd.NextDouble() < def.PainChance)
        {
            en.State = EnemyState.Pain;
            en.Timer = PainTime;
        }
    }

    private void OnEnemyHit(Enemy en) { } // blood arrives with the polish phase
}
