namespace IronCrypt.Client.Game;

/// <summary>One gun. Bullets and shells are the two ammo pools; the pistol and chaingun share bullets.</summary>
public record WeaponDef(string Name, int Damage, int Pellets, double Spread, double Cooldown,
    int MagSize, double ReloadTime, bool UsesShells, Sfx Sound, double Kick);

// Player combat: weapons, ammo, reloads, damage taken, item pickups, score.
public partial class Engine
{
    // ── tuning knobs ──
    public static readonly WeaponDef[] Weapons =
    {
        new("Pistol",   Damage: 14, Pellets: 1, Spread: 0.012, Cooldown: 0.32,  MagSize: 12, ReloadTime: 1.0, UsesShells: false, Sfx.Pistol,   Kick: 0.5),
        new("Shotgun",  Damage: 9,  Pellets: 7, Spread: 0.085, Cooldown: 0.85,  MagSize: 6,  ReloadTime: 1.5, UsesShells: true,  Sfx.Shotgun,  Kick: 1.0),
        new("Chaingun", Damage: 11, Pellets: 1, Spread: 0.035, Cooldown: 0.095, MagSize: 50, ReloadTime: 2.0, UsesShells: false, Sfx.Chaingun, Kick: 0.35),
    };
    public const int MaxHealth = 100, MaxArmor = 100, MaxBullets = 200, MaxShells = 50;
    public const double SwitchTime = 0.25;   // weapon drops and comes back up
    public const double FlashTime = 0.06;    // muzzle flash on screen
    public const int LevelBonus = 500;       // score per cleared level, times depth

    public int Health = MaxHealth, Armor;
    public int Bullets, Shells;              // reserve ammo (not yet in a magazine)
    public readonly int[] Mag = new int[3];
    public readonly bool[] Owned = new bool[3];
    public int Weapon;
    public double Cooldown, ReloadLeft, SwitchLeft;
    public double MuzzleFlash;               // seconds of flash left
    public double Recoil;                    // 0..1, decays — kicks the weapon sprite
    public double HurtFlash, Shake;          // seconds left — red tint and view shake
    public int Kills, Score;

    public bool Dead => Health <= 0;
    public WeaponDef Gun => Weapons[Weapon];
    public int Reserve => Gun.UsesShells ? Shells : Bullets;

    private Random _rnd = new(1);

    public Engine() => ResetRun();

    private void ResetRun()
    {
        _rnd = new Random(Seed);
        Health = MaxHealth;
        Armor = 0;
        Bullets = 36;
        Shells = 0;
        Kills = Score = 0;
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

        for (var i = 0; i < gun.Pellets; i++)
        {
            var angle = Angle + (_rnd.NextDouble() * 2 - 1) * gun.Spread;
            HitScan(angle, RayToWall(angle), gun.Damage);
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
        if (gun.UsesShells) Shells -= take;
        else Bullets -= take;
        Mag[Weapon] += Weapon == 0 ? need : take;
    }

    /// <summary>Distance a bullet travels along <paramref name="angle"/> before a wall or closed door stops it.</summary>
    public double RayToWall(double angle)
    {
        var rx = Math.Cos(angle);
        var ry = Math.Sin(angle);
        var mx = (int)Px;
        var my = (int)Py;
        var ddx = rx == 0 ? 1e30 : Math.Abs(1 / rx);
        var ddy = ry == 0 ? 1e30 : Math.Abs(1 / ry);
        var sdx = rx < 0 ? (Px - mx) * ddx : (mx + 1 - Px) * ddx;
        var sdy = ry < 0 ? (Py - my) * ddy : (my + 1 - Py) * ddy;
        for (var i = 0; i < 128; i++)
        {
            double dist;
            if (sdx < sdy)
            {
                dist = sdx;
                sdx += ddx;
                mx += rx < 0 ? -1 : 1;
            }
            else
            {
                dist = sdy;
                sdy += ddy;
                my += ry < 0 ? -1 : 1;
            }
            if (Level.BlocksSight(mx, my)) return dist;
        }
        return 1e30;
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
            case PickupKind.Shotgun:
                return TakeWeapon(1, shells: 8, bullets: 0);
            case PickupKind.Chaingun:
                return TakeWeapon(2, shells: 0, bullets: 40);
            default:
                return false;
        }
        Sounds.Add(Sfx.Pickup);
        return true;
    }

    private bool TakeWeapon(int index, int shells, int bullets)
    {
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
