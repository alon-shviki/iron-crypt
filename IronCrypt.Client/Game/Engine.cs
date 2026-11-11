namespace IronCrypt.Client.Game;

/// <summary>Sound events the engine emits; the page forwards the ids to the WebAudio synth in gameInterop.js.</summary>
public enum Sfx
{
    Step, Door, DoorLocked, Pistol, Shotgun, Chaingun, Empty, Reload, Pickup, Key,
    EnemyHurt, EnemyDie, EnemyAlert, EnemyShot, PlayerHurt, PlayerDie, LevelDone, WeaponGet,
}

/// <summary>One frame of player intent, already device-independent (keyboard, mouse and touch all fold into this).</summary>
public struct PlayerInput
{
    public double Forward, Strafe; // -1..1
    public double Turn;            // radians to rotate this frame
    public bool Fire, Reload;
    public int Weapon;             // 0 = keep, 1..3 = select, -1 = cycle
}

public class Pickup
{
    public PickupKind Kind;
    public double X, Y;
}

/// <summary>Whole simulation: player, doors, combat, enemies, progression. No rendering, no interop — testable head-less.</summary>
public partial class Engine
{
    // ── tuning knobs ──
    public const double PlayerRadius = 0.22;
    public const double MoveSpeed = 3.4;     // cells per second
    public const double MaxDt = 0.05;        // clamp: a long frame must never tunnel through a wall
    public const double StepInterval = 0.42; // seconds between footstep sounds while moving
    public const double DoorReach = 1.5;     // doors slide open when the player is this close
    public const double DoorSpeed = 2.5;     // open fraction per second
    public const float DoorHold = 3f;        // seconds a door stays open after the last trigger
    public const double PickupReach = 0.55;

    public Level Level = Level.FromRows("###", "#S#", "###");
    public int Seed;
    public int Depth;
    public double Px, Py, Angle;
    public double Bob;                       // walk-cycle phase, drives weapon sway
    public bool Moving;
    public double Time;                      // seconds of simulated play on this level
    public bool HasRedKey, HasBlueKey;
    public bool LevelComplete;
    public string Message = "";              // short on-screen notice ("You need the red key")
    public double MessageTime;
    public readonly List<Pickup> Pickups = new();
    public readonly List<Sfx> Sounds = new();

    private readonly List<int> _doors = new();
    private double _stepTimer;
    private double _lockedCooldown;

    /// <summary>Any text is a valid seed: numbers are used as-is, anything else is hashed (stable across runs).</summary>
    public static int SeedFromText(string? text)
    {
        text = (text ?? "").Trim();
        if (int.TryParse(text, out var n)) return n;
        var hash = 2166136261u; // FNV-1a — string.GetHashCode is randomized per process, so it cannot be a seed
        foreach (var ch in text) hash = (hash ^ ch) * 16777619u;
        return (int)(hash & 0x7FFFFFFF);
    }

    public static int LevelSeed(int seed, int depth) => unchecked(seed * 31 + depth * 7919);

    public void NewGame(int seed)
    {
        Seed = seed;
        Depth = 0;
        ResetRun();
        NextLevel();
    }

    public void NextLevel()
    {
        Depth++;
        LoadLevel(LevelGenerator.Generate(LevelSeed(Seed, Depth), Depth));
    }

    public void LoadLevel(Level level)
    {
        Level = level;
        Px = level.StartX;
        Py = level.StartY;
        Angle = level.StartAngle;
        Time = 0;
        HasRedKey = HasBlueKey = false;
        LevelComplete = false;
        Message = "";
        MessageTime = 0;

        Pickups.Clear();
        foreach (var p in level.Pickups) Pickups.Add(new Pickup { Kind = p.Kind, X = p.X, Y = p.Y });
        _doors.Clear();
        for (var i = 0; i < level.Cells.Length; i++)
            if (Cell.IsDoor(level.Cells[i])) _doors.Add(i);
        OnLevelLoaded();
    }

    public void Tick(double dt, in PlayerInput input)
    {
        if (LevelComplete || Dead) return;
        dt = Math.Min(dt, MaxDt);
        Time += dt;
        MessageTime -= dt;
        _lockedCooldown -= dt;

        MovePlayer(dt, input);
        TickCombat(dt, input);
        TickEnemies(dt);
        TickDoors(dt);
        CollectPickups();

        if ((int)Px == Level.ExitX && (int)Py == Level.ExitY)
        {
            LevelComplete = true;
            OnLevelComplete();
            Sounds.Add(Sfx.LevelDone);
        }
    }

    public void Say(string message)
    {
        Message = message;
        MessageTime = 2.5;
    }

    private void MovePlayer(double dt, in PlayerInput input)
    {
        Angle = (Angle + input.Turn) % (Math.PI * 2);

        var f = Math.Clamp(input.Forward, -1, 1);
        var s = Math.Clamp(input.Strafe, -1, 1);
        var mag = Math.Sqrt(f * f + s * s);
        Moving = mag > 0.05;
        if (!Moving) return;
        if (mag > 1)
        {
            // diagonal is not faster
            f /= mag;
            s /= mag;
        }

        var cos = Math.Cos(Angle);
        var sin = Math.Sin(Angle);
        var dx = (cos * f - sin * s) * MoveSpeed * dt;
        var dy = (sin * f + cos * s) * MoveSpeed * dt;

        // axis-separated so the player slides along walls instead of sticking
        if (IsFree(Px + dx, Py)) Px += dx;
        if (IsFree(Px, Py + dy)) Py += dy;

        Bob += dt * 9;
        _stepTimer -= dt;
        if (_stepTimer <= 0)
        {
            _stepTimer = StepInterval;
            Sounds.Add(Sfx.Step);
        }
    }

    /// <summary>A body of radius r at (x, y) touches no blocking cell (checked at its 4 corners).</summary>
    public bool IsFree(double x, double y, double r = PlayerRadius) =>
        !Level.Blocks((int)(x - r), (int)(y - r)) && !Level.Blocks((int)(x + r), (int)(y - r)) &&
        !Level.Blocks((int)(x - r), (int)(y + r)) && !Level.Blocks((int)(x + r), (int)(y + r));

    // ── doors ──

    /// <summary>Starts (or keeps) a door opening. Enemies call this too.</summary>
    public void OpenDoor(int index)
    {
        if (Level.DoorOpen[index] == 0 && Level.DoorTimer[index] <= 0) Sounds.Add(Sfx.Door);
        Level.DoorTimer[index] = DoorHold;
    }

    private void TickDoors(double dt)
    {
        var lvl = Level;
        foreach (var i in _doors)
        {
            var cx = i % lvl.W + 0.5;
            var cy = i / lvl.W + 0.5;
            var dx = Px - cx;
            var dy = Py - cy;

            if (dx * dx + dy * dy < DoorReach * DoorReach)
            {
                var cell = lvl.Cells[i];
                var locked = (cell == Cell.DoorRed && !HasRedKey) || (cell == Cell.DoorBlue && !HasBlueKey);
                if (!locked) OpenDoor(i);
                else if (_lockedCooldown <= 0)
                {
                    _lockedCooldown = 2;
                    Say(cell == Cell.DoorRed ? "You need the red key" : "You need the blue key");
                    Sounds.Add(Sfx.DoorLocked);
                }
            }

            if (lvl.DoorTimer[i] > 0)
            {
                lvl.DoorOpen[i] = Math.Min(1f, lvl.DoorOpen[i] + (float)(DoorSpeed * dt));
                if (lvl.DoorOpen[i] >= 1f) lvl.DoorTimer[i] -= (float)dt;
            }
            else if (lvl.DoorOpen[i] > 0 && !DoorwayOccupied(cx, cy))
            {
                lvl.DoorOpen[i] = Math.Max(0f, lvl.DoorOpen[i] - (float)(DoorSpeed * dt));
            }
        }
    }

    /// <summary>A door never closes on a body — that is how things would get stuck inside walls.</summary>
    private bool DoorwayOccupied(double cx, double cy)
    {
        const double Clear = 0.95;
        if (Math.Abs(Px - cx) < Clear && Math.Abs(Py - cy) < Clear) return true;
        return EnemyNear(cx, cy, Clear);
    }

    // ── pickups ──

    private void CollectPickups()
    {
        for (var i = Pickups.Count - 1; i >= 0; i--)
        {
            var p = Pickups[i];
            var dx = p.X - Px;
            var dy = p.Y - Py;
            if (dx * dx + dy * dy > PickupReach * PickupReach) continue;
            if (ApplyPickup(p.Kind)) Pickups.RemoveAt(i);
        }
    }

    private bool ApplyPickup(PickupKind kind)
    {
        switch (kind)
        {
            case PickupKind.KeyRed:
                HasRedKey = true;
                Say("Red key");
                Sounds.Add(Sfx.Key);
                return true;
            case PickupKind.KeyBlue:
                HasBlueKey = true;
                Say("Blue key");
                Sounds.Add(Sfx.Key);
                return true;
            default:
                return ApplyItem(kind);
        }
    }
}
