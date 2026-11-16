namespace IronCrypt.Client.Game;

public enum Ammo { Bullets, Shells, Rockets }

/// <summary>One gun. Three ammo pools; the pistol and chaingun share bullets. Rockets fly, everything else is hitscan.</summary>
public record WeaponDef(string Name, int Damage, int Pellets, double Spread, double Cooldown,
    int MagSize, double ReloadTime, Ammo Ammo, Sfx Sound, double Kick);

/// <summary>How a shot connected — what the trickshot bonuses are judged on.</summary>
public record struct HitInfo(bool Head, bool Banked, bool Splash, double Distance);

public struct Missile
{
    public double X, Y, Z, Vx, Vy, Vz;
}

public struct Explosion
{
    public double X, Y, Z, Age;
}

// Player combat: weapons, ammo, reloads, damage taken, item pickups, score.
public partial class Engine
{
    // ── tuning knobs ──
    public static readonly WeaponDef[] Weapons =
    {
        new("Pistol",   Damage: 14, Pellets: 1, Spread: 0.012, Cooldown: 0.32,  MagSize: 12, ReloadTime: 1.0, Ammo.Bullets, Sfx.Pistol,   Kick: 0.5),
        new("Shotgun",  Damage: 9,  Pellets: 7, Spread: 0.085, Cooldown: 0.85,  MagSize: 6,  ReloadTime: 1.5, Ammo.Shells,  Sfx.Shotgun,  Kick: 1.0),
        new("Chaingun", Damage: 11, Pellets: 1, Spread: 0.035, Cooldown: 0.095, MagSize: 50, ReloadTime: 2.0, Ammo.Bullets, Sfx.Chaingun, Kick: 0.35),
        // Damage = splash damage at the centre of the blast
        new("Rockets",  Damage: 120, Pellets: 1, Spread: 0,    Cooldown: 0.9,   MagSize: 4,  ReloadTime: 2.2, Ammo.Rockets, Sfx.Rocket,   Kick: 1.0),
    };
    public const int MaxHealth = 100, MaxArmor = 100, MaxBullets = 200, MaxShells = 50, MaxRockets = 20;
    public const double HeadZone = 0.75;     // top quarter of an enemy is its head
    public const int HeadshotMultiplier = 2;
    public const double BankDamage = 0.6;    // a bullet keeps this much damage after bouncing off a wall
    public const double RocketSpeed = 10;    // cells per second
    public const double SplashRadius = 2.3;
    public const double SelfSplash = 0.5;    // share of splash damage the player takes from their own rocket
    public const double ExplosionTime = 0.35;
    public const double LongShotRange = 9;   // cells
    public const double SpinNeeded = 4;      // radians of recent turning for a spin shot (a fast 360)
    public const int HeadshotBonus = 50, LongShotBonus = 75, DoubleKillBonus = 100, MultiKillBonus = 200, BankShotBonus = 150, SpinShotBonus = 200;
    public const double SwitchTime = 0.25;   // weapon drops and comes back up
    public const double FlashTime = 0.06;    // muzzle flash on screen
    public const int LevelBonus = 500;       // score per cleared level, times depth

    public int Health = MaxHealth, Armor;
    public int Bullets, Shells, Rockets;     // reserve ammo (not yet in a magazine)
    public readonly int[] Mag = new int[Weapons.Length];
    public readonly bool[] Owned = new bool[Weapons.Length];
    public int Weapon;
    public double Cooldown, ReloadLeft, SwitchLeft;
    public double MuzzleFlash;               // seconds of flash left
    public double Recoil;                    // 0..1, decays — kicks the weapon sprite
    public double HurtFlash, Shake;          // seconds left — red tint and view shake
    public int Kills, Score;
    public int Tricks;                       // trickshot bonuses earned this run
    public readonly List<Missile> Missiles = new();
    public readonly List<Explosion> Explosions = new();

    public bool Dead => Health <= 0;
    public WeaponDef Gun => Weapons[Weapon];
    public int Reserve => Gun.Ammo switch { Ammo.Shells => Shells, Ammo.Rockets => Rockets, _ => Bullets };

    private Random _rnd = new(1);
    private int _shotKills; // kills by the current trigger pull or blast — two or more is a multi-kill

    public Engine() => ResetRun();

    private void ResetRun()
    {
        _rnd = new Random(Seed);
        Health = MaxHealth;
        Armor = 0;
        Bullets = 36;
        Shells = Rockets = 0;
        Kills = Score = Tricks = 0;
        Weapon = 0;
        Array.Clear(Owned);
        Owned[0] = true;
        for (var i = 0; i < Mag.Length; i++) Mag[i] = Weapons[i].MagSize;
        Cooldown = ReloadLeft = SwitchLeft = MuzzleFlash = Recoil = HurtFlash = Shake = 0;
    }

    private void OnLevelComplete() => Score += LevelBonus * Depth;

    private void TickCombat(double dt, in PlayerInput input)
    {
        Cooldown -= dt;
        MuzzleFlash -= dt;
        HurtFlash -= dt;
        Shake -= dt;
        Recoil = Math.Max(0, Recoil - dt * 5);
        if (SwitchLeft > 0) SwitchLeft -= dt;
        TickMissiles(dt);

        if (input.Weapon != 0) SelectWeapon(input.Weapon);

        if (ReloadLeft > 0)
        {
            ReloadLeft -= dt;
            if (ReloadLeft <= 0) FinishReload();
            return;
        }

        if (input.Reload) StartReload();
        else if (input.Fire && SwitchLeft <= 0 && Cooldown <= 0) Fire();
    }

    private void SelectWeapon(int request)
    {
        var target = Weapon;
        if (request > 0 && request <= Weapons.Length && Owned[request - 1]) target = request - 1;
        else if (request < 0)
        {
            // cycle to the next owned weapon (the one touch button)
            for (var i = 1; i <= Weapons.Length; i++)
            {
                var w = (Weapon + i) % Weapons.Length;
                if (!Owned[w]) continue;
                target = w;
                break;
            }
        }
        if (target == Weapon) return;
        Weapon = target;
        SwitchLeft = SwitchTime;
        ReloadLeft = 0; // switching abandons a reload in progress
    }

    private void Fire()
    {
        var gun = Gun;
        if (Mag[Weapon] <= 0)
        {
            if (!StartReload())
            {
                Cooldown = 0.4;
                Sounds.Add(Sfx.Empty);
            }
            return;
        }

        Mag[Weapon]--;
        Cooldown = gun.Cooldown;
        MuzzleFlash = FlashTime;
        Recoil = Math.Min(1, Recoil + gun.Kick);
        if (gun.Kick >= 1) Shake = 0.12;
        Sounds.Add(gun.Sound);
        AlertEnemies();

        _shotKills = 0;
        if (gun.Ammo == Ammo.Rockets)
        {
            var (cos, sin) = (Math.Cos(Angle), Math.Sin(Angle));
            Missiles.Add(new Missile { X = Px + cos * 0.3, Y = Py + sin * 0.3, Z = 0.45, Vx = cos * RocketSpeed, Vy = sin * RocketSpeed, Vz = Pitch * RocketSpeed });
        }
        else
        {
            for (var i = 0; i < gun.Pellets; i++)
            {
                // spread is a cone: sideways and up/down
                var angle = Angle + (_rnd.NextDouble() * 2 - 1) * gun.Spread;
                var slope = Pitch + (_rnd.NextDouble() * 2 - 1) * gun.Spread;
                FireRay(angle, slope, gun.Damage);
            }
        }
        if (Mag[Weapon] == 0) StartReload(); // auto-reload: nobody should need a reload key on a phone
    }

    /// <summary>Begins a reload if the magazine has room and there is ammo to put in it.</summary>
    public bool StartReload()
    {
        var gun = Gun;
        if (ReloadLeft > 0 || Mag[Weapon] >= gun.MagSize) return false;
        // the pistol is never dry: it tops up for free when the bullet reserve runs out, so a run cannot soft-lock
        if (Reserve <= 0 && Weapon != 0) return false;
        ReloadLeft = gun.ReloadTime;
        Sounds.Add(Sfx.Reload);
        return true;
    }

    private void FinishReload()
    {
        var gun = Gun;
        var need = gun.MagSize - Mag[Weapon];
        var take = Math.Min(need, Reserve);
        switch (gun.Ammo)
        {
            case Ammo.Shells: Shells -= take; break;
            case Ammo.Rockets: Rockets -= take; break;
            default: Bullets -= take; break;
        }
        Mag[Weapon] += Weapon == 0 ? need : take;
    }

    /// <summary>Distance a bullet travels along <paramref name="angle"/> before a wall or closed door stops it.</summary>
    public double RayToWall(double angle) => RayToWall(Px, Py, angle, out _);

    /// <summary>Same, from any point. <paramref name="side"/> is the wall face hit: 0 = facing east/west, 1 = facing north/south.</summary>
    public double RayToWall(double ox, double oy, double angle, out int side)
    {
        var rx = Math.Cos(angle);
        var ry = Math.Sin(angle);
        var mx = (int)ox;
        var my = (int)oy;
        var ddx = rx == 0 ? 1e30 : Math.Abs(1 / rx);
        var ddy = ry == 0 ? 1e30 : Math.Abs(1 / ry);
        var sdx = rx < 0 ? (ox - mx) * ddx : (mx + 1 - ox) * ddx;
        var sdy = ry < 0 ? (oy - my) * ddy : (my + 1 - oy) * ddy;
        side = 0;
        for (var i = 0; i < 128; i++)
        {
            double dist;
            if (sdx < sdy)
            {
                dist = sdx;
                sdx += ddx;
                mx += rx < 0 ? -1 : 1;
                side = 0;
            }
            else
            {
                dist = sdy;
                sdy += ddy;
                my += ry < 0 ? -1 : 1;
                side = 1;
            }
            if (Level.BlocksSight(mx, my)) return dist;
        }
        return 1e30;
    }

    /// <summary>
    /// One bullet. It flies along <paramref name="angle"/> rising <paramref name="slope"/> per cell, stops in the first
    /// enemy, the floor or the ceiling — and bounces once off a wall, which is what makes bank shots possible.
    /// </summary>
    private void FireRay(double angle, double slope, int damage)
    {
        var (ox, oy, oz) = (Px, Py, 0.5); // eye height
        var travelled = 0.0;
        for (var bounce = 0; bounce <= 1; bounce++)
        {
            var wall = RayToWall(ox, oy, angle, out var side);
            var vertical = slope > 1e-9 ? (1 - oz) / slope : slope < -1e-9 ? -oz / slope : 1e30; // ceiling or floor
            if (HitScan(ox, oy, oz, angle, slope, Math.Min(wall, vertical), damage, bounce > 0, travelled)) return;
            if (vertical <= wall || wall > 1e29) return;

            // reflect off the wall face and carry on, weaker
            var stop = Math.Max(0, wall - 0.02);
            ox += Math.Cos(angle) * stop;
            oy += Math.Sin(angle) * stop;
            oz += slope * stop;
            travelled += stop;
            angle = side == 0 ? Math.PI - angle : -angle;
            damage = (int)(damage * BankDamage);
        }
    }

    // ── rockets ──

    private void TickMissiles(double dt)
    {
        for (var i = Explosions.Count - 1; i >= 0; i--)
        {
            var ex = Explosions[i];
            ex.Age += dt;
            if (ex.Age >= ExplosionTime) Explosions.RemoveAt(i);
            else Explosions[i] = ex;
        }

        for (var i = Missiles.Count - 1; i >= 0; i--)
        {
            var m = Missiles[i];
            var steps = (int)(RocketSpeed * dt / 0.1) + 1; // sub-steps of at most 0.1 cell, so it cannot skip a wall or a body
            var h = dt / steps;
            var hit = false;
            for (var k = 0; k < steps && !hit; k++)
            {
                var (nx, ny, nz) = (m.X + m.Vx * h, m.Y + m.Vy * h, m.Z + m.Vz * h);
                hit = Level.BlocksSight((int)nx, (int)ny) || nz <= 0 || nz >= 1; // explode where it was, just short of the surface
                if (hit) break;
                (m.X, m.Y, m.Z) = (nx, ny, nz);
                foreach (var en in Enemies)
                {
                    if (!en.Alive) continue;
                    var def = EnemyDefs[(int)en.Kind];
                    var (dx, dy) = (en.X - m.X, en.Y - m.Y);
                    if (dx * dx + dy * dy < def.Radius * def.Radius && m.Z <= def.Size) hit = true;
                }
            }
            if (!hit)
            {
                Missiles[i] = m;
                continue;
            }
            Missiles.RemoveAt(i);
            Explode(m.X, m.Y, m.Z);
        }
    }

    /// <summary>Splash damage falls off linearly to nothing at SplashRadius, needs line of sight, and hurts the player too.</summary>
    public void Explode(double x, double y, double z)
    {
        var damage = Weapons[3].Damage;
        _shotKills = 0;
        Explosions.Add(new Explosion { X = x, Y = y, Z = z });
        Sounds.Add(Sfx.Explosion);
        Shake = 0.3;
        AlertEnemies();

        foreach (var en in Enemies.ToArray())
        {
            if (!en.Alive) continue;
            var d = Math.Sqrt((en.X - x) * (en.X - x) + (en.Y - y) * (en.Y - y));
            if (d >= SplashRadius || !Level.LineOfSight(x, y, en.X, en.Y)) continue;
            var fromPlayer = Math.Sqrt((en.X - Px) * (en.X - Px) + (en.Y - Py) * (en.Y - Py));
            Damage(en, (int)(damage * (1 - d / SplashRadius)), new HitInfo(Head: false, Banked: false, Splash: true, fromPlayer));
        }

        var self = Math.Sqrt((Px - x) * (Px - x) + (Py - y) * (Py - y));
        if (self < SplashRadius && Level.LineOfSight(x, y, Px, Py))
            Hurt((int)(damage * SelfSplash * (1 - self / SplashRadius)));
    }

    /// <summary>Called for every kill: adds up the style bonuses and announces them.</summary>
    private void AwardTricks(HitInfo hit)
    {
        _shotKills++;
        var names = new List<string>();
        var bonus = 0;
        void Add(string name, int points)
        {
            names.Add(name);
            bonus += points;
        }

        if (hit.Head) Add("HEADSHOT", HeadshotBonus);
        if (hit.Banked) Add("BANK SHOT", BankShotBonus);
        if (hit.Distance >= LongShotRange) Add("LONG SHOT", LongShotBonus);
        if (Math.Abs(_spin) >= SpinNeeded) Add("SPIN SHOT", SpinShotBonus);
        if (_shotKills == 2) Add("DOUBLE KILL", DoubleKillBonus);
        else if (_shotKills > 2) Add("MULTI KILL", MultiKillBonus);
        if (bonus == 0) return;

        Score += bonus;
        Tricks += names.Count;
        Say($"{string.Join(" + ", names)}  +{bonus}");
        Sounds.Add(Sfx.Trick);
    }

    /// <summary>Damage to the player. Armor soaks half of it while it lasts.</summary>
    public void Hurt(int damage)
    {
        if (Dead) return;
        var soaked = Math.Min(Armor, damage / 2);
        Armor -= soaked;
        Health = Math.Max(0, Health - (damage - soaked));
        HurtFlash = 0.35;
        Shake = 0.25;
        Sounds.Add(Dead ? Sfx.PlayerDie : Sfx.PlayerHurt);
    }

    /// <summary>False = leave it on the floor (already full), so nothing is wasted.</summary>
    private bool ApplyItem(PickupKind kind)
    {
        switch (kind)
        {
            case PickupKind.Health:
                if (Health >= MaxHealth) return false;
                Health = Math.Min(MaxHealth, Health + 25);
                Say("Health +25");
                break;
            case PickupKind.Armor:
                if (Armor >= MaxArmor) return false;
                Armor = Math.Min(MaxArmor, Armor + 50);
                Say("Armor +50");
                break;
            case PickupKind.Bullets:
                if (Bullets >= MaxBullets) return false;
                Bullets = Math.Min(MaxBullets, Bullets + 30);
                Say("Bullets +30");
                break;
            case PickupKind.Shells:
                if (Shells >= MaxShells) return false;
                Shells = Math.Min(MaxShells, Shells + 8);
                Say("Shells +8");
                break;
            case PickupKind.Rockets:
                if (Rockets >= MaxRockets) return false;
                Rockets = Math.Min(MaxRockets, Rockets + 4);
                Say("Rockets +4");
                break;
            case PickupKind.Shotgun:
                return TakeWeapon(1, shells: 8, bullets: 0, rockets: 0);
            case PickupKind.Chaingun:
                return TakeWeapon(2, shells: 0, bullets: 40, rockets: 0);
            case PickupKind.RocketLauncher:
                return TakeWeapon(3, shells: 0, bullets: 0, rockets: 4);
            default:
                return false;
        }
        Sounds.Add(Sfx.Pickup);
        return true;
    }

    private bool TakeWeapon(int index, int shells, int bullets, int rockets)
    {
        Rockets = Math.Min(MaxRockets, Rockets + rockets);
        Shells = Math.Min(MaxShells, Shells + shells);
        Bullets = Math.Min(MaxBullets, Bullets + bullets);
        if (Owned[index])
        {
            Say($"{Weapons[index].Name} ammo");
            Sounds.Add(Sfx.Pickup);
            return true;
        }
        Owned[index] = true;
        Say($"{Weapons[index].Name}!");
        Sounds.Add(Sfx.WeaponGet);
        SelectWeapon(index + 1);
        return true;
    }
}
