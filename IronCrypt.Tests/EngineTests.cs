using IronCrypt.Client.Game;

namespace IronCrypt.Tests;

public class EngineTests
{
    private static readonly string[] Room =
    {
        "#######",
        "#.....#",
        "#..S..#",
        "#.....#",
        "#######",
    };

    private static Engine NewEngine(params string[] rows)
    {
        var e = new Engine();
        e.LoadLevel(Level.FromRows(rows.Length == 0 ? Room : rows));
        return e;
    }

    private static void Run(Engine e, double seconds, PlayerInput input)
    {
        for (var t = 0.0; t < seconds; t += 1 / 60.0) e.Tick(1 / 60.0, input);
    }

    // ── Phase 1: movement + collision ──

    [Fact]
    public void Player_StopsAtWall()
    {
        var e = NewEngine();
        Run(e, 5, new PlayerInput { Forward = 1 }); // facing +x, wall at x = 6

        Assert.True(e.Px > 5.5, "should have walked up to the wall");
        Assert.True(e.Px < 6 - Engine.PlayerRadius + 0.001, "must not penetrate the wall");
        Assert.True(e.IsFree(e.Px, e.Py));
    }

    [Fact]
    public void Player_SlidesAlongWall()
    {
        var e = NewEngine();
        e.Angle = Math.PI / 4; // diagonal: the south wall (y = 4) is reached first, x stays free
        Run(e, 0.6, new PlayerInput { Forward = 1 });
        var xWhenBlocked = e.Px;
        var yWhenBlocked = e.Py;
        Run(e, 0.2, new PlayerInput { Forward = 1 });

        Assert.Equal(yWhenBlocked, e.Py, 3);                 // pinned against the wall…
        Assert.True(e.Px > xWhenBlocked + 0.2, "…but still sliding along it");
        Assert.True(e.IsFree(e.Px, e.Py));
    }

    [Fact]
    public void Player_LongFrameCannotTunnel()
    {
        var e = NewEngine();
        for (var i = 0; i < 50; i++) e.Tick(10.0, new PlayerInput { Forward = 1 }); // absurd dt gets clamped
        Assert.True(e.Px < 6);
        Assert.True(e.IsFree(e.Px, e.Py));
    }

    [Fact]
    public void Player_RandomWalkNeverEndsInsideAWall()
    {
        var e = NewEngine();
        var rnd = new Random(42);
        for (var i = 0; i < 20000; i++)
        {
            e.Tick(rnd.NextDouble() * 0.06, new PlayerInput
            {
                Forward = rnd.NextDouble() * 2 - 1,
                Strafe = rnd.NextDouble() * 2 - 1,
                Turn = rnd.NextDouble() - 0.5,
            });
            Assert.True(e.IsFree(e.Px, e.Py), $"stuck in wall at step {i}: {e.Px},{e.Py}");
        }
    }

    [Fact]
    public void ClosedDoor_BlocksMovementAndSight()
    {
        var e = NewEngine("#####", "#SD.#", "#####");
        Run(e, 2, new PlayerInput { Forward = 1 });

        Assert.True(e.Px < 2, "closed door is solid");
        Assert.False(e.Level.LineOfSight(1.5, 1.5, 3.5, 1.5));
    }

    // ── Renderer ──

    [Fact]
    public void Renderer_DrawsTexturedWallAhead()
    {
        var e = NewEngine();
        var r = new Renderer(320, 200);
        var px = r.Render(e);

        Assert.Equal(320 * 200 * 4, px.Length);
        var center = r.Fb[100 * 320 + 160];
        var ceiling = r.Fb[0];
        var floor = r.Fb[199 * 320 + 160];
        Assert.NotEqual(ceiling, center);
        Assert.NotEqual(floor, center);
        Assert.Equal(0xFF000000u, center & 0xFF000000u); // opaque
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(390, 844)]   // phone portrait
    [InlineData(844, 390)]   // phone landscape
    [InlineData(3440, 1440)] // ultrawide
    public void Renderer_SizeFor_FollowsViewportAndRenders(double vw, double vh)
    {
        var (w, h) = Renderer.SizeFor(vw, vh);
        Assert.InRange(w, 200, 480);
        Assert.InRange(h, 200, 440);
        Assert.Equal(vw >= vh, w >= h);

        var e = NewEngine();
        var r = new Renderer(w, h);
        e.Angle = 1.1;
        r.Render(e); // must not throw at any size
    }
}
