using ArtifactX.Core.NmsModels;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Names for the parts a base, freighter or station is built from (the "r&lt;7" id of
/// each object in its parts list), and for loose placed objects.
///
/// Almost every part is an item in the catalog under its own id. The few that aren't
/// are pieces the game places itself, which have no name anywhere in the game files:
/// checked 2026-10-20 against GcBaseBuildingTable (basebuildingobjectstable.mbin) -
/// BRIDGECONNECTOR, STACONNECT, STATION_CORE, FREIGHTER_CORE, AIRLCKCONNECTOR and
/// BASE_ROOT have no product, no OverrideProductID in use and no SinglePartID with a
/// name. Those get a plain description of what they are; anything else unnamed is
/// spelled out from its id.
/// </summary>
public static class BasePartNames
{
    private static readonly Dictionary<string, string> BuiltIn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BRIDGECONNECTOR"] = "Freighter bridge connector",
        ["BIGGSCONNECTOR"] = "Corvette connector",
        ["AIRLCKCONNECTOR"] = "Airlock connector",
        ["STACONNECT"] = "Space station connector",
        ["STATION_CORE"] = "Space station core",
        ["FREIGHTER_CORE"] = "Freighter core",
        ["BASE_ROOT"] = "Base anchor",
        // A claimed station keeps 120 STACONNECT, one BRIDGECONNECTOR, STATION_CORE and
        // U_PARAGON after every part the game lets you remove is gone (user cleared a
        // station by hand, 2026-10-21) - U_PARAGON was missing here and showed as "U Paragon".
        // Removal test the same day (Slot 8, save edited, then played and saved in game):
        // with all four kinds removed the game put U_PARAGON back on its next save and left the
        // rest gone; with only STACONNECT removed nothing came back. Both stations worked inside
        // and out (Station Core, Manage Station, docking) and the exterior looked the same as
        // before. So 122 per station look removable - kept protected until a second test on a
        // fresh claim; U_PARAGON must always stay.
        ["U_PARAGON"] = "Station paragon",
    };

    public static bool IsBaseComputer(string id) =>
        string.Equals(CatalogService.NormalizeId(id), CatalogService.NormalizeId(NmsBuildCounts.BaseComputerId), StringComparison.OrdinalIgnoreCase);

    /// <summary>One of the pieces the game places itself (connectors, cores), not something the player built.</summary>
    public static bool IsBuiltIn(string id) => BuiltIn.ContainsKey(CatalogService.NormalizeId(id));

    /// <summary>Whether a claimed station's part can go: never the pieces the game places
    /// itself, and only real, buildable items the catalog names - a station's structure
    /// isn't all known. Warm the catalog cache for the ids first.</summary>
    public static bool CanRemoveFromStation(string id) =>
        !IsBuiltIn(id) && CatalogService.TryGet(CatalogService.NormalizeId(id))?.DisplayName is { Length: > 0 };

    /// <summary>Call CatalogService.WarmCacheAsync with the ids first, for names and icons.</summary>
    public static string Name(string id)
    {
        string norm = CatalogService.NormalizeId(id);
        if (BuiltIn.TryGetValue(norm, out var builtIn)) return builtIn;
        if (CatalogService.TryGet(norm)?.DisplayName is { Length: > 0 } name) return TitleCase(name);
        return TitleCase(norm.Replace('_', ' ').Trim());
    }

    /// <summary>A glyph for a part without a catalog icon (Segoe MDL2 Assets).</summary>
    public static string Glyph(string id)
    {
        string upper = CatalogService.NormalizeId(id).ToUpperInvariant();
        if (upper.Contains("LIGHT") || upper.Contains("LAMP")) return "\uEA80"; // Lightbulb
        if (upper.Contains("CONNECT") || upper.Contains("PIPE") || upper.Contains("WIRE")) return "\uE71B"; // Link
        return "\uE80F"; // Home
    }

    // Catalog names are mostly upper case ("BASE COMPUTER"); mixed-case names are left as they are.
    private static string TitleCase(string s) =>
        s.Length == 0 || HasLower(s) ? s : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    private static bool HasLower(string s)
    {
        foreach (char c in s) if (char.IsLower(c)) return true;
        return false;
    }
}
