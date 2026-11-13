using libMBIN;
using ArtifactX.Core.Recipes;
using ArtifactX.GameData;
using ArtifactX.GameData.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ArtifactX.Core.Unlocks;
using ArtifactX.GameData.Services;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

if (args.Length == 1 && args[0] == "--planet-shop-survey-check")
{

    // Synthetic assets only. Does not require a game install or write a catalog/save.
    try
    {
        const string tablePath = "METADATA/SIMULATION/ENVIRONMENT/PLANETBUILDINGTABLE.MBIN";
        var files = new GcBuildingFilename[(int)GcBuildingClassification.BuildingClassEnum.Shop + 1];
        files[(int)GcBuildingClassification.BuildingClassEnum.Shop] = new() { LSystem = ["RULES.LSYSTEM.MBIN"] };
        var table = new GcBuildingDefinitionTable
        {
            BuildingFiles = [new() { BuildingFiles = files }]
        };
        var rules = new TkLSystemRulesData
        {
            Rules = [new() { Name = "shop", Model = "ENTRY.SCENE.MBIN", Rules = [new()
            {
                LocatorType = "WEAPONSCRATE_",
                Entries = [new() { Model = "ENTRY.SCENE.MBIN", Probability = 40, Restrictions = [] }]
            }] }]
        };
        var entry = Node("entry", 10, 0, 0);
        entry.Transform.ScaleX = entry.Transform.ScaleY = entry.Transform.ScaleZ = 2;
        entry.Transform.RotZ = 90;
        var reference = Node("reference", 1, 0, 0);
        reference.Attributes = [new() { Name = "SCENEGRAPH", Value = "crate.scene.mbin" }];
        entry.Children = [reference];
        var crate = Node("crate", 0, 1, 0);
        var terminal = Node("terminal", 0, 0, 3);
        terminal.Attributes = [new() { Name = "ATTACHMENT", Value = "weapon.entity.mbin" }];
        crate.Children = [terminal];
        var interaction = new GcInteractionComponentData
        {
            InteractionType = new() { InteractionType = GcInteractionType.InteractionTypeEnum.WeaponTerminal },
            ReseedAfterRewardSuccess = true,
            InteractionSpecificData = new GcWeaponTerminalInteractionData { RespawnPeriodInSeconds = 3600, UseSentinelWeapon = true }
        };
        var attachment = new TkAttachmentData { Components = [new() { Template = interaction }] };
        var assets = new Dictionary<string, NMSTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            [tablePath] = table, ["RULES.LSYSTEM.MBIN"] = rules, ["ENTRY.SCENE.MBIN"] = entry,
            ["CRATE.SCENE.MBIN"] = crate, ["WEAPON.ENTITY.MBIN"] = attachment
        };
        var reads = new Dictionary<string, int>();
        NMSTemplate? Decode(string path)
        {
            reads[path] = reads.GetValueOrDefault(path) + 1;
            return assets.GetValueOrDefault(path);
        }
        var survey = PlanetShopWeaponSurveyService.Read(Decode);
        Require(survey.References.Length == 1 && survey.UnresolvedPaths.Length == 0, "Terminal chain was not resolved");
        var row = survey.References[0];
        var position = row.Terminal.PositionInEntryModel;
        Require(Math.Abs(position[0] - 8) < .00001f && Math.Abs(position[1] - 2) < .00001f
            && Math.Abs(position[2] - 6) < .00001f, "Nested scale/rotation/translation composition differs");
        Require(row.Probability == 40 && row.Race == "Traders" && row.SystemType == "Normal"
            && row.Terminal.ReseedAfterRewardSuccess && row.Terminal.UseSentinelWeapon == true
            && row.Terminal.RespawnPeriodInSeconds == 3600, "Extracted metadata was lost or guessed");
        Require(reads.Values.All(count => count == 1), "An asset was decoded repeatedly");

        assets.Remove("CRATE.SCENE.MBIN");
        survey = PlanetShopWeaponSurveyService.Read(Decode);
        Require(survey.References.Length == 0 && survey.UnresolvedPaths.Contains("CRATE.SCENE.MBIN"),
            "Missing reference was silently accepted");
        assets["CRATE.SCENE.MBIN"] = crate;
        var cycle = Node("cycle", 0, 0, 0);
        cycle.Attributes = [new() { Name = "SCENEGRAPH", Value = "ENTRY.SCENE.MBIN" }];
        crate.Children.Add(cycle);
        survey = PlanetShopWeaponSurveyService.Read(Decode);
        Require(survey.References.Length == 1 && survey.UnresolvedPaths.Contains("ENTRY.SCENE.MBIN"),
            "Recursive reference was not reported");
        crate.Children.Remove(cycle);
        interaction.InteractionType.InteractionType = GcInteractionType.InteractionTypeEnum.None;
        Require(PlanetShopWeaponSurveyService.Read(Decode).References.Length == 0, "Non-weapon interaction accepted");
        Require(PlanetShopWeaponSurveyService.Read(_ => null).UnresolvedPaths.Contains(tablePath), "Missing building table accepted");
        Console.WriteLine("PASS: reference traversal, nested transforms, metadata, decode cache, missing assets, cycles, and non-weapon rejection.");
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error);
        Environment.ExitCode = 1;
    }

    return;
}


if (args.Length == 2 && args[0] == "--palettes-step")
{
    // Runs the palette catalog step (base + biome palettes) on the given catalog - use a copy, it is written.
    var game = GameInstall.Find() ?? throw new InvalidOperationException("No game install found.");
    CatalogSteps.AddPalettes(Path.GetFullPath(args[1]), game.PcbanksPath);
    return;
}

if (args.Length >= 2 && args[0] == "--mbin-dump")
{
    // Research probe: --mbin-dump <path substring> [n]: lists matching game files; with n, dumps the n-th as JSON
    // (fields only, 6 levels deep) - to read game tables without the catalog.
    var game = GameInstall.Find() ?? throw new InvalidOperationException("No game install found.");
    var reader = new ArtifactX.GameData.Services.PakReaderService();
    var hits = new List<(string Pak, ArtifactX.GameData.Models.PakHeader Header, ArtifactX.GameData.Models.PakEntry Entry)>();
    foreach (var pak in new ArtifactX.GameData.Services.PakDiscoveryService().Discover(game.PcbanksPath))
    {
        if (string.IsNullOrEmpty(pak.FullPath)) continue;
        try
        {
            var header = reader.ReadHeader(pak.FullPath);
            foreach (var e in reader.Read(pak.FullPath))
                if (e.FileName?.Contains(args[1], StringComparison.OrdinalIgnoreCase) == true) hits.Add((pak.FullPath, header, e));
        }
        catch { }
    }
    for (int i = 0; i < hits.Count && i < 400; i++) Console.WriteLine($"{i}: {hits[i].Entry.FileName}");
    if (args.Length > 2 && int.TryParse(args[2], out int n) && n < hits.Count)
    {
        var bytes = new ArtifactX.GameData.Services.PakExtractionService().ExtractEntryBytes(hits[n].Pak, hits[n].Entry, hits[n].Header)!;
        using var mbin = new MBINFile(new MemoryStream(bytes));
        mbin.Load();
        Console.WriteLine("type " + mbin.GetData()?.GetType().FullName);
        Console.WriteLine(JsonSerializer.Serialize((object?)mbin.GetData(), new JsonSerializerOptions
        {
            IncludeFields = true, WriteIndented = true, MaxDepth = 64,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        }));
    }
    return;
}

if (args.Length >= 4 && args[0] == "--creature-colour-probe")
{
    // Issue #15 probe: --creature-colour-probe <catalog> <scene path> <role seed hex> [colour seed hex]
    // The creature's seed-picked parts, then each big material's average painted colour with the palette table from the
    // seed itself (starship rule) and from its first child seed (multi-tool rule), to compare with a live screenshot.
    string catalog = Path.GetFullPath(args[1]);
    string scene = args[2].ToLowerInvariant();
    ulong seed = Convert.ToUInt64(args[3].Replace("0x", ""), 16);
    ulong colourSeed = args.Length > 4 ? Convert.ToUInt64(args[4].Replace("0x", ""), 16) : seed;
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
    connection.Open();
    var trees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var palettes = new SortedDictionary<int, ArtifactX.Core.Procedural.ColourPalette>();
    using (var q = connection.CreateCommand())
    {
        q.CommandText = "SELECT c.TemplateType, i.NameEnglish, i.UsageCategory, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType IN ('ShipDescriptors','Palettes')";
        using var r = q.ExecuteReader();
        while (r.Read())
            if (r.GetString(0) == "Palettes")
                palettes[int.Parse(r.GetString(1))] = new(JsonSerializer.Deserialize<float[][]>(r.GetString(3))!, r.GetString(2));
            else trees[r.GetString(1)] = r.GetString(3);
    }
    IReadOnlyList<ArtifactX.Core.Procedural.PartGroup>? Load(string p) => trees.TryGetValue(p, out var j) ? ArtifactX.Core.Procedural.ShipParts.ParseTree(j) : null;
    var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Walk(IReadOnlyList<ArtifactX.Core.Procedural.PartGroup> groups)
    {
        foreach (var g in groups) foreach (var o in g.Options) { all.Add(o.Id); foreach (var c in o.Children) Walk(c); }
    }
    Walk(Load(ArtifactX.Core.Procedural.ShipParts.DescriptorFor(scene)) ?? []);
    var chosen = ArtifactX.Core.Procedural.ShipParts.Generate(scene, seed, Load).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Console.WriteLine("parts: " + string.Join(" ", chosen));
    var models = new ShipModels(GameInstall.Find() ?? throw new InvalidOperationException("No game install found."));
    var mesh = models.Build(scene, all, chosen) ?? throw new InvalidOperationException("no mesh");
    static ulong Child(ulong s)
    {
        ulong lo = s & 0xFFFFFFFF, hi = s >> 32;
        ulong s0 = lo == 0 ? 1 : lo, s1 = (((lo << 16) | (lo >> 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        ulong Next() { ulong t = s0 * 0x5A76F899UL + s1; s0 = t & 0xFFFFFFFF; s1 = t >> 32; return s0; }
        ulong a = Next();
        ulong v = (Next() << 32) | a;
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7UL;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997UL;
        return v ^ (v >> 33);
    }
    static string Hex(float[] c) => $"#{(int)(c[0] * 255):X2}{(int)(c[1] * 255):X2}{(int)(c[2] * 255):X2}";
    foreach (var (rule, tableSeed) in new[] { ("raw", colourSeed), ("child", Child(colourSeed)) })
    {
        var table = ArtifactX.Core.Procedural.ShipLook.PaletteTable(tableSeed, palettes.Values.ToList());
        Func<string, string, float[]?> colour = (p, a) =>
        {
            int pi = ShipModels.PaletteIndex(p), ai = ShipModels.SlotIndex(a);
            return pi < 0 || pi >= table.Length || table[pi] is null || ai is < 0 or > 5 ? null : table[pi][Math.Min(ai, 3)];
        };
        Console.WriteLine($"== {rule} {tableSeed:X16}");
        foreach (string p in new[] { "Fur", "Scale", "Underbelly", "Rock", "Feather" })
        {
            int pi = ShipModels.PaletteIndex(p);
            Console.WriteLine($"   {p,-11} {string.Join(" ", table[pi].Select(Hex))}");
        }
        foreach (var m in mesh.Materials.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(8))
        {
            var t = models.Painted(m.Key, colour, 64, seed);
            string avg = "none";
            if (t is not null)
            {
                double r = 0, g = 0, b = 0, n = 0;
                foreach (uint px in t.Pixels) { double a = (px >> 24) / 255.0; r += ((px >> 16) & 255) * a; g += ((px >> 8) & 255) * a; b += (px & 255) * a; n += a; }
                avg = n > 0 ? $"#{(int)(r / n):X2}{(int)(g / n):X2}{(int)(b / n):X2}" : "clear";
            }
            Console.WriteLine($"   {m.Count(),6} {Path.GetFileName(m.Key),-40} {avg}");
        }
    }
    return;
}

if (args.Length >= 4 && args[0] == "--creature-part-probe")
{
    // Issue #15 probe: --creature-part-probe <catalog> <scene path> <chosen ids comma list> [extra id ...]
    // Builds the creature with the chosen parts, then with each extra id added, and prints where the added triangles
    // are (bounding box, materials), to tell what a part is (horns on the head, a plate round the neck...).
    string catalog = Path.GetFullPath(args[1]);
    string scene = args[2].ToLowerInvariant();
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
    connection.Open();
    using var query = connection.CreateCommand();
    query.CommandText = "SELECT i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id=i.CategoryId WHERE c.TemplateType='ShipDescriptors' AND i.NameEnglish=$p";
    query.Parameters.AddWithValue("$p", ArtifactX.Core.Procedural.ShipParts.DescriptorFor(scene));
    var tree = ArtifactX.Core.Procedural.ShipParts.ParseTree((string)query.ExecuteScalar()!);
    var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    void Walk(IReadOnlyList<ArtifactX.Core.Procedural.PartGroup> groups)
    {
        foreach (var g in groups) foreach (var o in g.Options) { all.Add(o.Id); foreach (var c in o.Children) Walk(c); }
    }
    Walk(tree);
    var game = GameInstall.Find() ?? throw new InvalidOperationException("No game install found.");
    var models = new ShipModels(game);
    static string Box(IEnumerable<System.Numerics.Vector3> pts)
    {
        var list = pts.ToList();
        if (list.Count == 0) return "none";
        var min = list.Aggregate(System.Numerics.Vector3.Min); var max = list.Aggregate(System.Numerics.Vector3.Max);
        return $"x {min.X:F2}..{max.X:F2} y {min.Y:F2}..{max.Y:F2} z {min.Z:F2}..{max.Z:F2}";
    }
    // "*" chooses every option: everything the scene can draw.
    var chosen = args[3] == "*" ? new HashSet<string>(all, StringComparer.OrdinalIgnoreCase)
        : args[3].Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var baseMesh = models.Build(scene, all, chosen) ?? throw new InvalidOperationException("no mesh");
    Console.WriteLine($"base: {baseMesh.Corners.Length / 3} triangles, {Box(baseMesh.Corners)}");
    foreach (var m in baseMesh.Materials.GroupBy(x => x).OrderByDescending(g => g.Count()))
    {
        // Which palette colours the material's texture layers ask for (every layer, first option).
        var asked = new SortedSet<string>();
        bool painted = models.Painted(m.Key, (p, a) => { asked.Add($"{p}/{a}"); return [1f, 1f, 1f]; }, 16) is not null;
        Console.WriteLine($"   {m.Count(),6} {Path.GetFileName(m.Key)}  {(painted ? asked.Count == 0 ? "fixed texture" : string.Join(" ", asked) : "no texture")}");
    }
    foreach (string id in chosen.ToList().Concat(args.Skip(4)))
    {
        bool removing = chosen.Contains(id);
        var set = new HashSet<string>(chosen, StringComparer.OrdinalIgnoreCase);
        if (removing) set.Remove(id); else set.Add(id);
        var mesh = models.Build(scene, all, set);
        if (mesh is null) { Console.WriteLine($"{id}: no mesh"); continue; }
        // The part's own triangles: those in one mesh and not the other (by corner position).
        var bigger = removing ? baseMesh : mesh; var smaller = removing ? mesh : baseMesh;
        var have = new HashSet<System.Numerics.Vector3>(smaller.Corners);
        var own = new List<System.Numerics.Vector3>(); var mats = new Dictionary<string, int>();
        for (int i = 0; i + 2 < bigger.Corners.Length; i += 3)
            if (!have.Contains(bigger.Corners[i]) || !have.Contains(bigger.Corners[i + 1]) || !have.Contains(bigger.Corners[i + 2]))
            {
                own.Add(bigger.Corners[i]); own.Add(bigger.Corners[i + 1]); own.Add(bigger.Corners[i + 2]);
                string mat = Path.GetFileName(bigger.Materials[i / 3 < bigger.Materials.Length ? i / 3 : 0]);
                mats[mat] = mats.GetValueOrDefault(mat) + 1;
            }
        Console.WriteLine($"{(removing ? "has" : "add")} {id,-18} {own.Count / 3,6} triangles  {Box(own)}  {string.Join(", ", mats.Select(k => $"{k.Key} {k.Value}"))}");
    }
    return;
}

if (args.Length == 2 && args[0] == "--multitool-kind-check")
{
    // Issue #60: the Multi-Tool Finder's kinds (Core MultiToolKindClassifier: catalog scene SizeTags) against the app's
    // Current System kinds (MultiToolKinds: weapon tags read from the game's model files) for 3,000 pool seeds.
    string catalog = Path.GetFullPath(args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={catalog};Mode=ReadOnly");
    connection.Open();
    string Row(string id)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
        query.Parameters.AddWithValue("$id", id);
        return (string)query.ExecuteScalar()!;
    }
    var modelTables = JsonSerializer.Deserialize<ArtifactX.Core.Universe.MultiToolModelTables>(Row("MULTITOOLMODEL"))!;
    var inventory = JsonSerializer.Deserialize<ArtifactX.Core.Universe.InventoryGenerationTables>(Row("INVENTORYGENERATION"))!;
    var classifier = new ArtifactX.Core.Universe.MultiToolKindClassifier(modelTables);
    var game = GameInstall.Find() ?? throw new InvalidOperationException("No game install found.");
    var tags = new ShipModels(game).WeaponTags(ArtifactX.Core.Universe.SystemMultiToolGenerator.MainModel);
    var tree = ArtifactX.Core.Procedural.ShipParts.ParseTree(modelTables.DescriptorJson);
    string descriptor = ArtifactX.Core.Procedural.ShipParts.DescriptorFor(modelTables.ModelPath);
    var rng = new Random(60);
    int same = 0, differ = 0;
    var counts = new Dictionary<string, int>();
    for (int n = 0; n < 3000; n++)
    {
        ulong address = ((ulong)rng.Next() << 32 | (uint)rng.Next()) & 0xFF_FFFF_FFFF_FFFFUL;
        foreach (var tool in ArtifactX.Core.Universe.SystemMultiToolGenerator.GeneratePool(address,
                     ArtifactX.Core.Universe.MultiToolPoolSource.SpaceStation, "Space Station", definitions: inventory.MultiToolPools)
                 .Where(t => t.PoolType == "Standard").Take(1))
        {
            string core = classifier.KindOf(tool);
            var picked = ArtifactX.Core.Procedural.ShipParts.Generate(modelTables.ModelPath, tool.Seed,
                    p => string.Equals(p, descriptor, StringComparison.OrdinalIgnoreCase) ? tree : null)
                .Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string app = picked.Contains("_MULTITOOL_ALIENXRARE") ? "Alien" : picked.Contains("_MULTITOOL_STARTXRARE") ? "Starter"
                : picked.Contains("_MULTITOOL_PRISTINEXRARE") ? "Experimental" : !picked.Contains("_MULTITOOL_NORMAL") ? "?"
                : picked.Any(p => tags.TryGetValue(p, out var t) && t is "WEAPON_LARGE" or "WEAPON_MEDIUM") ? "Rifle" : "Pistol";
            counts[core] = counts.GetValueOrDefault(core) + 1;
            if (core == app) same++;
            else { differ++; if (differ <= 10) Console.WriteLine($"  differ: seed {tool.Seed:X16} finder {core} app {app}"); }
        }
    }
    Console.WriteLine($"kinds: {string.Join(", ", counts.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}"))}");
    if (differ > 0) throw new InvalidOperationException($"{differ} of {same + differ} kinds differ from the app's.");
    Console.WriteLine($"PASS: Multi-Tool Finder kinds match the app's for {same} seeds.");
    return;
}

if (args.Length == 3 && args[0] == "--industrial-cleanup-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("Use a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddIndustrialCleanup(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open();
    using var query = connection.CreateCommand();
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='INDUSTRIALCLEANUP'";
    var guide = JsonSerializer.Deserialize<ArtifactX.Core.IndustrialCleanup.IndustrialCleanupDefinition>((string)query.ExecuteScalar()!)!;
    if (guide.Title != "Industrial Cleanup" || guide.Instructions.Length < 4 || guide.Counters.Length != 5
        || !guide.Counters.Select(c => c.StatId).ToHashSet().SetEquals(new[] { "CARGO_VALUE", "CARGO_SCRAP", "CARGO_TOX", "CARGO_RAD", "CARGO_EXP" })
        || !guide.FindingText.Contains("Exocraft Radar") || !guide.Instructions.Any(i => i.Text.Contains("maximum resource recovery")))
        throw new InvalidOperationException("Cleanup definitions differ from inspected build 184271.");
    Console.WriteLine("PASS: normal cleanup tutorial, discovery instructions and five mission-defined lifetime counters.");
    return;
}

if (args.Length == 3 && args[0] == "--title-unlocks-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddTitleUnlocks(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open();
    using var query = connection.CreateCommand();
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='TITLEUNLOCKS'";
    var rules = JsonSerializer.Deserialize<TitleUnlockRule[]>((string)query.ExecuteScalar()!)!;
    // Installed build 184271 references, inspected 2026-11-02. These assert
    // declared requirements, not that meeting them grants a cosmetic in game.
    foreach (var (special, stat, amount) in new[]
    {
        ("TRUCK_CAB02", "CARGO_DESTROYED", 100f),
        ("TRUCK_BODY03", "CARGO_DESTROYED", 200f),
        ("TRUCK_WHEELS02", "CARGO_SCRAP", 75f),
        ("TRUCK_WHEELS03", "CARGO_EXP", 50f),
        ("TRUCK_WHEELS06", "CARGO_TOX", 50f),
        ("TRUCK_WHEELS07", "CARGO_RAD", 50f)
    })
    {
        var rule = rules.Single(r => r.SpecialIds.Contains(special));
        if (rule.StatId != stat || rule.StatValue != amount || string.IsNullOrWhiteSpace(rule.Title)
            || string.IsNullOrWhiteSpace(rule.Requirement))
            throw new InvalidOperationException($"Title reward mapping differs for {special}.");
    }
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='VEHICLECUSTOMISATION'";
    var options = JsonSerializer.Deserialize<VehicleCustomisationOption[]>((string)query.ExecuteScalar()!)!;
    var cabin = options.Single(o => o.SpecialId == "TRUCK_CAB02");
    if (cabin.Vehicle != 2 || cabin.Group != "CABIN" || cabin.Name != "Widescreen Cabin"
        || !options.Any(o => o.Vehicle == 2 && o.Group == "PAINT" && o.SpecialId == "TRUCK_PAINT02")
        || options.GroupBy(o => (o.Vehicle, o.Group, o.OptionId)).Any(g => g.Count() != 1)
        || options.Any(o => string.IsNullOrWhiteSpace(o.Name) || string.IsNullOrWhiteSpace(o.Group)))
        throw new InvalidOperationException("Vehicle customization associations or localization differ from references.");
    Console.WriteLine($"PASS: {rules.Length} title reward rules and {options.Length} vehicle choices; six Colossus references and localized instructions match.");
    return;
}

if (args.Length is 3 or 4 && args[0] == "--extraction-site-check")
{
    ExtractionCatalogChecks.Sites(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}

if (args.Length == 4 && args[0] == "--building-layout-check")
{
    BuildingLayoutChecks.Live(args[1], args[2], args[3]);
    return;
}

if (args.Length == 3 && args[0] == "--extraction-world-check")
{
    ExtractionCatalogChecks.World(args[1], args[2]);
    return;
}

if (args.Length == 3 && args[0] is "--food-catalog-check" or "--creature-source-catalog-check" or "--extraction-catalog-check")
{
    string output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("The check requires a new scratch catalog.");
    using (var db = new CatalogDbContext(output)) db.Database.EnsureCreated();
    CatalogSteps.AddPlanetGeneration(output, args[1]);
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={output};Mode=ReadOnly");
    connection.Open(); using var query = connection.CreateCommand();
    if (args[0] == "--extraction-catalog-check")
    {
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='EXTRACTIONTABLES'";
        var extractionTables = JsonSerializer.Deserialize<ExtractionTables>((string)query.ExecuteScalar()!)!;
        if (extractionTables.Hotspots.Length != 6 || extractionTables.BiomeGases.Length != 17 || extractionTables.Substances.Length == 0 || extractionTables.Networks.Length == 0)
            throw new InvalidOperationException("Extraction tables are incomplete.");
        var substance = extractionTables.Substances.Single(s => s.Id == "LUSH1");
        if (ExtractionStock.DisplayAmount(10033886, substance) != 6750)
            throw new InvalidOperationException("Extracted Paraffinium yield differs from the observed stock.");
        if (extractionTables.Networks.Single(n => n.PartId == "U_EXTRACTOR_S").HotspotKind != "Mineral"
            || extractionTables.Networks.Single(n => n.PartId == "U_GASEXTRACTOR").HotspotKind != "Gas"
            || extractionTables.Networks.Single(n => n.PartId == "U_SILO_S").Storage != 1440000)
            throw new InvalidOperationException("Installed extraction equipment definitions differ from references.");
        Console.WriteLine($"PASS: extracted {extractionTables.Hotspots.Length} hotspot categories, {extractionTables.BiomeGases.Length} biome gases, {extractionTables.Substances.Length} yields and {extractionTables.Networks.Length} resource-link definitions.");
        return;
    }
    if (args[0] == "--creature-source-catalog-check")
    {
        query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='CREATUREPRODUCTS'";
        var sources = JsonSerializer.Deserialize<CreatureProductSource[]>((string)query.ExecuteScalar()!)!;
        var meat = sources.Where(s => s.ItemId == "FOOD_M_DIGGER").ToArray();
        if (meat.Length != 2 || meat.Any(s => s.Method != "Hunt" || s.Habitat != "Ground")
            || !meat.Select(s => s.CreatureKind).Order().SequenceEqual(new[] { "Plough", "ProtoDigger" }))
            throw new InvalidOperationException("Dirty Meat creature source mapping differs from the installed reference.");
        if (!sources.Any(s => s.Method == "Harvest") || sources.Any(s => string.IsNullOrWhiteSpace(s.ItemId)))
            throw new InvalidOperationException("Creature product extraction is incomplete.");
        Console.WriteLine($"PASS: extracted {sources.Length} creature product mappings; Dirty Meat hunting and habitats match installed tables.");
        return;
    }
    query.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId='FOODINGESTOR'";
    var tables = JsonSerializer.Deserialize<FoodIngestorTables>((string)query.ExecuteScalar()!)!;
    if (tables.Foods.Length == 0 || tables.BaseDuration <= 0 || tables.Foods.Any(f => !float.IsFinite(FoodIngestorBonus.Calculate(f))))
        throw new InvalidOperationException("Invalid food bonus extraction.");
    Console.WriteLine($"PASS: catalog build extracted {tables.Foods.Length} food bonus definitions and duration from the installed game.");
    return;
}

// Synthetic binary records contain no extracted game content.
const uint unknownHash = 0xFFFFFFFF;
if (NMSTemplate.GetTemplateType(unknownHash) is not null)
    throw new InvalidOperationException("Test hash is no longer unknown");
using var emptyReader = new BinaryReader(new MemoryStream());
ExpectTemplateError(() => NMSTemplate.DeserializeBinaryTemplate(emptyReader, (string)null!), "missing name");
using (var mbin = new MBINFile(new MemoryStream(new byte[32])))
{
    mbin.Header = new MBINHeader { NameHash = unknownHash };
    ExpectTemplateError(() => mbin.GetData(), "FFFFFFFF");
}
if (NMSTemplate.TemplateFromNameHash(unknownHash) is not null
    || NMSTemplate.DeserializeBinaryTemplate(emptyReader, unknownHash) is not null)
    throw new InvalidOperationException("Unknown hash unexpectedly resolved");

foreach (bool empty in new[] { false, true })
{
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
    {
        writer.Write(16L); writer.Write(1); writer.Write(1u); // Generic list header.
        writer.Write(empty ? 0L : 16L); writer.Write(unknownHash); writer.Write(0xEEEEEE01u);
    }
    stream.Position = 0;
    using var reader = new BinaryReader(stream);
    if (!empty) ExpectTemplateError(() => NMSTemplate.DeserializeGenericList(reader, new NMSTemplate()), "FFFFFFFF");
    else if (NMSTemplate.DeserializeGenericList(reader, new NMSTemplate()) is not { Count: 1 } list
        || list[0].GetType().Name != "EmptyNode")
        throw new InvalidOperationException("Empty component placeholder was not preserved");
}
Console.WriteLine("PASS: missing name, unknown hash lookup, unknown component diagnostic and empty component placeholder.");

static void ExpectTemplateError(Action action, string diagnostic)
{
    try { action(); }
    catch (DeserializeTemplateException ex) when (ex.Message.Contains(diagnostic, StringComparison.Ordinal)) { return; }
    throw new InvalidOperationException($"Expected template error containing {diagnostic}");
}


// Keep both a WAL reader and a source writer open across publication. These are
// synthetic catalogs; no installed catalog or player database is changed.
string folder = Path.Combine(Path.GetTempPath(), "ArtifactX-catalog-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    string target = Path.Combine(folder, "catalog.sqlite");
    string source = Path.Combine(folder, "next.sqlite");
    using var reader = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={target};Pooling=False");
    using var writer = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={source};Pooling=False");
    reader.Open(); writer.Open();
    void Execute(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }
    Execute(reader, "PRAGMA journal_mode=WAL; CREATE TABLE Version(Value INTEGER); INSERT INTO Version VALUES(51)");
    Execute(writer, "PRAGMA journal_mode=WAL; CREATE TABLE Version(Value INTEGER); INSERT INTO Version VALUES(52)");
    ArtifactX.GameData.Services.CatalogPublication.Publish(source, target);
    using var query = reader.CreateCommand(); query.CommandText = "SELECT Value FROM Version";
    if (Convert.ToInt32(query.ExecuteScalar()) != 52)
        throw new InvalidOperationException("Open catalog connection retained stale WAL data after publication");
    using var fresh = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={target};Mode=ReadOnly;Pooling=False");
    fresh.Open(); using var freshQuery = fresh.CreateCommand(); freshQuery.CommandText = "SELECT Value FROM Version";
    if (Convert.ToInt32(freshQuery.ExecuteScalar()) != 52)
        throw new InvalidOperationException("Fresh catalog connection retained stale WAL data after publication");
    Execute(writer, "UPDATE Version SET Value=53");
    using (var snapshot = reader.BeginTransaction(deferred: true))
    {
        query.Transaction = snapshot;
        if (Convert.ToInt32(query.ExecuteScalar()) != 52)
            throw new InvalidOperationException("Reader snapshot did not start on the published catalog");
        ArtifactX.GameData.Services.CatalogPublication.Publish(source, target);
        if (Convert.ToInt32(query.ExecuteScalar()) != 52)
            throw new InvalidOperationException("Publication disrupted an active reader snapshot");
        snapshot.Commit();
    }
    query.Transaction = null;
    if (Convert.ToInt32(query.ExecuteScalar()) != 53 || Convert.ToInt32(freshQuery.ExecuteScalar()) != 53)
        throw new InvalidOperationException("Readers did not see the next catalog after ending the snapshot");
    Console.WriteLine("PASS: catalog publication updates open and fresh readers, includes source WAL changes and preserves active reader snapshots.");
}
finally { Directory.Delete(folder, recursive: true); }

static TkSceneNodeData Node(string name, float x, float y, float z) => new()
{
    Name = new(name), Children = [], Attributes = [],
    Transform = new() { TransX = x, TransY = y, TransZ = z, ScaleX = 1, ScaleY = 1, ScaleZ = 1 }
};

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
