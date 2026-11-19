namespace ArtifactX.Core.Universe;

/// <summary>
/// A generated planet's profile in plain words, shared by the desktop's Planet Details and the phone's System Details:
/// weather, sentinels, flora, fauna and what the scanner lists. Moved from the desktop's PlanetText (2026-11-18) so
/// both apps say the same thing.
/// </summary>
public static class PlanetProfileText
{
    /// <summary>Weather types in GcWeatherOptions order, worded plainly (the game picks one
    /// of several flowery words per type - "Frost", "Icy Blasts" - in its text step).</summary>
    private static readonly string[] WeatherTypes =
    [
        "Clear", "Dusty", "Humid", "Snowy", "Toxic", "Scorching", "Radioactive", "Exotic", "Exotic", "Exotic",
        "Swampy", "Volcanic", "Bubbling", "Anomalous", "Burning", "Cold", "Gas giant",
    ];

    /// <summary>The game's weather word families (PlanetProfileGenerator.WeatherWordFamily), worded plainly.
    /// Waterworlds have their own words for every kind of weather, so they keep the type's word.</summary>
    private static string? FamilyWord(string family) => family switch
    {
        "LUSH" => "Humid",
        "SWAMP" => "Swampy",
        "TOXIC" => "Toxic",
        "RADIO" => "Radioactive",
        "COLD" => "Snowy",
        "HEAT" => "Scorching",
        "BARREN" => "Dusty",
        "DEAD" => "Clear",
        "GLITCH" => "Anomalous",
        "RED" or "GREEN" or "BLUE" => "Exotic",
        "LAVA" => "Volcanic",
        "GASGIANT" => "Gas giant",
        _ => null,
    };

    /// <summary>"Extreme toxic · frequent storms". The kind is the one the game's words show, not the weather type: a
    /// lush world with corrupted sentinels has the Weird type but humid words, so <paramref name="body"/> (the planet's
    /// biome and subtype) is used when known.</summary>
    public static string Weather(PlanetProfile profile, PlanetType? body)
    {
        string? family = body is null ? null
            : FamilyWord(PlanetProfileGenerator.WeatherWordFamily(body.Biome, body.SubType, profile.WeatherType));
        string type = family ?? (profile.WeatherType >= 0 && profile.WeatherType < WeatherTypes.Length ? WeatherTypes[profile.WeatherType] : "Unknown");
        string weather = profile.Extreme ? $"Extreme {type.ToLowerInvariant()}" : type;
        return profile.Storms switch
        {
            1 => $"{weather} · occasional storms",
            2 => $"{weather} · frequent storms",
            _ => weather,
        };
    }

    /// <summary>GcCombatTimerDifficultyOption Normal - the slot the game uses unless the save's
    /// sentinel difficulty is changed.</summary>
    private const int NormalDifficulty = 2;

    /// <summary>The sentinel level at normal difficulty, worded like the game's Discoveries
    /// quick view: nothing shown there is "Low", level 1 is "High Sentinel Activity" (its
    /// scanner words "Frequent", "Observant"...), 2 "Aggressive Sentinels", 3 the dissonant,
    /// corrupted kind (checked against 800 recorded words, 2026-10-22).</summary>
    public static string Sentinels(PlanetProfile profile) =>
        (profile.Sentinels.Length > NormalDifficulty ? profile.Sentinels[NormalDifficulty] : 1) switch
        {
            0 => "Low",
            2 => "Aggressive",
            3 => "Corrupted",
            _ => "High",
        };

    private static string Level(int level) => level switch
    {
        1 => "Low",
        2 => "Medium",
        3 => "High",
        _ => "None",
    };

    /// <summary>The flora level plainly worded ("None", "Low", "Medium", "High").</summary>
    public static string Flora(PlanetProfile profile) => Level(profile.Flora);

    /// <summary>The fauna summary ("None", "1 species", "N species", or by life level).</summary>
    public static string Fauna(PlanetProfile profile) => profile.FaunaCount is int count
        ? count == 0 ? "None" : count == 1 ? "1 species" : $"{count} species"
        : Level(profile.Fauna);

    /// <summary>What the scanner lists, in its order: the planet's plant, the three mined resources (most common
    /// first), then other hints ("Cactus Flesh, Copper, Pyrite, Salt"). <paramref name="itemName"/> gives an item's
    /// display name or null for a hint that isn't an item, which is named from <paramref name="hintNames"/>.</summary>
    public static IReadOnlyList<string> Resources(PlanetProfile profile, Func<string, string?> itemName,
        IReadOnlyDictionary<string, string>? hintNames)
    {
        string Hint(string id) => hintNames is not null && hintNames.TryGetValue(id, out var name) ? name : itemName(id) ?? id;
        var plants = profile.Hints.Where(h => itemName(h) is not null).Select(h => itemName(h)!);
        var mined = new[] { profile.Common, profile.Uncommon, profile.Rare }.Where(id => id.Length > 0).Select(id => itemName(id) ?? id);
        var others = profile.Hints.Where(h => itemName(h) is null).Select(Hint);
        return plants.Concat(mined).Concat(others).Distinct().ToList();
    }
}
