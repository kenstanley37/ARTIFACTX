using ArtifactX.Core.Companions;
using ArtifactX.Core.Recipes;
using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;
using Microsoft.Data.Sqlite;
using System.Text.Json;

/// <summary>
/// The mobile rules file gives the phone exactly what the desktop gets: a written-and-read rules file against the
/// catalog read directly (the desktop path), for portal positions and hotspot / base-computer plans across many
/// systems, plus the in-game ground truth for the Near Portal planner (#63, Uwaiimor XIII, 2026-11-17).
/// </summary>
static class MobileRulesChecks
{
    public static void Run(string catalogPath)
    {
        // The phone's path: export, write, read back.
        var exported = MobileRulesExportService.Read(catalogPath);
        using var buffer = new MemoryStream();
        exported.Write(buffer);
        buffer.Position = 0;
        var phone = GameRules.Read(buffer);
        Console.WriteLine($"Rules file: {phone.Rows.Count} tables, {phone.ItemNames.Count} names, {buffer.Length / 1024} KB ({phone.GameBuildLabel}).");

        // The desktop's path: the catalog rows read straight from SQLite (CatalogService / PlanetHotspotService).
        using var connection = new SqliteConnection($"Data Source={catalogPath};Mode=ReadOnly");
        connection.Open();
        T Desktop<T>(string id) where T : class
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DescriptionEnglish FROM Items WHERE GameId=$id";
            command.Parameters.AddWithValue("$id", id);
            return JsonSerializer.Deserialize<T>((string)command.ExecuteScalar()!)!;
        }
        var pcWorld = new ExtractionWorldTables(Desktop<BiomeTables>("BIOMETABLES"), Desktop<PlanetProfileTables>("PLANETPROFILE"),
            Desktop<ExtractionTables>("EXTRACTIONTABLES"));
        var pcStations = Desktop<StationEquipmentTables>("STATIONEQUIPMENT");
        var pcRules = Desktop<BuildingOverrideRule[]>("BUILDINGOVERRIDES");
        var phoneWorld = new ExtractionWorldTables(phone.Biomes!, phone.Profiles!, phone.Extraction!);
        var (phoneStations, phoneRules) = (phone.Stations!, phone.BuildingOverrides!);

        int failures = 0, portals = 0, plans = 0;
        void Fail(string message) { failures++; Console.WriteLine("FAIL: " + message); }

        // A fixed spread of systems (galaxies 1, 2 and 219, regions across the map), every planet.
        var random = new Random(185034);
        var systems = new List<ulong> { 0x3EFDAFBFFB004 };
        for (int i = 0; i < 40; i++)
        {
            ulong galaxy = (ulong)(i % 3 == 2 ? 218 : i % 2);
            ulong address = (galaxy << 32) | ((ulong)(uint)random.Next(0, 0x1000) << 40) | ((ulong)(uint)random.Next(0, 0x100) << 24)
                | ((ulong)(uint)random.Next(0, 0x1000) << 12) | (uint)random.Next(1, 0x300);
            systems.Add(address);
        }

        foreach (ulong system in systems)
        {
            var pc = PortalPositions.ForSystem(system, pcWorld.Biomes, pcWorld.Profiles, pcStations, pcRules);
            var ph = PortalPositions.ForSystem(system, phoneWorld.Biomes, phoneWorld.Profiles, phoneStations, phoneRules);
            if (pc.Count != ph.Count) { Fail($"{system:X}: {pc.Count} portals on PC, {ph.Count} from the rules file"); continue; }
            foreach (var (planet, at) in pc)
            {
                portals++;
                if (!ph.TryGetValue(planet, out var other) || other != at) { Fail($"{system:X} planet {planet}: portal differs"); continue; }
                ulong planetAddress = system | ((ulong)planet << 52);
                var a = HotspotPlanner.Plan(planetAddress, at.Latitude, at.Longitude, pcWorld, _ => null, true, at.Latitude, at.Longitude);
                var b = HotspotPlanner.Plan(planetAddress, at.Latitude, at.Longitude, phoneWorld, _ => null, true, at.Latitude, at.Longitude);
                plans++;
                if (a.Hotspots.Count != b.Hotspots.Count || a.Hotspots.Zip(b.Hotspots).Any(p => p.First.CandidateKey != p.Second.CandidateKey
                        || p.First.Latitude != p.Second.Latitude || p.First.Longitude != p.Second.Longitude)
                    || (a.PortalSites?.Count ?? 0) != (b.PortalSites?.Count ?? 0)
                    || (a.PortalSites ?? []).Zip(b.PortalSites ?? []).Any(p => p.First.BaseCoordinates != p.Second.BaseCoordinates))
                    Fail($"{system:X} planet {planet}: hotspot plan differs");
            }
        }
        Console.WriteLine($"{portals} portals and {plans} hotspot plans compared across {systems.Count} systems.");

        // In game (Slot 8, Uwaiimor XIII, option 1 "Portal + Power + Mineral"): the base computer spot at -7.83, -162.84
        // read Hotspot Proximity 113.49 to the C-class power hotspot and 256.54 to the A-class Dioxite deposit.
        const ulong uwaiimor = 0x3EFDAFBFFB004UL | (1UL << 52);
        var portal = PortalPositions.ForSystem(0x3EFDAFBFFB004, phoneWorld.Biomes, phoneWorld.Profiles, phoneStations, phoneRules).GetValueOrDefault(1);
        if (portal is null) Fail("Uwaiimor XIII: no portal");
        else
        {
            var plan = HotspotPlanner.Plan(uwaiimor, portal.Latitude, portal.Longitude, phoneWorld, phone.ItemName, true, portal.Latitude, portal.Longitude);
            var spot = plan.PortalSites?.FirstOrDefault();
            bool power = spot?.Hotspots.Any(h => h.Kind == "Power" && h.Class == "C") == true;
            bool dioxite = spot?.Hotspots.Any(h => h.Class == "A" && h.ResourceName.Contains("Dioxite", StringComparison.OrdinalIgnoreCase)) == true;
            bool where = spot is not null && Math.Abs(spot.BaseLatitude - -7.83) < 0.006 && Math.Abs(spot.BaseLongitude - -162.84) < 0.006;
            if (!power || !dioxite || !where)
                Fail($"Uwaiimor XIII: first base spot {spot?.BaseCoordinates ?? "none"} (power C {power}, Dioxite A {dioxite})");
            else Console.WriteLine($"Uwaiimor XIII: portal {portal.Coordinates}, base computer {spot!.BaseCoordinates} with power C and Dioxite A - matches the game.");
        }

        // Uwaiimor XIII is body 1 of a gas-giant system without the GasGiantPlanet roll: a size-4 Giant, not a
        // true gas giant (radius 523264 read from the game, 2026-11-17). The phone once called it "Medium".
        string uwaiimorCode = ((1UL << 44) | (0x3EFUL << 32) | 0xFBFFB004).ToString("X12");
        var readoutBody = PortalSystemReadout.Generate(uwaiimorCode, 219).Bodies.FirstOrDefault();
        if (readoutBody?.SizeLabel != "Giant") Fail($"Uwaiimor XIII: phone size label {readoutBody?.SizeLabel ?? "none"}, expected Giant");
        else Console.WriteLine($"Uwaiimor XIII: phone readout says {readoutBody.Name} is a Giant.");

        RecipesAndEggs(phone, connection, Desktop<EggSequencerRules>, Fail);

        Console.WriteLine(failures == 0 ? "PASS: the rules file gives the phone the desktop's portals, hotspots, base spots, recipes and egg rules." : $"{failures} failures.");
        if (failures > 0) Environment.ExitCode = 1;
    }

    /// <summary>The phone's recipes and Egg Sequencer against the desktop's catalog rows: the same recipes and the same
    /// sequencer items with the same effects; then the shared wording (EggSequencerText) against the owner's in-game
    /// sequencer screens of 2026-11-04 (as EggSequencerChecks in Core.Checks).</summary>
    private static void RecipesAndEggs(GameRules phone, SqliteConnection connection, Func<string, EggSequencerRules> desktop,
        Action<string> fail)
    {
        var pcRecipes = 0;
        var pcValues = new List<(string Id, string Name, bool IsSubstance, long Value)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT c.TemplateType, i.NameEnglish, i.DescriptionEnglish FROM Items i JOIN Categories c ON c.Id = i.CategoryId
                WHERE c.TemplateType IN ('Recipes', 'ItemValues') AND i.DescriptionEnglish IS NOT NULL
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(0) == "Recipes") { pcRecipes++; continue; }
                using var json = JsonDocument.Parse(reader.GetString(2));
                var root = json.RootElement;
                if (root.GetProperty("kind").GetString() is "Product" or "Substance")
                    pcValues.Add((root.GetProperty("id").GetString()!, reader.IsDBNull(1) ? "" : reader.GetString(1),
                        root.GetProperty("kind").GetString() == "Substance", root.GetProperty("value").GetInt64()));
            }
        }
        if (phone.Recipes.Count != pcRecipes || phone.Recipes.Any(r => r.Ingredients.Count == 0 || r.ResultId.Length == 0))
            fail($"recipes: phone {phone.Recipes.Count}, catalog {pcRecipes}");
        if (phone.Food is null) fail("no Nutrient Ingestor table on the phone");

        var pcEgg = desktop("EGGSEQUENCER");
        if (phone.EggSequencer is not { } egg) { fail("no Egg Sequencer rules on the phone"); return; }
        long full = phone.ItemValues.GetValueOrDefault(egg.FullDoseProductId)?.Value ?? 0;
        long pcFull = pcValues.FirstOrDefault(v => v.Id == pcEgg.FullDoseProductId).Value;
        var pcItems = EggSequencer.Catalysts(pcEgg, pcValues, pcFull).ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        var phoneItems = EggSequencer.Catalysts(egg, phone.ItemValues.Select(v => (v.Key, phone.ItemName(v.Key) ?? "", v.Value.IsSubstance, v.Value.Value)), full);
        if (full != pcFull || phoneItems.Count != pcItems.Count
            || phoneItems.Any(i => !pcItems.TryGetValue(i.Id, out var pc) || pc.Effect != i.Effect || pc.Value != i.Value))
            fail($"egg items: phone {phoneItems.Count} (full dose {full}), desktop {pcItems.Count} (full dose {pcFull})");

        // The game's own screens: "Overdosed! (102%)" for 100 Frozen Tubers, "Dose: 0%" for one Ferrite Dust, two
        // Viscous Custard "Dose: 100%" and Gentleness "Increasing", two Ice Cream Weight / Height "Increasing".
        var byId = phoneItems.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
        (EggCatalystSlot Slot, string Id, int Count, string Dose, string? Label, string? Status)[] screens =
        [
            (EggCatalystSlot.GeneSplitter, "FOOD_P_COLDWILD", 100, "Overdosed! (102%)", null, "UI_EGG_MODIFIER_OUT_UNSTABLE"),
            (EggCatalystSlot.DyeInjector, "LAND1", 1, "Dose: 0%", null, null),   // only the dose was recorded
            (EggCatalystSlot.NeuralCalibrator, "FOOD_R_CUSTARD", 2, "Dose: 100%", "UI_PET_GENTLENESS_RATING", "UI_EGG_MODIFIER_OUT_INCREASING"),
            (EggCatalystSlot.GrowthHormone, "FOOD_ICE", 2, "Dose: 100%", "WEIGHT_HEIGHT", "UI_EGG_MODIFIER_OUT_INCREASING"),
        ];
        foreach (var s in screens)
        {
            if (!byId.TryGetValue(s.Id, out var item)) { fail($"egg screen: {s.Id} is not a sequencer item on the phone"); continue; }
            var report = EggSequencerText.Describe(s.Slot, item, s.Count, null, full, egg);
            string label = s.Label is null ? report.StatusLabel : EggSequencerText.Text(egg, s.Label);
            if (report.DoseText != s.Dose || (s.Status is not null && report.StatusText != EggSequencerText.Text(egg, s.Status)) || report.StatusLabel != label)
                fail($"egg screen: {s.Count} {item.Name} in the {s.Slot}: {report.DoseText}, {report.StatusLabel} {report.StatusText}");
        }
        Console.WriteLine($"Phone recipes: {phone.Recipes.Count:N0}; egg items: {phoneItems.Count:N0}, matching the desktop and four in-game sequencer screens.");
    }
}
