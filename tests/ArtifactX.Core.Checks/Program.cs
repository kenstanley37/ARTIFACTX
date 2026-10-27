using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ArtifactX.Core.Collections;
using ArtifactX.Core.Universe;
using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;

// No desktop dependencies: run with dotnet run --project tests/ArtifactX.Core.Checks.
try
{
    var rows = new ObservableCollection<Row> { new(1, "A", 10), new(2, "B", 20) };
    var first = rows[0];
    var second = rows[1];
    int changes = 0, resets = 0;
    rows.CollectionChanged += (_, e) =>
    {
        changes++;
        if (e.Action == NotifyCollectionChangedAction.Reset) resets++;
    };
    void Apply(params Row[] incoming) => ObservableCollectionSync.Apply(rows, incoming,
        row => row.Id, (a, b) => a.Model == b.Model, (a, b) => a.Value = b.Value);

    for (int i = 0; i < 100; i++) Apply(new(1, "A", 10), new(2, "B", 20));
    Require(changes == 0 && ReferenceEquals(first, rows[0]) && ReferenceEquals(second, rows[1]),
        "Unchanged snapshots recreated rows");

    Apply(new(1, "A", 11), new(2, "B", 20));
    Require(changes == 0 && rows[0].Value == 11 && ReferenceEquals(first, rows[0]),
        "A stats update replaced the model row");

    Apply(new(2, "B", 20), new(1, "A", 11));
    Require(changes == 1 && ReferenceEquals(first, rows[1]) && ReferenceEquals(second, rows[0]),
        "Reordering lost row identity");

    Apply(new(2, "C", 30), new(1, "A", 11));
    Require(!ReferenceEquals(second, rows[0]) && ReferenceEquals(first, rows[1]),
        "Model replacement affected an unrelated row");

    int before = changes;
    bool rejected = false;
    try { Apply(new(1, "A", 1), new(1, "B", 2)); }
    catch (ArgumentException) { rejected = true; }
    Require(rejected && changes == before, "Duplicate keys were accepted or partially published");

    var random = new Random(1027);
    for (int i = 0; i < 1000; i++)
    {
        var incoming = Enumerable.Range(0, 10).OrderBy(_ => random.Next()).Take(random.Next(11))
            .Select(id => new Row(id, "same", i)).ToArray();
        var retained = rows.ToDictionary(row => row.Id);
        Apply(incoming);
        Require(rows.Select(row => (row.Id, row.Model, row.Value))
            .SequenceEqual(incoming.Select(row => (row.Id, row.Model, row.Value))),
            "Mixed-update output differs from incoming data");
        foreach (var row in rows)
            if (retained.TryGetValue(row.Id, out var old) && old.Model == row.Model)
                Require(ReferenceEquals(old, row), "A surviving row was replaced");
    }
    Require(resets == 0, "A collection Reset was emitted");
    var ids = new MultiToolDamageStatIds(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    var tables = new MultiToolTechnologyTables([], [],
        ScannerRules: new(57, 200, 1.25f, .1f, .1f, 3),
        DamageRules: new(ids, "WEAPON_DAMAGE", 0, 0, []),
        ProductFoodBonusStats: new() { ["REWARD_FOOD"] = 61 });
    Require(MultiToolFoodGuard.HasNoDisplayedStatEffect("REWARD_FOOD", tables), "Unrelated food blocked display");
    foreach (int stat in Enumerable.Range(1, 10).Append(57))
    {
        tables.ProductFoodBonusStats!["MATCHING"] = stat;
        Require(!MultiToolFoodGuard.HasNoDisplayedStatEffect("MATCHING", tables), "A relevant food target was ignored");
    }
    Require(!MultiToolFoodGuard.HasNoDisplayedStatEffect("UNKNOWN", tables)
        && !MultiToolFoodGuard.HasNoDisplayedStatEffect(null, tables)
        && !MultiToolFoodGuard.HasNoDisplayedStatEffect("REWARD_FOOD", tables with { ProductFoodBonusStats = null })
        && !MultiToolFoodGuard.HasNoDisplayedStatEffect("REWARD_FOOD", tables with { ScannerRules = null }),
        "Unknown food/catalog inputs were accepted");
    var player = new JObject { ["nsH"] = "^REWARD_FOOD", ["DtI"] = 1, ["4kj"] = new JArray() };
    var runtime = NmsMultiToolGenerationPaths.ReadStandardGameState(player, 0);
    Require(runtime is { HasActiveSpecialTechnology: true, ActiveFoodUnitItemId: "REWARD_FOOD" }, "Food identity lost in saved inputs");
    player["nsH"] = "^";
    runtime = NmsMultiToolGenerationPaths.ReadStandardGameState(player, 0);
    Require(runtime is { HasActiveSpecialTechnology: false, ActiveFoodUnitItemId: null }, "Empty food changed default behavior");
    int[] curves = [0];
    var stats = new[] { new ProceduralModuleStat(3, 0f, 1f, 0, false), new ProceduralModuleStat(4, 0f, 1f, 0, false) };
    foreach (string id in new[] { "UP_LASERX", "UP_SENGUN" })
    {
        var module = new ProceduralModuleDefinition(id, id == "UP_SENGUN" ? 5 : 4, 1, 2, 0, stats);
        Require(MultiToolProceduralModuleGeneration.RollOrdinaryStats(1, module, curves) is null, "High-grade roll accepted without its chance");
        Require(MultiToolProceduralModuleGeneration.RollOrdinaryStats(1, module, curves, 100)?.Count == 2, "Certain high-grade roll did not maximize count");
    }
    Require(MultiToolProceduralModuleGeneration.RollOrdinaryStats(1, new("UP_LASER0", 7, 1, 2, 0, stats), curves) is not null,
        "SeaTrash roll required an illegal chance");
    player["yhJ"] = new JObject { ["Iis"] = 0, ["oZw"] = new JObject { ["dZj"] = 1, ["IyE"] = 2, ["uXE"] = 3, ["vby"] = 4, ["jsv"] = 0 } };
    var saveRoot = new JObject { [NmsSaveContext.ActiveContextKey] = "Main",
        [NmsSaveContext.MainRootKey] = new JObject { ["6f="] = player },
        [NmsSaveContext.ExpeditionRootKey] = new JObject { ["6f="] = player.DeepClone() } };
    Require(NmsMultiToolGenerationPaths.ReadActiveSavedState(saveRoot, 0)?.Runtime.HasVerifiedStandardIllegalModuleRoll == true,
        "Main context did not establish the standard high-grade roll");
    saveRoot[NmsSaveContext.ActiveContextKey] = NmsSaveContext.ExpeditionContextValue;
    Require(NmsMultiToolGenerationPaths.ReadActiveSavedState(saveRoot, 0)?.Runtime.HasVerifiedStandardIllegalModuleRoll == false,
        "Seasonal context assumed the standard high-grade roll");
    Console.WriteLine("PASS: quality 4/5 high-grade rolls need the chance; SeaTrash rolls do not; only Main establishes the standard roll.");
    Console.WriteLine("PASS: unrelated food accepted; all 11 displayed-stat targets and unknown inputs guarded; saved food identity preserved.");
    Console.WriteLine("PASS: unchanged snapshots, in-place stats, reorder, model replacement, duplicate rejection, and 1000 mixed updates; zero resets.");
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

sealed class Row(int id, string model, int value)
{
    public int Id { get; } = id;
    public string Model { get; } = model;
    public int Value { get; set; } = value;
}
