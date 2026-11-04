using System.Text.Json;
using ArtifactX.Core.Companions;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>The sequencer screen's words: slot names and subtitles (S, A, C, P
    /// = Growth Hormone, Gene Splitter, Dye Injector, Neural Calibrator), the
    /// Embryo Status labels and the two words each personality trait shows by
    /// its sign (picked at 0x1407875f4).</summary>
    private static readonly string[] EggScreenTextKeys =
    [
        "UI_EGG_MODIFIER_NAME_L", "UI_EGG_MODIFIER_INPUT_HEADING", "UI_EGG_MODIFIER_OUTPUT_HEADING",
        "UI_EGG_MODIFIER_INPUT_S", "UI_EGG_MODIFIER_INPUT_S_SUB", "UI_EGG_MODIFIER_INPUT_A", "UI_EGG_MODIFIER_INPUT_A_SUB",
        "UI_EGG_MODIFIER_INPUT_C", "UI_EGG_MODIFIER_INPUT_C_SUB", "UI_EGG_MODIFIER_INPUT_P", "UI_EGG_MODIFIER_INPUT_P_SUB",
        "WEIGHT_HEIGHT", "UI_EGG_MODIFIER_OUT_A", "UI_EGG_MODIFIER_OUT_C", "UI_EGG_MODIFIER_OUT_P",
        "UI_EGG_MODIFIER_OUT_INHERITED", "UI_EGG_MODIFIER_OUT_INCREASING", "UI_EGG_MODIFIER_OUT_DECREASING", "UI_EGG_MODIFIER_OUT_UNSTABLE",
        "UI_PET_HELPFUL_RATING", "UI_PET_PLAYFUL_RATING", "UI_PET_AGGRESSION_RATING", "UI_PET_GENTLENESS_RATING",
        "UI_PET_INDEPENDENCE_RATING", "UI_PET_ATTACHMENT_RATING",
    ];

    /// <summary>
    /// The Egg Sequencer's tuning (format 66, ArtifactX.Core EggSequencer): the
    /// PetEgg* values of GcCreatureGlobals and the item overrides of
    /// peteggtraitmodifieroverridetable.mbin (24 rows in build 184271: Ferrite
    /// Dust raises Helpfulness, Sac Venom raises Aggression...). Written as one
    /// row, GameId EGGSEQUENCER, holding the rules as JSON.
    /// </summary>
    public static void AddEggSequencer(string catalogPath, string pcbanksPath)
    {
        const string tableFile = "METADATA/SIMULATION/ECOSYSTEM/PETEGGTRAITMODIFIEROVERRIDETABLE.MBIN";
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        var extraction = new ExtractionService();
        NMSTemplate? Decode(string file)
        {
            if (!index.TryFind(file, out var pakPath, out var entry, out var header)) return null;
            using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pakPath, entry, header)!));
            mbin.Load();
            return mbin.GetData();
        }
        if (Decode("GCCREATUREGLOBALS.MBIN") is not GcCreatureGlobals globals)
            throw new InvalidDataException("Creature globals not found or have an unexpected structure.");
        if (Decode(tableFile) is not GcPetEggTraitModifierOverrideTable table)
            throw new InvalidDataException($"Egg trait table not found or has an unexpected structure: {tableFile}");
        if (Decode("METADATA/REALITY/TABLES/NMS_REALITY_GCPRODUCTTABLE.MBIN") is not GcProductTable products
            || Decode("METADATA/REALITY/TABLES/NMS_REALITY_GCSUBSTANCETABLE.MBIN") is not GcSubstanceTable substances)
            throw new InvalidDataException("Product or substance table not found.");
        var ingredients = products.Table.Where(p => p.EggModifierIngredient).Select(p => p.ID.Value)
            .Concat(substances.Table.Where(s => s.EggModifierIngredient).Select(s => s.ID.Value)).ToList();

        var overrides = table.TraitModifiers
            .Select(m => (Id: m.ProductID.Value.Length > 0 ? m.ProductID.Value : m.SubstanceID.Value, m))
            .Where(o => o.Id.Length > 0)
            .Select(o => new EggTraitOverride(o.Id, (EggTrait)(int)o.m.Trait.PetTrait, o.m.IncreasesTrait))
            .ToList();
        var rules = new EggSequencerRules(overrides, globals.PetEggMaxChangeProduct.Value,
            globals.PetEggSubstanceModifier, globals.PetEggTraitRangeModifier, globals.PetEggTraitRangeMax,
            globals.PetEggScaleRangeModifier, globals.PetEggScaleRangeMax,
            globals.PetEggAccessoryChanceModifier, globals.PetEggMaxAccessoriesChangeChance,
            globals.PetEggColourChanceModifier, globals.PetEggMaxColourChangeChance,
            globals.PetEggOverdosageModifier, globals.PetEggMaxOverdosage, globals.PetEggMaxTopDescriptorChangeChance,
            globals.PetEggModificationItemLimit, ingredients, EnglishTexts(index, extraction, EggScreenTextKeys.ToHashSet()));

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "EggSequencer", "GcPetEggTraitModifierOverrideTable", tableFile.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "EGGSEQUENCER", NameEnglish = "Egg Sequencer rules",
                DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote the Egg Sequencer rules ({overrides.Count} item overrides, {ingredients.Count} ingredients) to {catalogPath}.");
    }
}
