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

    // ── Phase 2: levels, doors, keys, exit ──

    [Fact]
    public void Generator_IsDeterministicPerSeed()
    {
        var a = LevelGenerator.Generate(1234, 3);
        var b = LevelGenerator.Generate(1234, 3);
        var c = LevelGenerator.Generate(1235, 3);

        Assert.Equal(a.Cells, b.Cells);
        Assert.Equal(a.Enemies, b.Enemies);
        Assert.Equal(a.Pickups, b.Pickups);
        Assert.Equal((a.StartX, a.StartY, a.ExitX, a.ExitY), (b.StartX, b.StartY, b.ExitX, b.ExitY));
        Assert.NotEqual(a.Cells, c.Cells);
    }

    [Fact]
    public void Generator_EveryLevelIsSolvable_KeysBeforeTheirDoors()
    {
        var levels = 0;
        var withLock = 0;
        for (var seed = 1; seed <= 150; seed++)
        {
            for (var depth = 1; depth <= 8; depth += 1)
            {
                var lvl = LevelGenerator.Generate(seed, depth);
                var (sx, sy) = ((int)lvl.StartX, (int)lvl.StartY);
                int CellOf(PickupKind k) => lvl.Pickups.Where(p => p.Kind == k).Select(p => (int)p.Y * lvl.W + (int)p.X).DefaultIfEmpty(-1).First();
                var hasRed = lvl.Cells.Contains(Cell.DoorRed);
                var hasBlue = lvl.Cells.Contains(Cell.DoorBlue);
                var where = $"seed {seed} depth {depth}";
                levels++;
                if (hasRed) withLock++;

                // walk the key chain in the order the player must: nothing → blue → red → exit
                var none = LevelGenerator.Flood(lvl, sx, sy, false, false);
                if (hasBlue) Assert.True(none[CellOf(PickupKind.KeyBlue)] >= 0, $"blue key unreachable, {where}");
                var withBlue = LevelGenerator.Flood(lvl, sx, sy, false, hasBlue);
                if (hasRed) Assert.True(withBlue[CellOf(PickupKind.KeyRed)] >= 0, $"red key unreachable, {where}");
                var all = LevelGenerator.Flood(lvl, sx, sy, true, true);
                Assert.True(all[lvl.ExitY * lvl.W + lvl.ExitX] > 0, $"exit unreachable, {where}");

                // a locked door must actually gate something, or the key is pointless
                if (hasRed) Assert.True(none[lvl.ExitY * lvl.W + lvl.ExitX] < 0, $"red door does not gate the exit, {where}");
            }
        }
        Assert.True(withLock * 2 > levels, $"keys should be the norm, not the exception: {withLock}/{levels} levels had a locked door");
    }

    [Fact]
    public void Generator_DoorsSitBetweenSolidJambs()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var lvl = LevelGenerator.Generate(seed, 4);
            for (var y = 1; y < lvl.H - 1; y++)
            {
                for (var x = 1; x < lvl.W - 1; x++)
                {
                    if (!Cell.IsDoor(lvl.At(x, y))) continue;
                    bool Wall(int cx, int cy) => lvl.At(cx, cy) is > 0 and < Cell.Door;
                    var horizontalPassage = Wall(x, y - 1) && Wall(x, y + 1) && lvl.At(x - 1, y) == 0 && lvl.At(x + 1, y) == 0;
                    var verticalPassage = Wall(x - 1, y) && Wall(x + 1, y) && lvl.At(x, y - 1) == 0 && lvl.At(x, y + 1) == 0;
                    Assert.True(horizontalPassage || verticalPassage, $"door at {x},{y} seed {seed} is not a proper doorway");
                }
            }
        }
    }

    [Fact]
    public void Generator_NothingSpawnsInsideWalls_OrOnTopOfEachOther_OrNearTheStart()
    {
        for (var seed = 1; seed <= 150; seed++)
        {
            var depth = 1 + seed % 9;
            var lvl = LevelGenerator.Generate(seed, depth);
            var dist = LevelGenerator.Flood(lvl, (int)lvl.StartX, (int)lvl.StartY, true, true);
            var used = new HashSet<int> { (int)lvl.StartY * lvl.W + (int)lvl.StartX, lvl.ExitY * lvl.W + lvl.ExitX };

            Assert.NotEmpty(lvl.Enemies);
            foreach (var (x, y) in lvl.Enemies.Select(s => (s.X, s.Y)).Concat(lvl.Pickups.Select(s => (s.X, s.Y))))
            {
                var cell = (int)y * lvl.W + (int)x;
                Assert.Equal(Cell.Floor, lvl.Cells[cell]);
                Assert.True(dist[cell] >= 0, $"unreachable spawn, seed {seed}");
                Assert.True(used.Add(cell), $"two things share cell {cell}, seed {seed}");
            }
            foreach (var en in lvl.Enemies)
                Assert.True(dist[(int)en.Y * lvl.W + (int)en.X] >= LevelGenerator.MinEnemyDistance, $"enemy too close to start, seed {seed}");
        }
    }

    [Fact]
    public void Seed_TextIsStable_AndLevelsDifferByDepth()
    {
        Assert.Equal(42, Engine.SeedFromText(" 42 "));
        Assert.Equal(Engine.SeedFromText("crypt"), Engine.SeedFromText("crypt"));
        Assert.NotEqual(Engine.SeedFromText("crypt"), Engine.SeedFromText("crypts"));
        Assert.NotEqual(Engine.LevelSeed(7, 1), Engine.LevelSeed(7, 2));

        var a = new Engine();
        var b = new Engine();
        a.NewGame(99);
        b.NewGame(99);
        a.NextLevel();
        b.NextLevel();
        Assert.Equal(2, a.Depth);
        Assert.Equal(a.Level.Cells, b.Level.Cells);
    }

    [Fact]
    public void Door_SlidesOpenWhenApproached_AndLetsThePlayerThrough()
    {
        var e = NewEngine("######", "#S.D.#", "######");
        Assert.False(e.Level.LineOfSight(1.5, 1.5, 4.5, 1.5)); // closed door blocks sight
        Run(e, 3, new PlayerInput { Forward = 1 });

        Assert.True(e.Px > 4, "walked through the opened door");
        Assert.Contains(Sfx.Door, e.Sounds);
    }

    [Fact]
    public void Door_ClosesBehindThePlayer_ButNeverOnThem()
    {
        var e = NewEngine("#########", "#S.D....#", "#########");
        var door = 1 * e.Level.W + 3;
        Run(e, 0.75, new PlayerInput { Forward = 1 });
        e.Px = 3.5; // park inside the doorway
        Run(e, 10, default);
        Assert.True(e.Level.DoorOpen[door] >= 0.8f, "door must not close on a body");
        Assert.True(e.IsFree(e.Px, e.Py));

        e.Px = 7.5;
        Run(e, 10, default);
        Assert.Equal(0f, e.Level.DoorOpen[door]);
    }

    [Fact]
    public void LockedDoor_NeedsItsKey()
    {
        var e = NewEngine("#######", "#S..R.#", "#######");
        Run(e, 3, new PlayerInput { Forward = 1 });
        Assert.True(e.Px < 4, "locked door stays shut");
        Assert.Contains(Sfx.DoorLocked, e.Sounds);
        Assert.Contains("red key", e.Message);

        e.Pickups.Add(new Pickup { Kind = PickupKind.KeyRed, X = e.Px, Y = e.Py });
        Run(e, 3, new PlayerInput { Forward = 1 });
        Assert.True(e.HasRedKey);
        Assert.Empty(e.Pickups);
        Assert.True(e.Px > 5, "key opens it");
    }

    [Fact]
    public void Exit_CompletesTheLevel()
    {
        var e = NewEngine("#####", "#S.X#", "#####");
        Run(e, 2, new PlayerInput { Forward = 1 });
        Assert.True(e.LevelComplete);
        Assert.Contains(Sfx.LevelDone, e.Sounds);
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
