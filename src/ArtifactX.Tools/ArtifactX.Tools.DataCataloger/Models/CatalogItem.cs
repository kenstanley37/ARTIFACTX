namespace ArtifactX.Tools.DataCataloger.Models;

public class CatalogItem
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public CatalogCategory? Category { get; set; }

    /// <summary>The row's own game ID, e.g. "CASING". This is what save-game data references.</summary>
    public string GameId { get; set; } = "";

    /// <summary>Loc key for the primary display name, e.g. "CASING_NAME" (null if none found).</summary>
    public string? NameLocKey { get; set; }

    /// <summary>Resolved English text for NameLocKey, cached here for zero-join lookups.</summary>
    public string? NameEnglish { get; set; }

    /// <summary>Loc key for the short/lowercase variant, e.g. "CASING_NAME_L".</summary>
    public string? NameLowerLocKey { get; set; }
    public string? NameLowerEnglish { get; set; }

    /// <summary>Loc key for description text, if present.</summary>
    public string? DescriptionLocKey { get; set; }
    public string? DescriptionEnglish { get; set; }

    /// <summary>Procedural upgrade-module rows (GcProceduralTechnologyTable) only -
    /// the row's raw "Template" family stem, e.g. "T_SCAN". CatalogBuildService
    /// strips the "T_" prefix and matches it against GcTechnologyTable to borrow
    /// the real base technology's icon and name (these rows have none of their
    /// own) - confirmed the right link by extracting and visually comparing
    /// icon PNGs; the row's own "Group" field pointed at a real but visually
    /// wrong item for at least one family.</summary>
    public string? TemplateId { get; set; }

    /// <summary>Equipment-slot category for row types that have one - currently only
    /// GcTechnology (Suit/Weapon/Ship/Freighter/Exocraft/Mech/.../All/None), extracted
    /// generically in CatalogClassifier by field shape, not hardcoded to that type.
    /// Null for row types with no such field, e.g. Product/Substance.</summary>
    public string? UsageCategory { get; set; }

    /// <summary>Real inventory stack cap (GcProductData/GcRealitySubstanceData's
    /// "StackMultiplier"), e.g. 20 for Metal Plating vs 9999 for common resources.
    /// Null for row types with no such field (Technology has no stack concept).</summary>
    public int? MaxStackSize { get; set; }

    /// <summary>ShipCapacity rows only - one Technology or Cargo max-slot count for
    /// a single (ship type, class letter) pair, straight from the game's own
    /// GcInventoryTable.ShipInventoryMaxUpgradeSize. GameId encodes which:
    /// "{SHIPTYPE}_{CLASSLETTER}_CARGO" / "..._TECH". Kept as its own field
    /// rather than reusing MaxStackSize, which means something different
    /// (real inventory stack cap) for every other row type.</summary>
    public int? CapacityValue { get; set; }

    /// <summary>The reference-tool-style Catalog top-level grouping (e.g. "Raw Materials",
    /// "Crafted Products", "Constructed Technology", "Equipment") - distinct from
    /// UsageCategory, which means something different per row type (equipment slot for
    /// Technology, crafting-ingredient tag for Product/Substance). For GcProductData rows
    /// this is the row's own WikiCategory enum, mapped to its display name (rows whose
    /// WikiCategory is "NotEnabled" - not shown on the in-game Guide/Catalog screen at all -
    /// are left null here). GcTechnology rows map wholesale to "Equipment" and
    /// GcRealitySubstanceData rows map wholesale to "Raw Materials", since neither row type
    /// has an equivalent sub-category field of its own (confirmed by inspecting both classes
    /// directly - only a "WikiEnabled" bool each). Null for every other row type.</summary>
    public string? CatalogGroup { get; set; }

    public List<IconAsset> Icons { get; set; } = new();
}