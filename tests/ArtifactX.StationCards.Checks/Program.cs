using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;

try
{
    var model = new StationMultiToolsViewModel();
    var snapshot = new PlanetSnapshot(new());
    await model.UpdateAsync(123, snapshot);
    Require(model.StationReloadTools.Count == 2, "Same-name cabinets were not kept distinct");
    var first = model.StationReloadTools[0];
    var second = model.StationReloadTools[1];
    int changes = 0, hidden = 0;
    model.StationReloadTools.CollectionChanged += (_, _) => changes++;
    model.PropertyChanged += (_, e) =>
    {
        if (e.PropertyName == nameof(model.HasStationReloadTools) && !model.HasStationReloadTools) hidden++;
    };
    for (int i = 0; i < 50; i++)
    {
        // SaveFolderWatchService adopts a fresh metadata object for the same editing session.
        SaveSessionManager.ActiveSlot = new object();
        await model.UpdateAsync(123, snapshot);
    }
    Require(changes == 0 && hidden == 0 && ReferenceEquals(first, model.StationReloadTools[0])
        && ReferenceEquals(second, model.StationReloadTools[1]), "Autosave metadata replacement rebuilt/collapsed cards");

    LocatedMultiToolService.Damage = 441;
    await model.UpdateAsync(123, snapshot);
    Require(changes == 0 && ReferenceEquals(first, model.StationReloadTools[0]) && first.Stats.Contains("441.0"),
        "Damage-only update replaced the card or failed to update text");

    var pending = new TaskCompletionSource<IReadOnlyList<TestTool>>();
    LocatedMultiToolService.Pending = pending;
    var obsolete = model.UpdateAsync(123, snapshot);
    SaveSessionManager.SessionIdentity = new object();
    pending.SetResult([]);
    await obsolete;
    Require(model.StationReloadTools.Count == 2 && hidden == 0, "A stale result modified the new session's display");
    LocatedMultiToolService.Pending = null;
    await model.UpdateAsync(123, snapshot);
    Require(hidden == 1 && !ReferenceEquals(first, model.StationReloadTools[0]), "A real session switch reused old cards");
    var newSessionRow = model.StationReloadTools[0];
    await model.UpdateAsync(456, snapshot);
    Require(hidden == 2 && !ReferenceEquals(newSessionRow, model.StationReloadTools[0]), "A real system switch reused old cards");
    Console.WriteLine("PASS: 50 metadata replacements preserve cards/visibility; stats update in place; stale results rejected; real session/system changes clear old cards.");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
