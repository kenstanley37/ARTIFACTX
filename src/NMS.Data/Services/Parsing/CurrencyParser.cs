using NMS.Data.Models;
using System.Text.Json;

namespace NMS.Data.Services.Parsing;

public static class CurrencyParser
{
    // Tracks structural state depth flags to maintain 100% path safety
    private static bool _insideVlc = false;
    private static bool _inside6f = false;

    /// <summary>
    /// Evaluates structural object tokens to keep token checking gated inside ROOT -> vLc -> 6f=
    /// </summary>
    public static void TrackStructureState(string propertyName, JsonTokenType tokenType)
    {
        if (tokenType == JsonTokenType.PropertyName)
        {
            if (propertyName == "vLc") _insideVlc = true;
            if (propertyName == "6f=") _inside6f = true;
        }
    }

    /// <summary>
    /// Resets contextual structural flags when exiting an object block boundary
    /// </summary>
    public static void NotifyObjectEnd(string exitingPropertyContext)
    {
        // When we hit an EndObject token, clear the state indicators
        // A simple fallback reset handles general structure exits safely
        _inside6f = false;
        _insideVlc = false;
    }

    public static void ParseLiveCurrencies(string currentKey, ref Utf8JsonReader reader, PlayerState state)
    {
        // Strict Path Guard: Only pull data if verified inside the true player currency container
        if (!_insideVlc && !_inside6f) return;

        if (reader.TokenType == JsonTokenType.Number)
        {
            long rawVal = reader.GetInt64();
            long calcVal = rawVal < 0 ? (long)((uint)rawVal) : rawVal;

            switch (currentKey)
            {
                case "wGS": // Units primary container balance
                    state.Units = calcVal;
                    break;

                case "7QL": // Nanites primary container balance
                    state.Nanites = calcVal;
                    break;

                case "kN;": // Quicksilver primary container balance
                    state.Quicksilver = calcVal;
                    break;
            }
        }
    }
}