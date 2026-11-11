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

/// <summary>Whole simulation: player, doors, combat, enemies, progression. No rendering, no interop — testable head-less.</summary>
public partial class Engine
{
    // ── tuning knobs ──
    public const double PlayerRadius = 0.22;
    public const double MoveSpeed = 3.4;     // cells per second
    public const double MaxDt = 0.05;        // clamp: a long frame must never tunnel through a wall
    public const double StepInterval = 0.42; // seconds between footstep sounds while moving

    public Level Level = Level.FromRows("###", "#S#", "###");
    public double Px, Py, Angle;
    public double Bob;                       // walk-cycle phase, drives weapon sway
    public bool Moving;
    public readonly List<Sfx> Sounds = new();

    private double _stepTimer;

    public void LoadLevel(Level level)
    {
        Level = level;
        Px = level.StartX;
        Py = level.StartY;
        Angle = level.StartAngle;
    }

    public void Tick(double dt, in PlayerInput input)
    {
        dt = Math.Min(dt, MaxDt);
        MovePlayer(dt, input);
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
}
