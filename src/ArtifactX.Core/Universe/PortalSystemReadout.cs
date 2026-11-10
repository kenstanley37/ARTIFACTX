using System.Globalization;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Theming;

namespace ArtifactX.Core.Universe;

/// <summary>
/// A planet or moon generated from a portal address for the System from Glyphs readout.
/// </summary>
public sealed record GeneratedSystemBody(
    int BodyIndex,
    string Name,
    ulong Seed,
    bool IsMoon,
    int HostBodyIndex,
    string HostBodyName,
    string SizeLabel,
    bool HasRings,
    bool IsSafeStart,
    bool IsPrime,
    bool IsDialedBody,
    string BodyPortalCode,
    string SwatchHex,
    string PrimaryLine,
    string SecondaryLine)
{
    public string SeedHex => $"0x{Seed:X16}";
}

/// <summary>
/// A native starship or fleet vehicle generated from a system's portal address.
/// </summary>
public sealed record GeneratedSystemShip(
    int Index,
    SystemStarshipType Type,
    string TypeLabel,
    string Name,
    ulong Seed,
    ShipSClassStats SClassStats,
    string SpawnChanceSummary)
{
    public string IndexLabel => $"#{Index:D2}";
    public string SeedHex => $"0x{Seed:X16}";
    public string StatsLine => $"S-Class base: {SClassStats.CompactStatsSummary}";
    public string SlotsLine => SClassStats.SlotsSummary;
}

/// <summary>
/// A native multi-tool in a reload body's cabinet pool.
/// </summary>
public sealed record GeneratedSystemMultiTool(
    int Index,
    string Name,
    string KindLabel,
    ulong Seed,
    string SourceLabel,
    MultiToolSClassStats SClassStats)
{
    public string SeedHex => $"0x{Seed:X16}";
    public string StatsLine => $"S-Class base: {SClassStats.CompactStatsSummary} · {SClassStats.MaxTechSlots} Tech";
}

/// <summary>
/// The multi-tools offered by system cabinets when the game is loaded at a specific body (0 = Space Station, 1..N = Planet/Moon).
/// </summary>
public sealed record GeneratedMultiToolLoadPool(
    int BodyIndex,
    string PlaceTitle,
    string ReloadInstruction,
    IReadOnlyList<GeneratedSystemMultiTool> Tools);

/// <summary>
/// Complete address-derived readout for a star system from its 12-character portal glyph code and 1-based galaxy number.
/// Works entirely from <see cref="ArtifactX.Core"/> procedural maths without needing game files or a save file.
/// </summary>
public sealed record PortalSystemReadout(
    string PortalCode,
    ulong PortalCodeValue,
    ulong PackedAddress,
    int GalaxyNumber,
    string GalaxyName,
    string GalaxyClassification,
    int DialedBodyIndex,
    string SystemName,
    string RegionName,
    string GalacticCoordinates,
    double DistanceToCoreLightYears,
    bool RequiresPurpleStarAccess,
    SystemAttributes Attributes,
    string StarPill,
    string RacePill,
    string EconomyWealthPill,
    string ConflictPill,
    string CompactSummary,
    IReadOnlyList<PortalGlyphVector> GlyphVectors,
    IReadOnlyList<GeneratedSystemBody> Bodies,
    string ShipRulesSummary,
    IReadOnlyList<GeneratedSystemShip> Ships,
    string MultiToolOddsSummary,
    string MultiToolExplanation,
    IReadOnlyList<GeneratedMultiToolLoadPool> MultiToolPools)
{
    public static bool TryGenerate(string? input, int galaxyNumber, out PortalSystemReadout? readout, out string error)
    {
        readout = null;
        if (!NmsPortalCode.TryParse(input, out string code, out error))
            return false;

        if (galaxyNumber is < 1 or > 256)
        { error = "Select a galaxy between 1 and 256."; return false; }

        readout = Generate(code, galaxyNumber);
        return true;
    }

    public static PortalSystemReadout Generate(string portalCode, int galaxyNumber = 1)
    {
        if (!NmsPortalCode.TryParse(portalCode, out string normalizedCode, out _))
            throw new ArgumentException("Enter a complete portal address.", nameof(portalCode));
        ArgumentOutOfRangeException.ThrowIfLessThan(galaxyNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(galaxyNumber, 256);

        int clampedGalaxy = Math.Clamp(galaxyNumber, 1, 256);
        int galaxy0 = clampedGalaxy - 1;

        ulong codeValue = ulong.Parse(normalizedCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int dialedBody = (int)((codeValue >> 44) & 0xF);
        if (dialedBody == 0) dialedBody = 1;

        ulong packedAddress = NmsPortalCode.ToAddress(normalizedCode, clampedGalaxy);
        string coords = NmsPortalCode.ToCoordinates(normalizedCode);
        double distToCore = NmsPlanets.DistanceToCoreLightYears(packedAddress);
        bool requiresPurple = PortalTravel.RequiresPurpleStarAccess(normalizedCode);

        string galaxyName = NmsGalaxyNames.ForNumber(clampedGalaxy) ?? $"Galaxy {clampedGalaxy}";
        string galaxyClass = NmsGalaxyInfo.Get(clampedGalaxy)?.ClassificationName ?? "Normal";

        string systemName = SystemGenerator.SystemName(codeValue, galaxy0);
        string regionName = SystemGenerator.RegionName(codeValue, galaxy0);
        var attrs = SystemGenerator.Attributes(codeValue, galaxy0);
        var bodies = SystemGenerator.Bodies(codeValue, galaxy0);

        string starPill = attrs.GasGiant
            ? $"{attrs.Star} star · Gas giant"
            : $"{attrs.Star} star";

        string racePill = attrs.Uncharted
            ? "Uncharted"
            : attrs.Abandoned
                ? "Abandoned"
                : attrs.Race switch
                {
                    SystemRace.Gek => "Gek",
                    SystemRace.Korvax => "Korvax",
                    SystemRace.Vykeen => "Vy'keen",
                    _ => "Uncharted",
                };

        string economyName = attrs.Uncharted || attrs.Abandoned
            ? "No economy"
            : attrs.Economy switch
            {
                SystemEconomy.Trading => "Trading",
                SystemEconomy.AdvancedMaterials => "Advanced Materials",
                SystemEconomy.Scientific => "Scientific",
                SystemEconomy.Mining => "Mining",
                SystemEconomy.Manufacturing => "Manufacturing",
                SystemEconomy.Technology => "Technology",
                SystemEconomy.PowerGeneration => "Power Generation",
                _ => "None",
            };

        string wealthLabel = attrs.Uncharted || attrs.Abandoned
            ? "None"
            : attrs.Pirate
                ? "Black Market"
                : attrs.Wealth switch
                {
                    3 => "High ★★★ wealth",
                    2 => "Medium ★★ wealth",
                    _ => "Low ★ wealth",
                };

        string economyWealthPill = attrs.Uncharted || attrs.Abandoned
            ? "No station"
            : $"{economyName} · {wealthLabel}";

        string conflictPill = attrs.Uncharted || attrs.Abandoned
            ? "No conflict"
            : attrs.Pirate
                ? "Pirate-controlled"
                : attrs.Conflict switch
                {
                    3 => "High conflict",
                    2 => "Medium conflict",
                    _ => "Low conflict",
                };

        string bodiesCountText = bodies.MoonCount > 0
            ? $"{bodies.PlanetCount} planet{(bodies.PlanetCount == 1 ? "" : "s")}, {bodies.MoonCount} moon{(bodies.MoonCount == 1 ? "" : "s")}"
            : $"{bodies.PlanetCount} planet{(bodies.PlanetCount == 1 ? "" : "s")}";

        string compactSummary = attrs.Uncharted
            ? $"Uncharted · {attrs.Star} star · {bodiesCountText}"
            : $"{racePill} · {economyName} · {bodiesCountText}";

        var glyphVectors = normalizedCode.Select(ArtifactXPortalGlyphs.ForDigit).ToArray();

        // Build planet & moon list
        int totalBodies = bodies.PlanetSeeds.Length;
        string[] rawNames = Array.ConvertAll(bodies.PlanetSeeds, SystemGenerator.PlanetName);
        var bodyList = new List<GeneratedSystemBody>(totalBodies);
        int sizeCursor = 0;
        int planetNumber = 0;

        for (int i = 0; i < totalBodies; i++)
        {
            int bodyIndex = i + 1;
            ulong seed = bodies.PlanetSeeds[i];
            string bodyName = rawNames[i];
            bool isMoon = bodies.IsMoon(bodyIndex);
            int hostIndex = i < bodies.Hosts.Length ? bodies.Hosts[i] : 0;
            string hostName = hostIndex >= 1 && hostIndex <= rawNames.Length ? rawNames[hostIndex - 1] : $"Planet {hostIndex}";

            int rawSize = isMoon
                ? 3
                : attrs.GasGiantPlanet && i == 0
                    ? 4
                    : sizeCursor < bodies.Sizes.Length
                        ? bodies.Sizes[sizeCursor++]
                        : 1;

            if (!isMoon) planetNumber++;

            string sizeLabel = rawSize switch
            {
                4 => "Gas Giant",
                3 => "Moon",
                0 => "Large",
                1 => "Medium",
                _ => "Small",
            };

            bool hasRings = !isMoon && PlanetRings.HasRings(codeValue, galaxy0, bodyIndex);
            bool isSafeStart = attrs.SafeStartPlanet == bodyIndex;
            bool isPrime = bodyIndex > attrs.PlanetCount;
            bool isDialed = bodyIndex == dialedBody;
            string bodyPortalCode = $"{bodyIndex:X}{normalizedCode[1..]}";

            const string swatchHex = "#5CE1E6";
            string primaryLine = isMoon ? $"Moon of {hostName}" : $"Planet {planetNumber} ? {sizeLabel}";
            if (hasRings) primaryLine += " ? Planetary rings";
            string secondaryLine = $"Portal address {bodyPortalCode}";

            bodyList.Add(new GeneratedSystemBody(
                bodyIndex,
                bodyName,
                seed,
                isMoon,
                hostIndex,
                hostName,
                sizeLabel,
                hasRings,
                isSafeStart,
                isPrime,
                isDialed,
                bodyPortalCode,
                swatchHex,
                primaryLine,
                secondaryLine));
        }

        // Build starship pool
        string shipRulesSummary = EquipmentGeneration.FormatSystemShipRules(
            attrs.Wealth,
            wealthLabel.Replace(" wealth", ""),
            attrs.Pirate,
            attrs.Uncharted,
            attrs.Abandoned,
            hasCorruptedPlanet: false);

        var rawShips = SystemStarshipGenerator.Generate(
            codeValue,
            attrs,
            words: null,
            includeFleet: false,
            includeSentinel: false);

        var orderedShips = rawShips
            .OrderByDescending(s => s.Type == SystemStarshipType.Exotic)
            .ThenByDescending(s => s.Type == SystemStarshipType.SentinelInterceptor)
            .ThenBy(s => s.Index)
            .Select(s =>
            {
                var stats = EquipmentGeneration.CalculateShipSClassStats(s.Type, s.Seed);
                string spawnChance = EquipmentGeneration.FormatShipSpawnChance(
                    s.Type, attrs.Wealth, attrs.Pirate, attrs.Uncharted, attrs.Abandoned);
                string name = $"{s.TypeLabel} {s.Index}";
                return new GeneratedSystemShip(s.Index, s.Type, s.TypeLabel, name, s.Seed, stats, spawnChance);
            })
            .ToArray();

        const string mtOddsSummary = "Multi-tool details are unavailable.";
        const string mtExplanation = "The mobile game tables needed for cabinet pools are not installed. A pool also does not identify the tool sold by a particular cabinet.";
        GeneratedMultiToolLoadPool[] mtPools = [];

        return new PortalSystemReadout(
            normalizedCode,
            codeValue,
            packedAddress,
            clampedGalaxy,
            galaxyName,
            galaxyClass,
            dialedBody,
            systemName,
            regionName,
            coords,
            distToCore,
            requiresPurple,
            attrs,
            starPill,
            racePill,
            economyWealthPill,
            conflictPill,
            compactSummary,
            glyphVectors,
            bodyList,
            shipRulesSummary,
            orderedShips,
            mtOddsSummary,
            mtExplanation,
            mtPools);
    }

}
