using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace ArtifactX.Core.NmsModels;

/// <summary>What Save Selection shows for one save file.</summary>
public sealed record NmsSaveSummary(
    string? SaveName,
    string? SaveType,
    int PlayTimeSeconds,
    int GalaxyIndex,
    string GameMode,
    string? PlayerName);

/// <summary>
/// Reads a save's summary by streaming through its JSON and loading only the
/// small parts it needs - header (&lt;h0), save type (XTp), F2P, galaxy (vLc.idA),
/// difficulty (vLc.6f=.LyC), stat groups (vLc.6f=.gUR) and the player's own
/// discoveries (fDu.ETO.OsQ.?fB, one record at a time) - and skipping the rest.
/// Building the whole save as a JObject (up to 12 MB of JSON) took ~0.6 s a
/// file and didn't get faster in parallel; this is what makes Save Selection
/// quick with several accounts (2026-10-12).
/// </summary>
public static class NmsSaveSummaryReader
{
    public static NmsSaveSummary Read(string json)
    {
        string? saveName = null, saveType = null, gameMode = null, playerName = null;
        int f2p = 0, galaxy = -1, totalTime = 0;
        long newestDiscovery = long.MinValue;

        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonException("Not a save file.");

        ForEachProperty(reader, name =>
        {
            switch (name)
            {
                case "F2P":
                    reader.Read();
                    f2p = reader.TokenType == JsonToken.Integer ? Convert.ToInt32(reader.Value) : 0;
                    break;
                case "XTp":
                    reader.Read();
                    saveType = reader.Value as string;
                    break;
                case "<h0":
                    reader.Read();
                    saveName = JToken.ReadFrom(reader).ToObject<NmsSaveHeader>()?.SaveName;
                    break;
                case "vLc":
                    reader.Read();
                    if (reader.TokenType != JsonToken.StartObject) { reader.Skip(); break; }
                    ForEachProperty(reader, universe =>
                    {
                        switch (universe)
                        {
                            case "idA":
                                reader.Read();
                                galaxy = reader.TokenType == JsonToken.Integer ? Convert.ToInt32(reader.Value) : -1;
                                break;
                            case "6f=":
                                reader.Read();
                                if (reader.TokenType != JsonToken.StartObject) { reader.Skip(); break; }
                                ForEachProperty(reader, player =>
                                {
                                    reader.Read();
                                    if (player == "LyC") gameMode = JToken.ReadFrom(reader).ToObject<NmsDifficultySettings>()?.GameMode;
                                    else if (player == "gUR") totalTime = TotalPlayTime(JToken.ReadFrom(reader) as JArray);
                                    else reader.Skip();
                                });
                                break;
                            default:
                                reader.Read();
                                reader.Skip();
                                break;
                        }
                    });
                    break;
                case "fDu":
                    reader.Read();
                    ReadDiscoveries(reader, ref playerName, ref newestDiscovery);
                    break;
                default:
                    reader.Read();
                    reader.Skip();
                    break;
            }
        });

        return new NmsSaveSummary(saveName, saveType, totalTime > 0 ? totalTime : f2p, galaxy, gameMode ?? "Unknown", playerName);
    }

    /// <summary>Calls onProperty with each property name of the object the
    /// reader is on; onProperty must consume the property's value.</summary>
    private static void ForEachProperty(JsonTextReader reader, Action<string> onProperty)
    {
        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            onProperty((string)reader.Value!);
    }

    /// <summary>fDu.ETO.OsQ.?fB, one discovery record at a time: the player's
    /// own name is the discoverer name (ksu V?:) on their newest own record -
    /// one with an owner id (f5Q); downloaded records have none (NmsPlanets).</summary>
    private static void ReadDiscoveries(JsonTextReader reader, ref string? name, ref long newest)
    {
        if (reader.TokenType != JsonToken.StartObject) { reader.Skip(); return; }
        string? found = name;
        long best = newest;
        ForEachProperty(reader, eto =>
        {
            reader.Read();
            if (eto != "ETO" || reader.TokenType != JsonToken.StartObject) { reader.Skip(); return; }
            ForEachProperty(reader, osq =>
            {
                reader.Read();
                if (osq != "OsQ" || reader.TokenType != JsonToken.StartObject) { reader.Skip(); return; }
                ForEachProperty(reader, list =>
                {
                    reader.Read();
                    if (list != "?fB" || reader.TokenType != JsonToken.StartArray) { reader.Skip(); return; }
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        if (JToken.ReadFrom(reader)["ksu"] is not JObject owner || string.IsNullOrEmpty(owner["f5Q"]?.Value<string>())) continue;
                        long time = owner["3I1"]?.Type == JTokenType.Integer ? owner["3I1"]!.Value<long>() : 0;
                        if (time < best || owner["V?:"]?.Value<string>() is not { Length: > 0 } who) continue;
                        best = time;
                        found = who;
                    }
                });
            });
        });
        name = found;
        newest = best;
    }

    /// <summary>GLOBAL_STATS' TIME stat (seconds) - the save's real lifetime
    /// play time; F2P reads far too small for an established character
    /// (see NmsSaveFile.PlayTimeSeconds). 0 when missing.</summary>
    private static int TotalPlayTime(JArray? groups)
    {
        if (groups is null) return 0;
        foreach (var group in groups)
        {
            string groupId = (group[":rc"]?.Value<string>() ?? "").TrimStart('^');
            if (!string.Equals(groupId, "GLOBAL_STATS", StringComparison.OrdinalIgnoreCase)) continue;
            if (group["gUR"] is not JArray stats) return 0;
            foreach (var stat in stats)
            {
                string statId = (stat["b2n"]?.Value<string>() ?? "").TrimStart('^');
                if (!string.Equals(statId, "TIME", StringComparison.OrdinalIgnoreCase)) continue;
                double? value = stat[">MX"]?["eoL"]?.Value<double?>();
                return value.HasValue ? (int)Math.Round(value.Value) : 0;
            }
            return 0;
        }
        return 0;
    }
}
