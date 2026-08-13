using System.Reflection;
using libMBIN;

namespace ArtifactX.Tools.DataCataloger.Services;

public class ClassifiedRow
{
    public string GameId { get; set; } = "";
    public string? NameLocKey { get; set; }
    public string? NameLowerLocKey { get; set; }
    public string? DescriptionLocKey { get; set; }

    /// <summary>GcProceduralTechnologyData rows (Sigma/Tau/Theta-style upgrade
    /// modules - Scanner/Mining/Hazard upgrades etc.) have no icon of their own
    /// anywhere in that table - confirmed by decoding the real table directly.
    /// This raw "Template" field value (e.g. "T_SCAN", "T_LASER") is a family
    /// stem CatalogBuildService uses to find the real base technology in
    /// GcTechnologyTable to borrow its icon AND name from: strip the "T_"
    /// prefix, then the SHORTEST GcTechnologyTable GameId starting with that
    /// stem is the right one - confirmed against real data for two families
    /// (T_SCAN -> SCAN1 "Scanner", not the longer SCANBINOC1 "Analysis Visor";
    /// T_LASER -> LASER "Mining Beam", not the longer LASER_XO). Deliberately
    /// NOT the row's own "Group" field (a name-only loc-key reference that,
    /// for the Scan family, pointed at Analysis Visor - a real but visually
    /// wrong icon, confirmed by comparing the extracted PNGs directly; Group
    /// and Template can point at two different, only loosely related base
    /// techs). Null for row types with no "Template" field.</summary>
    public string? TemplateId { get; set; }

    /// <summary>The row's equipment-slot category, when it has one - e.g. GcTechnology's
    /// "Category" field (Suit/Weapon/Ship/Freighter/Exocraft/All/etc.) is the only thing
    /// that actually distinguishes Exosuit tech from Starship/Multi-tool tech; GcTechnologyTable
    /// itself is one flat list covering every equipment type. Null for row types with no such
    /// field (Product/Substance don't have one).</summary>
    public string? UsageCategory { get; set; }

    /// <summary>The row's real inventory stack cap, from GcProductData/GcRealitySubstanceData's
    /// "StackMultiplier" int field - e.g. 20 for Metal Plating vs 9999 for common resources.
    /// Null for row types with no such field (Technology tracks durability, not a stack, via
    /// its own 1o9/F9q fields already handled elsewhere).</summary>
    public int? MaxStackSize { get; set; }

    /// <summary>GcProductData's own "WikiCategory" enum field (Crafting/Tech/Construction/
    /// Trade/Curio/Cooking/NotEnabled) - the same grouping the in-game Guide/Catalog screen
    /// uses to bucket Products into "Crafted Products"/"Constructed Technology"/etc. Distinct
    /// from the "Category" field above (GcProductData ALSO has a plain "Category" field of
    /// type GcRealitySubstanceCategory - a crafting-ingredient-type tag like Metal/Flora/
    /// Exotic, unrelated to this). Raw enum name stored here (e.g. "Crafting"); CatalogBuildService
    /// maps it to the display group name. Null for row types with no such field (Technology/
    /// Substance don't have one - those map to their group wholesale instead, see
    /// CatalogBuildService's CatalogGroup assignment).</summary>
    public string? WikiCategory { get; set; }

    /// <summary>(field name on the row, e.g. "Icon"/"HeroIcon", DDS/PNG path referenced)</summary>
    public List<(string SourceField, string TexturePath)> Icons { get; set; } = new();
}

public static class CatalogClassifier
{
    /// <summary>
    /// A decoded MBIN is treated as a catalog table if it has a public List&lt;T&gt; OR T[]
    /// field whose element type T is itself an NMSTemplate-derived class with an "ID"/"Id"
    /// field. This matches the "Table" field convention Hello Games has used consistently
    /// across GcProductTable / GcSubstanceTable / GcTechnologyTable / etc. - some tables
    /// (mostly procedural-generation ones, e.g. GcProceduralProductTable) use a plain array
    /// instead of List&lt;T&gt;, so both shapes are checked. This does NOT hardcode specific
    /// table names, so new tables added by future game updates are picked up automatically.
    /// </summary>
    public static bool TryClassify(NMSTemplate template, out FieldInfo listField, out Type rowType)
    {
        foreach (var field in template.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            Type? elementType = null;

            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = field.FieldType.GetGenericArguments()[0];
            }
            else if (field.FieldType.IsArray)
            {
                elementType = field.FieldType.GetElementType();
            }

            if (elementType == null || !typeof(NMSTemplate).IsAssignableFrom(elementType))
                continue;

            bool hasIdField = elementType.GetField("ID") != null || elementType.GetField("Id") != null;
            if (!hasIdField)
                continue;

            listField = field;
            rowType = elementType;
            return true;
        }

        listField = null!;
        rowType = null!;
        return false;
    }

    public static List<ClassifiedRow> ExtractRows(NMSTemplate template, FieldInfo listField)
    {
        var rows = new List<ClassifiedRow>();

        if (listField.GetValue(template) is not System.Collections.IEnumerable list)
            return rows;

        foreach (var rowObj in list)
        {
            if (rowObj != null)
                rows.Add(ExtractRow(rowObj));
        }

        return rows;
    }

    private static ClassifiedRow ExtractRow(object rowObj)
    {
        var type = rowObj.GetType();
        var row = new ClassifiedRow();

        var idField = type.GetField("ID") ?? type.GetField("Id");
        row.GameId = idField != null ? ReflectionUtil.ExtractString(idField.GetValue(rowObj)) ?? "" : "";

        // A field is a CANDIDATE by field name ("Name"/"NameLower"/"Description") - this
        // stays name-agnostic across row types on purpose. But candidacy alone isn't
        // enough: some engine-internal fields (animation socket tags, aim-pose IDs) also
        // have "Name" in their C# field name while holding short internal tags like "EYE"
        // or "AimGL", not display keys. Every genuine loc key we've observed in this data
        // consistently ends in _NAME / _NAME_L / _DESC / _DESCRIPTION, so that shape check
        // is what actually confirms "this value is a loc key", independent of whether we
        // currently have it loaded in the English dictionary.
        string? bestName = null, bestNameLower = null, bestDescription = null, template = null;

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            string? value = ReflectionUtil.ExtractString(field.GetValue(rowObj));
            if (string.IsNullOrEmpty(value))
                continue;

            string fieldName = field.Name.ToLowerInvariant();

            if (fieldName.Contains("namelower"))
            {
                if (value.EndsWith("_NAME_L", StringComparison.OrdinalIgnoreCase))
                    bestNameLower ??= value;
            }
            else if (fieldName.Contains("name"))
            {
                if (value.EndsWith("_NAME", StringComparison.OrdinalIgnoreCase))
                    bestName ??= value;
            }
            else if (fieldName.Contains("desc"))
            {
                if (value.EndsWith("_DESC", StringComparison.OrdinalIgnoreCase) ||
                    value.EndsWith("_DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                    bestDescription ??= value;
            }
            else if (fieldName == "template")
            {
                template ??= value;
            }
        }

        row.TemplateId = template;
        row.NameLocKey = bestName;
        row.NameLowerLocKey = bestNameLower;
        row.DescriptionLocKey = bestDescription;

        // Equipment-slot category: a field literally named "Category" whose value is
        // itself an NMSTemplate wrapping a single enum field (GcTechnologyCategory's
        // shape - one enum field, no other data). Read by shape rather than checking
        // for "GcTechnology" by name, so any other row type following the same
        // Category-wraps-an-enum convention picks this up automatically.
        var categoryField = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "Category");

        if (categoryField?.GetValue(rowObj) is { } categoryValue)
        {
            var enumField = categoryValue.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => f.FieldType.IsEnum);

            row.UsageCategory = enumField?.GetValue(categoryValue)?.ToString();
        }

        // Stack cap: GcProductData/GcRealitySubstanceData both expose a plain int
        // "StackMultiplier" field with no wrapper - simpler shape than Category above,
        // just a direct name+type match.
        var stackField = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "StackMultiplier" && f.FieldType == typeof(int));

        if (stackField != null)
            row.MaxStackSize = (int)stackField.GetValue(rowObj)!;

        // Wiki category: GcProductData's "WikiCategory" field is a plain enum, not
        // wrapped in its own NMSTemplate type like Category/StackMultiplier's siblings
        // above - direct name+enum-type match.
        var wikiCategoryField = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "WikiCategory" && f.FieldType.IsEnum);

        if (wikiCategoryField != null)
            row.WikiCategory = wikiCategoryField.GetValue(rowObj)?.ToString();

        // Icon-shaped fields: any nested object exposing a "Filename" field (typically
        // TkTextureResource.Filename, itself a GcFilename - another Value-wrapped type,
        // not a plain string) pointing at an image file.
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var val = field.GetValue(rowObj);
            if (val == null) continue;

            var filenameField = val.GetType().GetField("Filename");
            if (filenameField == null) continue;

            string? path = ReflectionUtil.ExtractString(filenameField.GetValue(val));
            if (!string.IsNullOrEmpty(path) &&
                (path.EndsWith(".DDS", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".PNG", StringComparison.OrdinalIgnoreCase)))
            {
                row.Icons.Add((field.Name, path));
            }
        }

        return row;
    }
}