// Isolated I/O doubles; the production StationMultiToolsViewModel is compiled unchanged.
using ArtifactX.WinUI3.ViewModels;
using Newtonsoft.Json.Linq;

namespace Microsoft.UI.Xaml
{
    public readonly record struct Thickness(double UniformLength);
}

namespace Microsoft.UI.Xaml.Media
{
    public abstract class ImageSource;
    public abstract class Brush;
    public sealed class SolidColorBrush : Brush;
}

namespace ArtifactX.WinUI3.Services
{
    public static class ThemeResources
    {
        public static Microsoft.UI.Xaml.Media.Brush Brush(string key) => new Microsoft.UI.Xaml.Media.SolidColorBrush();
        public static Microsoft.UI.Xaml.Media.Brush ClassBrush(string? classLetter) => new Microsoft.UI.Xaml.Media.SolidColorBrush();
    }

    public sealed record PlanetSnapshot(JObject? EquipmentSaveInputs);

    public static class SaveSessionManager
    {
        public static object? SessionIdentity { get; set; } = new();
        public static object? ActiveSlot { get; set; } = new();
    }

    public static class LiveSaveWatcher
    {
        public static Task<PlanetSnapshot> GetLatestAsync() => Task.FromResult(new PlanetSnapshot(new JObject()));
    }

    public static class CabinetPoolService
    {
        public static int? SavedLoadBody(ulong system, JObject saveInputs) => 0;
    }

    public static class UniverseInfo
    {
        public static (ulong Code, int Galaxy) Generator(ulong address, int planet = 0) =>
            (((ulong)(planet & 0xF) << 44) | (((address >> 40) & 0xFFF) << 32) | (address & 0xFFFFFFFF), (int)((address >> 32) & 0xFF));
        public static string PlanetName(ulong address, int index) => $"Planet {index}";
    }

    public sealed record CatalogItemStub(string DisplayName, Microsoft.UI.Xaml.Media.ImageSource? Icon);

    public static class CatalogService
    {
        public static Task WarmCacheAsync(IEnumerable<string> ids) => Task.CompletedTask;
        public static CatalogItemStub? TryGet(string? id) => null;
        public static string? ModuleClassLetter(string? id) => null;
    }

    public static class LocatedMultiToolService
    {
        public static float Damage { get; set; } = 440;
        public static TaskCompletionSource<IReadOnlyList<TestTool>>? Pending { get; set; }
        public static IReadOnlyList<TestTool> Tools() => [new(1, Damage), new(2, 3402.9f)];
        public static Task<IReadOnlyList<TestTool>> GenerateStationForBodyAsync(ulong system, int body, JObject inputs)
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
    public sealed record InventorySlotViewModel(int X, int Y, string State, string? ItemId, bool IsSupercharged, string? DisplayName)
    {
        public bool IsOccupied => ItemId is not null;
        public string ShortLabel => ItemId ?? "";
    }

    public static class StationToolInventory
    {
        public static InventoryGridSnapshot Create(ArtifactX.WinUI3.Services.TestInventory inventory) => new(1, 1, []);
    }
}
