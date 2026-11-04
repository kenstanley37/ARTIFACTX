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
            globals.PetEggModificationItemLimit);

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "EggSequencer", "GcPetEggTraitModifierOverrideTable", tableFile.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "EGGSEQUENCER", NameEnglish = "Egg Sequencer rules",
                DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote the Egg Sequencer rules ({overrides.Count} item overrides) to {catalogPath}.");
    }
}
