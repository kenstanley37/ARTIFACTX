using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>What the Planet Finder's planet filters read for one body: the player's record where they took one, else
/// the game's generator (<see cref="PlanetTypesService.Basics"/>).</summary>
/// <param name="Weather"><see cref="PlanetFinderFactsService.Calm"/>, <see cref="PlanetFinderFactsService.Stormy"/> or
/// <see cref="PlanetFinderFactsService.Extreme"/>; "" when unknown.</param>
/// <param name="Sentinels">"Low", "Normal", "Aggressive" or "Corrupted" (or a recorded word with no known level).</param>
/// <param name="Flora">"None", "Low", "Medium" or "High"; Fauna likewise.</param>
/// <param name="Resources">The three mined resources' names, or what the player recorded.</param>
public sealed record PlanetFinderFacts(bool Infested, string Weather, string Sentinels, string Flora, string Fauna, string[] Resources)
{
    /// <summary>A base-building planet: calm weather, low sentinels, and both plants and animals.</summary>
    public bool Paradise => Weather == PlanetFinderFactsService.Calm && Sentinels == "Low"
        && Flora is not ("" or "None") && Fauna is not ("" or "None");

    /// <summary>"Calm weather · Low sentinels · Flora: High · Fauna: Medium · Copper, Sodium, Gold" for a card.</summary>
    public string Summary => string.Join(" · ", new[]
    {
        Weather.Length > 0 ? $"{Weather} weather" : "",
        Sentinels.Length > 0 ? $"{Sentinels} sentinels" : "",
        Flora.Length > 0 ? $"Flora: {Flora}" : "",
        Fauna.Length > 0 ? $"Fauna: {Fauna}" : "",
        string.Join(", ", Resources),
    }.Where(s => s.Length > 0));
}

/// <summary>
/// The Planet Finder's per-planet facts (<see cref="PlanetFinderFacts"/>) for every planet it lists, generated and
/// discovered alike, so the weather, sentinel, life and resource filters work on planets nobody recorded (2026-11-14,
/// the owner: players look for a calm paradise planet to build a main base on). Generated values come from
/// PlanetProfileGenerator.Basics, which matched the full profile on all 2,796 bodies of the Slot 8 region (build
/// 185034, 2026-11-14; the profile's weather and sentinels matched the game's own code on 807 of 808 planets).
/// </summary>
public static class PlanetFinderFactsService
{
    public const string Calm = "Calm", Stormy = "Stormy", Extreme = "Extreme";

    /// <summary>The weather words in the order the filter lists them.</summary>
    public static readonly string[] WeatherOrder = [Calm, Stormy, Extreme];
    public static readonly string[] SentinelOrder = ["Low", "Normal", "Aggressive", "Corrupted"];
    public static readonly string[] LifeOrder = ["None", "Low", "Medium", "High"];

    /// <summary>The facts for each planet (by system and planet number). Call off the UI thread: it works out every
    /// listed system's planets.</summary>
    public static Dictionary<(ulong System, int Planet), PlanetFinderFacts> Build(IReadOnlyList<KnownPlanet> planets,
        IReadOnlyList<(string Word, string Group, string Tier)> words, IReadOnlyDictionary<string, string> itemNames)
    {
        var result = new Dictionary<(ulong, int), PlanetFinderFacts>(planets.Count);
        foreach (var p in planets)
        {
            var key = (p.SystemAddress, p.PlanetIndex);
            if (result.ContainsKey(key)) continue;
            int i = p.PlanetIndex - 1;
            var types = PlanetTypesService.For(p.SystemAddress);
            var basics = PlanetTypesService.Basics(p.SystemAddress);
            PlanetBasics? b = i >= 0 && i < basics.Length ? basics[i] : null;
            bool infested = i >= 0 && i < types.Length && types[i].SubType == NmsPlanets.InfestedSubType;
            var d = PlanetDetailsService.Get(p.SystemAddress, p.PlanetIndex);
            string Recorded(string group, string? word) => string.IsNullOrEmpty(word) ? "" : PlanetText.LevelOf(words, group, word);

            string sentinels = Recorded("Sentinels", d?.Sentinels) is { Length: > 0 } rs ? (rs == "Medium" ? "Normal" : rs)
                : b is { Sentinels.Length: > NormalDifficulty } ? SentinelOrder[System.Math.Clamp(b.Sentinels[NormalDifficulty], 0, 3)] : "";
            string flora = Recorded("Life", d?.Flora) is { Length: > 0 } rf ? rf : b is null ? "" : LifeOrder[System.Math.Clamp(b.Flora, 0, 3)];
            string fauna = Recorded("Life", d?.Fauna) is { Length: > 0 } ra ? ra : b is null ? "" : LifeOrder[System.Math.Clamp(b.Fauna, 0, 3)];
            string[] resources = d?.Resources is { Count: > 0 } recorded ? [.. recorded]
                : b is null ? [] : [.. new[] { b.Common, b.Uncommon, b.Rare }.Where(id => id.Length > 0).Select(id => PlanetText.ItemName(id, itemNames))];
            // The generator's weather alone: it matched the game on 807 of 808 planets, and recorded weather words
            // have no calm/stormy/extreme level to read.
            string weather = b is null ? "" : b.Extreme ? Extreme : b.Storms > 0 ? Stormy : Calm;
            result[key] = new PlanetFinderFacts(infested, weather, sentinels, flora, fauna, resources);
        }
        return result;
    }

    /// <summary>GcCombatTimerDifficultyOption Normal: the sentinel slot the game uses unless the save's difficulty is changed.</summary>
    private const int NormalDifficulty = 2;
}
