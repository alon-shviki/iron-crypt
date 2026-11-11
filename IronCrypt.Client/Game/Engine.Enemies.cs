namespace IronCrypt.Client.Game;

// Phase 3 stubs — enemies arrive in the next phase; combat already calls these seams.
public partial class Engine
{
    private void OnLevelLoaded() { }

    private void TickEnemies(double dt) { }

    private bool EnemyNear(double x, double y, double range) => false;

    private void AlertEnemies() { }

    private void HitScan(double angle, double wallDist, int damage) { }
}
