namespace IronCrypt.Client.Game;

// Phase 2 stubs — combat and enemies arrive in the next phases; the core loop already calls these seams.
public partial class Engine
{
    public bool Dead => false;

    private void ResetRun() { }

    private void OnLevelLoaded() { }

    private void OnLevelComplete() { }

    private void TickCombat(double dt, in PlayerInput input) { }

    private void TickEnemies(double dt) { }

    private bool EnemyNear(double x, double y, double range) => false;

    private bool ApplyItem(PickupKind kind) => false; // items stay on the floor until combat exists
}
