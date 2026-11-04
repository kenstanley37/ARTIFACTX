using System.Text.Json;
using ArtifactX.Core.Procedural;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// How each procedural product is named from its seed (format 65,
    /// ArtifactX.Core ProceduralProductNames): its name templates, legacy rolls,
    /// rarity drop weights and per-placeholder words, read from
    /// proceduralproducttable.mbin and the player's English text. The game looks
    /// a product's table entry up by category (0x140edf235: category * 0x540),
    /// so an entry's category is its position. Only the categories that reach
    /// the name routine 0x140eb1d10 through the general branch are kept; the
    /// freighter logs and tech, DismantleData and MessageInBottle have their
    /// own branches, not read yet. Word counts are the numbered variants
    /// ("&lt;word&gt;_1"...) with no gap; the table's own NumOptions are all 0 and
    /// filled by the game at load.
    /// </summary>
    public static void AddProceduralProductNames(string catalogPath, string pcbanksPath)
    {
        const string tableFile = "METADATA/REALITY/TABLES/PROCEDURALPRODUCTTABLE.MBIN";
        var reader = new PakReaderService();
        var index = new GlobalFileIndexService();
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (pak.FullPath is not string path) continue;
            try { index.Add(path, reader.ReadHeader(path), reader.Read(path)); }
            catch (IOException) { /* Match the catalog's treatment of unreadable PAKs. */ }
        }
        var extraction = new ExtractionService();
        if (!index.TryFind(tableFile, out var pakPath, out var entry, out var header))
            throw new InvalidDataException($"Procedural product table not found: {tableFile}");
        using var mbin = new MBINFile(new MemoryStream(extraction.ExtractEntryBytes(pakPath, entry, header)!));
        mbin.Load();
        if (mbin.GetData() is not GcProceduralProductTable table)
            throw new InvalidDataException("Procedural product table has an unexpected structure.");

        var general = new HashSet<string>(StringComparer.Ordinal)
        {
            "Loot", "Document", "BioSample", "Fossil", "Plant", "Tool", "Farm", "SeaLoot", "SeaHorror",
            "Salvage", "Bones", "SpaceHorror", "SpaceBones", "DismantleBio", "DismantleTech", "ExhibitFossil",
        };
        var categories = Enum.GetNames<GcProceduralProductCategory.ProceduralProductCategoryEnum>();
        var products = table.Table.Select((p, i) => (Data: p, Category: i < categories.Length ? categories[i] : ""))
            .Where(p => general.Contains(p.Category) && p.Data.Product.ID.Value.Length > 0).ToList();

        IEnumerable<string> WordsOf(GcProceduralProductData p) => p.NameGeneratorWordList
            .SelectMany(w => new[] { w.Word.Word.Value, w.UncommonWord.Word.Value, w.RareWord.Word.Value })
            .Append(p.NameGeneratorBase.Word.Value).Where(w => w.Length > 0);
        var words = products.SelectMany(p => WordsOf(p.Data)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The game's lists hold fewer than 100 variants; 300 leaves room.
        var keys = words.SelectMany(w => Enumerable.Range(1, 300).Select(i => $"{w}_{i}").Append(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var text = EnglishTexts(index, extraction, keys);

        IReadOnlyList<string> Variants(string word)
        {
            if (word.Length == 0) return [];
            var list = Enumerable.Range(1, 300).Select(i => text.GetValueOrDefault($"{word}_{i}"))
                .TakeWhile(t => t is not null).Select(t => t!).ToList();
            // No variants: the game uses the word itself.
            return list.Count > 0 ? list : text.TryGetValue(word, out var bare) ? [bare] : [];
        }

        var naming = products.Select(p => new ProceduralProductNaming(
            p.Data.Product.ID.Value,
            p.Data.NameGeneratorLegacyRolls,
            p.Data.ProceduralData.Select(d => d.DropWeight).ToArray(),
            Variants(p.Data.NameGeneratorBase.Word.Value),
            p.Data.NameGeneratorWordList.Select(w => new ProceduralNameWord(w.ReplaceKey.Value,
                Variants(w.Word.Word.Value), Variants(w.UncommonWord.Word.Value), Variants(w.RareWord.Word.Value))).ToList()))
            .Where(n => n.Formats.Count > 0).ToList();
        foreach (var p in products.Where(p => naming.All(n => n.ProductId != p.Data.Product.ID.Value)))
            ConsoleStyle.Warning($"{p.Data.Product.ID.Value}: no name templates in the English text - skipped.");

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "ProceduralProductNames", "GcProceduralProductData", tableFile.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "PROCEDURALPRODUCTNAMES", NameEnglish = "Procedural product names",
                DescriptionEnglish = JsonSerializer.Serialize(naming) }
        });
        ConsoleStyle.Success($"Wrote naming for {naming.Count} procedural products to {catalogPath}.");
    }
}
