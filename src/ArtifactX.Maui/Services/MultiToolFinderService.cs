using System.Collections.Concurrent;
using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The phone's Multi-Tool Finder: what the space station cabinets of every system around a start sell, for each place
/// the game can be loaded at - the desktop's search (Core NearbySystems + CabinetMultiToolSearch) on the rules file's
/// tables. A phone has no save, so stations are taken as the ordinary layout (expedition "walk back in time" stations
/// aren't covered). Model, kind and class only, like the desktop finder.
/// </summary>
public static class MultiToolFinderService
{
    private static Task<MultiToolKindClassifier?>? _kinds;

    /// <summary>The cabinet offers within <paramref name="regionRadius"/> regions of <paramref name="start"/> (a system
    /// address), off the UI thread; null when the rules file lacks the tables.</summary>
    public static async Task<IReadOnlyList<CabinetMultiToolFind>?> FindAsync(ulong start, int regionRadius,
        IProgress<int>? progress, CancellationToken cancel)
    {
        if (await GameRulesService.GetAsync() is not { GalaxySettings: { } settings, Stations: { } stations, Inventory: { } inventory,
                MultiToolModel: { } model } rules)
            return null;
        _kinds ??= Task.Run(() => (MultiToolKindClassifier?)new MultiToolKindClassifier(model));
        if (await _kinds is not { } kinds) return null;
        return await Task.Run(() =>
        {
            var systems = NearbySystems.Around(start, regionRadius, settings).ToList();
            var found = new ConcurrentBag<CabinetMultiToolFind>();
            int done = 0;
            Parallel.ForEach(systems, new ParallelOptions { CancellationToken = cancel }, system =>
            {
                foreach (var find in CabinetMultiToolSearch.InSystem(system.SystemAddress, false, stations, inventory, kinds, rules.Words))
                    found.Add(find);
                int n = Interlocked.Increment(ref done);
                if (n % 200 == 0) progress?.Report(n);
            });
            return (IReadOnlyList<CabinetMultiToolFind>)found.ToList();
        }, cancel);
    }
}
