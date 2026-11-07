using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Loads one value out of a save's JSON by streaming past everything else,
/// instead of building the whole save as a JObject. A whole save is ~10x its
/// JSON size as a JObject tree; reading the other slots' discovery lists that
/// way (Settlement materials, Near Core) added ~1 GB of private memory
/// (2026-10-21).
/// </summary>
public static class NmsJsonStream
{
    /// <summary>The token at the given property path (e.g. "fDu", "ETO", "OsQ", "?fB"),
    /// or null when any step is missing.</summary>
    public static JToken? ReadAt(string json, params string[] path)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        if (!reader.Read()) return null;

        foreach (string step in path)
        {
            if (reader.TokenType != JsonToken.StartObject || !MoveToProperty(reader, step)) return null;
            if (!reader.Read()) return null;
        }
        return JToken.ReadFrom(reader);
    }

    /// <summary>Advances through the object the reader is on until the named
    /// property, skipping the values of the others.</summary>
    private static bool MoveToProperty(JsonTextReader reader, string name)
    {
        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            if ((string)reader.Value! == name) return true;
            reader.Read();
            reader.Skip();
        }
        return false;
    }
}
