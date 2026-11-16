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

    // ── Phase 3: combat ──

    private static readonly PlayerInput Fire = new() { Fire = true };

    [Fact]
    public void Firing_SpendsAmmo_AndRespectsTheCooldown()
    {
        var e = NewEngine();
        e.Tick(0.016, Fire);
        Assert.Equal(11, e.Mag[0]);
        Assert.Contains(Sfx.Pistol, e.Sounds);
        Assert.True(e.MuzzleFlash > 0);
        Assert.True(e.Recoil > 0);

        e.Tick(0.016, Fire); // still cooling down
        Assert.Equal(11, e.Mag[0]);
        Run(e, 1.0, Fire);   // 0.32 s per shot
        Assert.InRange(e.Mag[0], 7, 9);
    }

    [Fact]
    public void EmptyMagazine_AutoReloads_AfterTheReloadTime()
    {
        var e = NewEngine();
        e.Mag[0] = 1;
        var reserve = e.Bullets;
        e.Tick(0.016, Fire);
        Assert.Equal(0, e.Mag[0]);
        Assert.True(e.ReloadLeft > 0);
        Assert.Contains(Sfx.Reload, e.Sounds);

        Run(e, 0.5, Fire); // mid-reload: cannot fire
        Assert.Equal(0, e.Mag[0]);
        Run(e, 0.6, default);
        Assert.Equal(12, e.Mag[0]);
        Assert.Equal(reserve - 12, e.Bullets);
    }

    [Fact]
    public void Pistol_IsNeverDry_ButOtherGunsNeedAmmo()
    {
        var e = NewEngine();
        e.Bullets = 0;
        e.Mag[0] = 0;
        Assert.True(e.StartReload());
        Run(e, 1.1, default);
        Assert.Equal(12, e.Mag[0]);
        Assert.Equal(0, e.Bullets);

        e.Owned[1] = true;
        e.Tick(0.016, new PlayerInput { Weapon = 2 });
        Run(e, 0.5, default);
        Assert.Equal(1, e.Weapon);
        e.Mag[1] = 0;
        e.Shells = 0;
        e.Sounds.Clear();
        e.Tick(0.016, Fire);
        Assert.Contains(Sfx.Empty, e.Sounds);
        Assert.False(e.ReloadLeft > 0);
    }

    [Fact]
    public void WeaponSelect_OnlyOwnedWeapons_AndCycleSkipsTheRest()
    {
        var e = NewEngine();
        e.Tick(0.016, new PlayerInput { Weapon = 3 });
        Assert.Equal(0, e.Weapon); // chaingun not owned

        e.Owned[2] = true;
        e.Tick(0.016, new PlayerInput { Weapon = -1 });
        Assert.Equal(2, e.Weapon); // cycle skips the unowned shotgun
        Assert.True(e.SwitchLeft > 0);
        var mag = e.Mag[2];
        e.Tick(0.016, Fire);       // cannot fire while the weapon is coming up
        Assert.Equal(mag, e.Mag[2]);
    }

    [Fact]
    public void Pickups_Apply_AndStayOnTheFloorWhenFull()
    {
        var e = NewEngine();
        void Drop(PickupKind k) => e.Pickups.Add(new Pickup { Kind = k, X = e.Px, Y = e.Py });

        Drop(PickupKind.Health); // already at full health
        e.Tick(0.016, default);
        Assert.Single(e.Pickups);

        e.Hurt(40);
        e.Tick(0.016, default);
        Assert.Empty(e.Pickups);
        Assert.Equal(85, e.Health);

        Drop(PickupKind.Shotgun);
        e.Tick(0.016, default);
        Assert.True(e.Owned[1]);
        Assert.Equal(1, e.Weapon);
        Assert.Equal(8, e.Shells);
        Assert.Contains(Sfx.WeaponGet, e.Sounds);

        Drop(PickupKind.Armor);
        Drop(PickupKind.Bullets);
        Drop(PickupKind.Shells);
        e.Tick(0.016, default);
        Assert.Empty(e.Pickups);
        Assert.Equal((50, 66, 16), (e.Armor, e.Bullets, e.Shells));
    }

    [Fact]
    public void Armor_SoaksHalfTheDamage_AndDeathStopsTheSim()
    {
        var e = NewEngine();
        e.Armor = 10;
        e.Hurt(40); // armor takes min(10, 20) = 10
        Assert.Equal((70, 0), (e.Health, e.Armor));
        Assert.True(e.HurtFlash > 0);

        e.Hurt(500);
        Assert.True(e.Dead);
        Assert.Contains(Sfx.PlayerDie, e.Sounds);
        var x = e.Px;
        Run(e, 1, new PlayerInput { Forward = 1 });
        Assert.Equal(x, e.Px);
    }

    [Fact]
    public void Bullets_StopAtWallsAndClosedDoors()
    {
        var e = NewEngine("########", "#S...D.#", "########");
        Assert.Equal(3.5, e.RayToWall(0), 3);        // closed door at x = 5
        e.Level.DoorOpen[1 * 8 + 5] = 1f;
        Assert.Equal(5.5, e.RayToWall(0), 3);        // through the open door to the wall at x = 7
        Assert.Equal(0.5, e.RayToWall(Math.PI), 3);  // wall right behind
    }

    // ── Phase 4: enemies ──

    private static readonly string[] Hall =
    {
        "############",
        "#S.........#",
        "############",
    };

    [Fact]
    public void Shooting_KillsTheNearestEnemyInTheLineOfFire_AndScores()
    {
        var e = NewEngine(Hall);
        var near = e.SpawnEnemy(EnemyKind.Grunt, 5.5, 1.5)!;
        var far = e.SpawnEnemy(EnemyKind.Grunt, 9.5, 1.5)!;
        near.State = far.State = EnemyState.Pain; // hold them still for the test
        near.Timer = far.Timer = 100;

        for (var i = 0; i < 400 && near.Alive; i++) e.Tick(0.016, Fire);

        Assert.False(near.Alive);
        Assert.True(far.Alive, "the bullet stops in the first body");
        Assert.Equal(1, e.Kills);
        Assert.Equal(Engine.EnemyDefs[0].Score, e.Score);
        Assert.Contains(Sfx.EnemyDie, e.Sounds);
    }

    [Fact]
    public void Shooting_DoesNotHitThroughWalls_OrBehindThePlayer()
    {
        var e = NewEngine("#########", "#.S.#...#", "#########");
        var hidden = e.SpawnEnemy(EnemyKind.Grunt, 6.5, 1.5)!;
        var behind = e.SpawnEnemy(EnemyKind.Grunt, 1.5, 1.5)!;
        hidden.State = behind.State = EnemyState.Pain;
        hidden.Timer = behind.Timer = 100;
        behind.Hp = 10_000;
        var hiddenHp = hidden.Hp;
        e.Pitch = -0.03; // body height, so the numbers below are exact

        e.Tick(0.016, Fire);
        Assert.Equal(hiddenHp, hidden.Hp); // the wall stops the bullet…
        // …which bounces straight back: the one behind is only ever hit by the weakened ricochet
        Assert.Equal(10_000 - (int)(Engine.Weapons[0].Damage * Engine.BankDamage), behind.Hp);
    }

    [Fact]
    public void IdleEnemy_WakesOnSight_ButNotThroughAWall()
    {
        var e = NewEngine("#########", "#S..#...#", "#.......#", "#########");
        var hidden = e.SpawnEnemy(EnemyKind.Shooter, 6.5, 1.5)!; // wall between
        e.Tick(0.016, default);
        Assert.Equal(EnemyState.Idle, hidden.State);

        var seen = e.SpawnEnemy(EnemyKind.Shooter, 6.5, 2.5)!;
        e.Py = 2.5;
        e.Tick(0.016, default);
        Assert.NotEqual(EnemyState.Idle, seen.State);
        Assert.Contains(Sfx.EnemyAlert, e.Sounds);
    }

    [Fact]
    public void Gunshot_WakesEnemiesWithinEarshot()
    {
        var e = NewEngine("#########", "#S..#...#", "#.......#", "#########");
        var hidden = e.SpawnEnemy(EnemyKind.Grunt, 6.5, 1.5)!;
        e.Angle = Math.PI; // fire at the wall behind
        e.Tick(0.016, Fire);
        Assert.Equal(EnemyState.Chase, hidden.State);
    }

    [Fact]
    public void Grunt_PathfindsAroundWalls_AndMauls()
    {
        // the grunt starts with no line of sight and must route through the gap at the far end
        var e = NewEngine(
            "###########",
            "#S........#",
            "#########.#",
            "#.........#",
            "###########");
        var grunt = e.SpawnEnemy(EnemyKind.Grunt, 1.5, 3.5)!;
        grunt.State = EnemyState.Chase;

        Run(e, 12, default);
        Assert.True(e.Health < Engine.MaxHealth, $"grunt never reached the player (at {grunt.X:0.0},{grunt.Y:0.0})");
        Assert.Contains(Sfx.PlayerHurt, e.Sounds);
        Assert.True(e.IsFree(grunt.X, grunt.Y, Engine.EnemyDefs[0].Radius));
    }

    [Fact]
    public void Shooter_HitsFromRange_OnlyWithLineOfSight()
    {
        var e = NewEngine(Hall);
        var shooter = e.SpawnEnemy(EnemyKind.Shooter, 6.5, 1.5)!;
        Run(e, 15, default);
        Assert.True(e.Health < Engine.MaxHealth);
        Assert.Contains(Sfx.EnemyShot, e.Sounds);
        Assert.True(shooter.X > 4, "shooters keep their distance instead of charging");

        var walled = NewEngine("#########", "#S..#...#", "#########");
        walled.SpawnEnemy(EnemyKind.Shooter, 6.5, 1.5)!.State = EnemyState.Chase;
        Run(walled, 10, default);
        Assert.Equal(Engine.MaxHealth, walled.Health);
    }

    [Fact]
    public void Enemy_OpensPlainDoors_ButNotLockedOnes()
    {
        var e = NewEngine("##########", "#S...D...#", "##########");
        var grunt = e.SpawnEnemy(EnemyKind.Grunt, 7.5, 1.5)!;
        grunt.State = EnemyState.Chase;
        e.Px = 1.5; // far enough that the player is not the one opening it
        Run(e, 8, default);
        Assert.True(grunt.X < 5, "came through the door");

        var locked = NewEngine("##########", "#S...R...#", "##########");
        var stuck = locked.SpawnEnemy(EnemyKind.Grunt, 7.5, 1.5)!;
        stuck.State = EnemyState.Chase;
        Run(locked, 8, default);
        Assert.True(stuck.X > 6, "locked door holds it back");
        Assert.Equal(Engine.MaxHealth, locked.Health);
    }

    [Fact]
    public void Enemies_NeverSpawnOrEndUpInsideWalls()
    {
        var blocked = NewEngine();
        Assert.Null(blocked.SpawnEnemy(EnemyKind.Tank, 0.5, 0.5)); // solid wall
        Assert.Null(blocked.SpawnEnemy(EnemyKind.Tank, 1.1, 1.1)); // overlapping a wall corner

        for (var seed = 1; seed <= 6; seed++)
        {
            var e = new Engine();
            e.NewGame(seed);
            e.NextLevel();
            e.NextLevel(); // depth 3: all three enemy types
            Assert.Equal(e.Level.Enemies.Count, e.Enemies.Count);
            e.Health = 1_000_000; // survive the whole simulation
            foreach (var en in e.Enemies) en.State = EnemyState.Chase;

            for (var i = 0; i < 60 * 40; i++)
            {
                e.Tick(1 / 60.0, new PlayerInput { Forward = Math.Sin(i / 50.0), Turn = 0.02, Fire = i % 3 == 0 });
                if (i % 30 != 0) continue;
                foreach (var en in e.Enemies.Where(en => en.Alive)) // a door may close over a corpse; living bodies never
                    Assert.True(e.IsFree(en.X, en.Y, Engine.EnemyDefs[(int)en.Kind].Radius), $"{en.Kind} ({en.State}) in wall, seed {seed} t={i}");
                Assert.True(e.IsFree(e.Px, e.Py), $"player in wall, seed {seed} t={i}");
            }
        }
    }

    [Fact]
    public void Enemies_GetTougherWithDepth()
    {
        var e = NewEngine(Hall);
        e.Depth = 1;
        var shallow = e.SpawnEnemy(EnemyKind.Tank, 5.5, 1.5)!.Hp;
        e.Depth = 6;
        var deep = e.SpawnEnemy(EnemyKind.Tank, 8.5, 1.5)!.Hp;
        Assert.True(deep > shallow);
        Assert.True(LevelGenerator.Generate(5, 6).Enemies.Count > LevelGenerator.Generate(5, 1).Enemies.Count);
    }

    // ── Phase 6: flow + polish ──

    [Fact]
    public void ClearingALevel_PaysABonusThatGrowsWithDepth()
    {
        var e = new Engine();
        e.NewGame(5);
        e.Px = e.Level.ExitX + 0.5;
        e.Py = e.Level.ExitY + 0.5;
        e.Tick(0.016, default);
        Assert.True(e.LevelComplete);
        Assert.Equal(Engine.LevelBonus, e.Score);

        e.NextLevel();
        Assert.Equal((2, false, Engine.LevelBonus), (e.Depth, e.LevelComplete, e.Score)); // score carries over
        e.Px = e.Level.ExitX + 0.5;
        e.Py = e.Level.ExitY + 0.5;
        e.Tick(0.016, default);
        Assert.Equal(Engine.LevelBonus * 3, e.Score);
    }

    [Fact]
    public void NewGame_ResetsTheRun()
    {
        var e = new Engine();
        e.NewGame(5);
        e.Hurt(60);
        e.Owned[2] = true;
        e.Score = 999;
        e.NextLevel();
        e.NewGame(5);
        Assert.Equal((1, Engine.MaxHealth, 0, false), (e.Depth, e.Health, e.Score, e.Owned[2]));
    }

    [Fact]
    public void Hits_ThrowBlood_ThatFallsAndDisappears()
    {
        var e = NewEngine(Hall);
        var tank = e.SpawnEnemy(EnemyKind.Tank, 4.5, 1.5)!;
        e.Damage(tank, 5);
        Assert.NotEmpty(e.Blood);
        Run(e, 3, default);
        Assert.Empty(e.Blood);

        for (var i = 0; i < 500; i++) e.Damage(tank, 0); // never grows without bound
        Assert.True(e.Blood.Count <= Engine.MaxBlood);
    }

    [Fact]
    public void Renderer_FlashesRedWhenHurt_AndShakes()
    {
        var e = NewEngine();
        var r = new Renderer(320, 200);
        r.Render(e);
        var calm = r.Fb[10 * 320 + 10];
        var calmRow = Enumerable.Range(0, 200).First(y => r.Fb[y * 320 + 5] != r.Fb[5]); // first row that is not ceiling

        e.Hurt(10);
        e.Time = 0.5; // any phase where the shake offset is non-zero
        r.Render(e);
        var hurt = r.Fb[10 * 320 + 10];
        Assert.True((hurt & 0xFF) > (calm & 0xFF), "red channel rises");
        Assert.True(((hurt >> 8) & 0xFF) <= ((calm >> 8) & 0xFF), "green does not");

        var shakenRow = Enumerable.Range(0, 200).First(y => r.Fb[y * 320 + 5] != r.Fb[5]);
        Assert.NotEqual(calmRow, shakenRow); // the wall's top edge moved
    }

    // ── Phase 8: whole-level playthroughs ──

    /// <summary>
    /// A bot plays real generated levels start to finish: fetch the keys in order, walk to the exit, shoot whatever
    /// is in the way. Proves the pieces work together — doors open, keys unlock, enemies cannot wall the player in.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Bot_CanFinishGeneratedLevels(int seed)
    {
        var e = new Engine();
        e.NewGame(seed);
        for (var depth = 1; depth <= 4; depth++)
        {
            var lvl = e.Level;
            for (var i = 0; i < 60 * 240 && !e.LevelComplete; i++)
            {
                e.Health = Engine.MaxHealth; // the bot is testing the level, not its own aim

                int Find(PickupKind kind) => e.Pickups.Where(p => p.Kind == kind).Select(p => (int)p.Y * lvl.W + (int)p.X).DefaultIfEmpty(-1).First();
                var blue = Find(PickupKind.KeyBlue);
                var red = Find(PickupKind.KeyRed);
                var reach = LevelGenerator.Flood(lvl, (int)e.Px, (int)e.Py, e.HasRedKey, e.HasBlueKey);
                var exit = lvl.ExitY * lvl.W + lvl.ExitX;
                var goal = reach[exit] >= 0 ? exit : red >= 0 && reach[red] >= 0 ? red : blue;
                Assert.True(goal >= 0 && reach[goal] >= 0, $"bot has nowhere to go, seed {seed} depth {depth}");

                // step downhill on the distance field measured from the goal
                var toGoal = LevelGenerator.Flood(lvl, goal % lvl.W, goal / lvl.W, e.HasRedKey, e.HasBlueKey);
                var here = (int)e.Py * lvl.W + (int)e.Px;
                var next = new[] { here + 1, here - 1, here + lvl.W, here - lvl.W }
                    .Where(n => toGoal[n] >= 0).OrderBy(n => toGoal[n]).First();
                if (toGoal[here] == 0) next = here;
                e.Angle = Math.Atan2(next / lvl.W + 0.5 - e.Py, next % lvl.W + 0.5 - e.Px);
                e.Tick(1 / 60.0, new PlayerInput { Forward = 1, Fire = true });
                e.Sounds.Clear();
                Assert.True(e.IsFree(e.Px, e.Py), $"player in wall, seed {seed} depth {depth}");
            }
            Assert.True(e.LevelComplete, $"bot did not finish seed {seed} depth {depth} (stuck at {e.Px:0.0},{e.Py:0.0})");
            e.NextLevel();
        }
        Assert.Equal(5, e.Depth);
        Assert.True(e.Score >= Engine.LevelBonus * 10);
    }

    // ── Vertical look, headshots, trickshots ──

    private static Enemy Dummy(Engine e, EnemyKind kind, double x, double y, int hp = 10_000)
    {
        var en = e.SpawnEnemy(kind, x, y)!;
        en.State = EnemyState.Pain; // stands still
        en.Timer = 1e9;
        en.Hp = hp;
        return en;
    }

    [Fact]
    public void Look_TiltsTheView_WithinLimits()
    {
        var e = NewEngine();
        e.Tick(0.016, new PlayerInput { Look = 0.1 });
        Assert.Equal(0.1, e.Pitch, 6);
        e.Tick(0.016, new PlayerInput { Look = 5 });
        Assert.Equal(Engine.MaxPitch, e.Pitch);
        e.Tick(0.016, new PlayerInput { Look = -50 });
        Assert.Equal(-Engine.MaxPitch, e.Pitch);

        var r = new Renderer(320, 200);
        int Horizon() => Enumerable.Range(0, 200).First(y => r.Fb[y * 320 + 5] != r.Fb[5]);
        e.Pitch = 0;
        r.Render(e);
        var level = Horizon();
        e.Pitch = 0.2;
        r.Render(e);
        Assert.True(Horizon() > level, "looking up slides the scene down the screen");
    }

    [Fact]
    public void AimingOverAnEnemy_Misses_AndAtItsHead_DoesDoubleDamage()
    {
        var e = NewEngine(Hall);
        var grunt = Dummy(e, EnemyKind.Grunt, 5.5, 1.5); // 4 cells away, 0.72 tall
        var pistol = Engine.Weapons[0].Damage;

        e.Pitch = 0.2; // bullet is at height 1.3 by the time it gets there
        e.Tick(0.016, Fire);
        Assert.Equal(10_000, grunt.Hp);

        e.Pitch = -0.05; // height 0.3: body
        Run(e, 0.4, default);
        e.Tick(0.016, Fire);
        Assert.Equal(10_000 - pistol, grunt.Hp);

        e.Pitch = 0.04; // height 0.66: head (top quarter starts at 0.54)
        Run(e, 0.4, default);
        e.Sounds.Clear();
        e.Tick(0.016, Fire);
        Assert.Equal(10_000 - pistol - pistol * Engine.HeadshotMultiplier, grunt.Hp);
        Assert.Contains(Sfx.Headshot, e.Sounds);
    }

    [Fact]
    public void HeadshotKill_PaysABonus_AndAnnouncesIt()
    {
        var e = NewEngine(Hall);
        Dummy(e, EnemyKind.Grunt, 5.5, 1.5, hp: 1);
        e.Pitch = 0.04;
        e.Tick(0.016, Fire);

        Assert.Equal(Engine.EnemyDefs[0].Score + Engine.HeadshotBonus, e.Score);
        Assert.Equal(1, e.Tricks);
        Assert.Contains("HEADSHOT", e.Message);
        Assert.Contains(Sfx.Trick, e.Sounds);
    }

    [Fact]
    public void BulletsBounceOnceOffWalls_BankShotKillsRoundACorner()
    {
        // the grunt is behind the pillar; the only way to it is off the south wall
        var e = NewEngine(
            "#########",
            "#S..#...#",
            "#.......#",
            "#########");
        var target = Dummy(e, EnemyKind.Grunt, 6.5, 1.5, hp: 1);
        Assert.False(e.Level.LineOfSight(e.Px, e.Py, target.X, target.Y));

        // aim at the mirror image of the target in the wall at y = 3
        e.Angle = Math.Atan2((3 + (3 - target.Y)) - e.Py, target.X - e.Px);
        e.Pitch = -0.03; // body height
        e.Tick(0.016, Fire);

        Assert.False(target.Alive);
        Assert.Contains("BANK SHOT", e.Message);
        Assert.Equal(Engine.EnemyDefs[0].Score + Engine.BankShotBonus, e.Score);
    }

    [Fact]
    public void BankedBullets_AreWeaker_AndStopAfterOneBounce()
    {
        var e = NewEngine(
            "#########",
            "#S..#...#",
            "#.......#",
            "#########");
        var target = Dummy(e, EnemyKind.Grunt, 6.5, 1.5);
        e.Angle = Math.Atan2((3 + (3 - target.Y)) - e.Py, target.X - e.Px);
        e.Pitch = -0.03; // body height
        e.Tick(0.016, Fire);
        Assert.Equal(10_000 - (int)(Engine.Weapons[0].Damage * Engine.BankDamage), target.Hp);

        // straight at the far wall and back would need two bounces to reach something behind the player
        var hall = NewEngine(Hall);
        var behind = Dummy(hall, EnemyKind.Grunt, 5.5, 1.5);
        hall.Px = 8.5;
        hall.Angle = Math.PI / 2; // into the side wall: bounces straight back through the player's own position, never sideways
        hall.Tick(0.016, Fire);
        Assert.Equal(10_000, behind.Hp);
    }

    [Fact]
    public void LongShot_SpinShot_AndDoubleKill_AreRecognised()
    {
        var far = NewEngine("##############", "#S...........#", "##############");
        Dummy(far, EnemyKind.Grunt, 11.5, 1.5, hp: 1); // 10 cells
        far.Pitch = -0.01;
        far.Tick(0.016, Fire);
        Assert.Contains("LONG SHOT", far.Message);

        var spin = NewEngine(Hall);
        Dummy(spin, EnemyKind.Grunt, 4.5, 1.5, hp: 1);
        for (var i = 0; i < 10; i++) spin.Tick(0.016, new PlayerInput { Turn = Math.PI * 2 / 10 }); // a 360 in a sixth of a second
        spin.Tick(0.016, Fire);
        Assert.Contains("SPIN SHOT", spin.Message);

        var calm = NewEngine(Hall);
        Dummy(calm, EnemyKind.Grunt, 4.5, 1.5, hp: 1);
        Run(calm, 3, new PlayerInput { Turn = 2.6 / 60 }); // holding an arrow key is not a trick
        calm.Angle = 0;
        calm.Tick(0.016, Fire);
        Assert.DoesNotContain("SPIN", calm.Message);

        var two = NewEngine("#########", "#S......#", "#.......#", "#########");
        Dummy(two, EnemyKind.Grunt, 5.5, 1.5, hp: 1);
        Dummy(two, EnemyKind.Grunt, 5.5, 2.5, hp: 1);
        two.Explode(5.5, 2.0, 0.4);
        Assert.Contains("DOUBLE KILL", two.Message);
        Assert.Equal(2, two.Kills);
    }

    // ── Rocket launcher ──

    [Fact]
    public void RocketLauncher_IsPickedUp_Fires_AndTheRocketExplodesOnTheWall()
    {
        var e = NewEngine("##############", "#S...........#", "##############");
        e.Pickups.Add(new Pickup { Kind = PickupKind.RocketLauncher, X = e.Px, Y = e.Py });
        e.Tick(0.016, default);
        Assert.True(e.Owned[3]);
        Assert.Equal(3, e.Weapon);
        Assert.Equal(4, e.Rockets);

        var near = Dummy(e, EnemyKind.Tank, 11.0, 1.5);
        Run(e, 0.4, default); // weapon comes up
        e.Tick(0.016, Fire);
        Assert.Single(e.Missiles);
        Assert.Contains(Sfx.Rocket, e.Sounds);
        Assert.Equal(10_000, near.Hp); // still in flight

        Run(e, 2, default);
        Assert.Empty(e.Missiles);
        Assert.Contains(Sfx.Explosion, e.Sounds);
        Assert.True(near.Hp < 10_000, "direct hit");
        Assert.Equal(Engine.MaxHealth, e.Health); // 9+ cells away: no self damage
        Run(e, 1, default);
        Assert.Empty(e.Explosions);
    }

    [Fact]
    public void Splash_FallsOffWithDistance_NeedsLineOfSight_AndHurtsThePlayer()
    {
        var e = NewEngine("#########", "#S..#...#", "#.......#", "#########");
        var close = Dummy(e, EnemyKind.Grunt, 6.5, 2.5);
        var edge = Dummy(e, EnemyKind.Grunt, 7.5, 1.5);
        var walled = Dummy(e, EnemyKind.Grunt, 3.5, 1.5); // other side of the pillar from the blast
        e.Explode(5.5, 1.5, 0.5);

        Assert.True(close.Hp < edge.Hp, "nearer = more damage");
        Assert.True(edge.Hp < 10_000);
        Assert.Equal(10_000, walled.Hp);

        var self = NewEngine();
        self.Explode(self.Px + 0.5, self.Py, 0.5);
        Assert.InRange(self.Health, 1, Engine.MaxHealth - 20);
    }

    [Fact]
    public void RocketsFlyAlongThePitch_AndHitTheFloorWhenAimedDown()
    {
        var e = NewEngine("##############", "#S...........#", "##############");
        e.Owned[3] = true;
        e.Tick(0.016, new PlayerInput { Weapon = 4 });
        Run(e, 0.4, default);
        e.Pitch = -0.3;
        e.Tick(0.016, Fire);
        Run(e, 1, default);
        Assert.Empty(e.Missiles); // hit the floor ~1.5 cells out instead of flying to the far wall
        Assert.True(e.Health < Engine.MaxHealth, "blast at your own feet hurts");
    }

    // ── Charger ──

    [Fact]
    public void Charger_Telegraphs_ThenRamsThePlayer()
    {
        var e = NewEngine("##############", "#S...........#", "##############");
        var charger = e.SpawnEnemy(EnemyKind.Charger, 6.5, 1.5)!;
        charger.State = EnemyState.Chase;
        e.Tick(0.016, default);
        Assert.Equal(EnemyState.Windup, charger.State);
        Assert.Contains(Sfx.EnemyAlert, e.Sounds);
        Assert.Equal(Engine.MaxHealth, e.Health);

        Run(e, 1.5, default);
        Assert.Equal(Engine.MaxHealth - Engine.EnemyDefs[3].Damage, e.Health);
        Assert.NotEqual(EnemyState.Charge, charger.State);
    }

    [Fact]
    public void Charger_CannotSteer_SidesteppingMakesItRamTheWallAndStun()
    {
        var e = NewEngine(
            "##########",
            "#........#",
            "#S.......#",
            "#........#",
            "##########");
        var charger = e.SpawnEnemy(EnemyKind.Charger, 7.5, 2.5)!;
        charger.State = EnemyState.Chase;
        Run(e, 0.55, default);                 // it locks on at the end of the wind-up…
        Assert.Equal(EnemyState.Windup, charger.State);
        Run(e, 0.1, default);
        Assert.Equal(EnemyState.Charge, charger.State);
        e.Py = 1.3;                            // …and the player steps out of the lane
        Run(e, 1.0, default);

        Assert.Equal(Engine.MaxHealth, e.Health);
        Assert.Equal(EnemyState.Pain, charger.State); // dazed against the west wall
        Assert.True(charger.X < 2);
        Assert.True(e.IsFree(charger.X, charger.Y, Engine.EnemyDefs[3].Radius));
    }

    [Fact]
    public void Generator_AddsChargersAndRockets_OnlyFromTheirDepth()
    {
        Assert.DoesNotContain(Enumerable.Range(1, 40).SelectMany(s => LevelGenerator.Generate(s, 1).Enemies), en => en.Kind == EnemyKind.Charger);
        Assert.Contains(Enumerable.Range(1, 40).SelectMany(s => LevelGenerator.Generate(s, 3).Enemies), en => en.Kind == EnemyKind.Charger);
        Assert.DoesNotContain(Enumerable.Range(1, 40).SelectMany(s => LevelGenerator.Generate(s, 2).Pickups), p => p.Kind is PickupKind.RocketLauncher or PickupKind.Rockets);
        Assert.All(Enumerable.Range(1, 40), s => Assert.Contains(LevelGenerator.Generate(s, 3).Pickups, p => p.Kind == PickupKind.RocketLauncher));
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
        e.SpawnEnemy(EnemyKind.Tank, e.Px + 0.4, e.Py + 0.6); // a sprite right in the camera's face, partly off-screen
        e.Pickups.Add(new Pickup { Kind = PickupKind.KeyRed, X = e.Px + 1, Y = e.Py + 1 });
        r.Render(e); // must not throw at any size
    }
}
