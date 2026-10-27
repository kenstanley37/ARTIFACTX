// Isolated I/O doubles; the production StationMultiToolsViewModel is compiled unchanged.
using ArtifactX.WinUI3.ViewModels;
namespace ArtifactX.WinUI3.Services
{
    public sealed record PlanetSnapshot(object? EquipmentSaveInputs);
    public static class SaveSessionManager
    {
        public static object? SessionIdentity { get; set; } = new();
        public static object? ActiveSlot { get; set; } = new();
    }
    public static class LiveSaveWatcher
    {
        public static Task<PlanetSnapshot> GetLatestAsync() => Task.FromResult(new PlanetSnapshot(new()));
    }
    public static class CatalogService
    {
        public static Task WarmCacheAsync(IEnumerable<string> ids) => Task.CompletedTask;
    }
    public static class LocatedMultiToolService
    {
        public static float Damage { get; set; } = 440;
        public static TaskCompletionSource<IReadOnlyList<TestTool>>? Pending { get; set; }
        public static IReadOnlyList<TestTool> Tools() => [new(1, Damage), new(2, 3402.9f)];
        public static Task<IReadOnlyList<TestTool>> GenerateSavedReloadStationAsync(ulong system, object inputs)
            => Pending?.Task ?? Task.FromResult(Tools());
    }
    public sealed class TestTool(ulong site, float damage)
    {
        public TestContents Contents { get; } = new(new("same name", "model", 42), new(site, new(1, 2, 3)), 2);
        public TestInventory Inventory { get; } = new();
        public bool HasSupportedDamagePotential => true;
        public bool UsesLivePreviewState => true;
        public float DamagePotential => damage;
        public float ScannerRange => 221.5f;
    }
    public sealed record TestContents(TestModel Tool, TestLocation Location, int RatingIndex);
    public sealed record TestModel(string DefaultName, string ModelPath, ulong Seed);
    public sealed record TestLocation(ulong SiteSeed, System.Numerics.Vector3 WorldPosition);
    public sealed class TestInventory
    {
        public TestLayout Layout { get; } = new();
        public IReadOnlyList<TestTechnology> Technologies { get; } = [];
        public IReadOnlyList<int> SuperchargedSlots { get; } = [];
    }
    public sealed class TestLayout { public int[] UnlockedSlots { get; } = [1, 2]; }
    public sealed record TestTechnology(string Id);
}
namespace ArtifactX.WinUI3.ViewModels
{
    public sealed record InventorySlotViewModel(int X, int Y, string State, string ItemId, bool IsSupercharged, string DisplayName);
    public static class StationToolInventory
    {
        public static InventoryGridSnapshot Create(ArtifactX.WinUI3.Services.TestInventory inventory) => new(1, 1, []);
    }
}
