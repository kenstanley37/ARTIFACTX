using ArtifactX.Core.Universe;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string RewardTableFile = "METADATA/REALITY/TABLES/REWARDTABLE.MBIN";
    private const string SpawnDensityFile = "METADATA/SIMULATION/SOLARSYSTEM/BIOMES/PLACEMENTVALUES/SPAWNDENSITYLIST.MBIN";

    /// <summary>The object tables with <see cref="ObjectTables.Harvest"/> filled from every spawn file their lists name.</summary>
    private static ObjectTables ObjectTablesWithHarvest(ObjectTables tables, Func<string, NMSTemplate?> decode)
    {
        var lists = tables.BiomeFiles.SelectMany(b => b).OfType<BiomeObjects>().SelectMany(b => b.Lists)
            .Concat(tables.Common).Concat(tables.Groups.SelectMany(g => g.Files));
        var harvest = HarvestItems(lists.SelectMany(l => l.Files ?? []), decode);
        if (harvest is null) ConsoleStyle.Warning("Reward or spawn density table not found - what planets' plants and objects give left out.");
        return tables with { Harvest = harvest };
    }

    /// <summary>
    /// What each spawn file's objects give (catalog format 93, the Ingredient Finder): spawn file (GcExternalObjectList)
    /// -> each object's scene -> the ATTACHMENT entities in it (and in scenes it references) -> a
    /// GcSimpleInteractionComponentData's Id -> that id's entry in rewardtable.mbin (any of its tables: wild plants use
    /// GenericTable ids like WILD_LUSH, the cooking plants DestructionTable ids like DE_COOK_LUSH) -> the specific
    /// products and substances it gives. Read from build 185034's files 2026-11-13: LUSHWILD.MBIN -> IMPULSEBEANS.SCENE
    /// -> DE_COOK_LUSH -> FOOD_P_LUSHWILD 5-10; LUSH.MBIN -> LUSHPLANT.SCENE -> WILD_LUSH -> PLANT_LUSH 10-15;
    /// UNDERWATERSPHERES.MBIN -> FISHFIENDROCK.SCENE (by reference) -> DE_FISHCORE -> FISHCORE.
    /// From format 94 also what mining an object gives (GcDestructableComponentData's GivesReward, looked up the same
    /// way, and each GivesSubstances entry naming a Specific substance; entries with only a Category give the planet's
    /// own substance and are left out), marked <see cref="HarvestItem.Mined"/> unless picking gives it too. Read from
    /// build 185034's files 2026-11-13: UNDERWATERFULL.MBIN -> MEDIUIMGLOWPLANT.SCENE -> GivesSubstances PLANT_WATER 8-10
    /// (Kelp Sac; its "Kelp Sac" interaction only refills oxygen, REFRESH_O2); plants' DE_PLANT_SMALL/MED/LARGE -> FUEL1
    /// (Carbon); red crystals' DE_GEM_R_* -> FUEL2 (Condensed Carbon); DE_WATER_MED -> WATER1 (Salt). Its LootReward is
    /// read the same way, and its DestroyedModel scene like any other (what lies there once it's broken counts as mined).
    /// An object whose Placement isn't an active entry of spawndensitylist.mbin is left out: the game has no density
    /// for it (the lush, toxic and barren farm files list Solanium under BIOMEPLANTX, and Solanium doesn't grow there).
    /// </summary>
    /// <param name="spawnFiles">Every spawn file the object lists name.</param>
    private static Dictionary<string, HarvestItem[]>? HarvestItems(IEnumerable<string> spawnFiles, Func<string, NMSTemplate?> decode)
    {
        if (decode(RewardTableFile) is not GcRewardTable rewardTable || decode(SpawnDensityFile) is not GcSpawnDensityList density)
            return null;

        var rewards = new Dictionary<string, GcGenericRewardTableEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(GcRewardTable).GetFields())
            if (field.GetValue(rewardTable) is List<GcGenericRewardTableEntry> entries)
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.Id?.Value)) rewards.TryAdd(e.Id.Value, e);

        var placements = (density.DensityList ?? new()).Where(d => d.Active).Select(d => d.Name?.Value ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HarvestItem[] RewardItems(string id, bool mined = false)
        {
            if (!rewards.TryGetValue(id, out var entry) || entry.List is not { } list) return [];
            bool oneOf = list.RewardChoice is not (GcRewardTableItemList.RewardChoiceEnum.GiveAll or GcRewardTableItemList.RewardChoiceEnum.GiveAllSilent);
            return (list.List ?? new()).Select(i => i.Reward switch
            {
                GcRewardSpecificProduct p when !string.IsNullOrEmpty(p.ID?.Value) => new HarvestItem(p.ID.Value, p.AmountMin, p.AmountMax, oneOf, mined),
                GcRewardSpecificSubstance s when !string.IsNullOrEmpty(s.ID?.Value) => new HarvestItem(s.ID.Value, s.AmountMin, s.AmountMax, oneOf, mined),
                _ => null,
            }).OfType<HarvestItem>().ToArray();
        }

        var sceneItems = new Dictionary<string, HarvestItem[]>(StringComparer.OrdinalIgnoreCase);
        HarvestItem[] SceneItems(string scene, int depth)
        {
            if (sceneItems.TryGetValue(scene, out var known)) return known;
            sceneItems[scene] = []; // a scene referencing itself ends here
            if (depth > 8 || decode(scene) is not TkSceneNodeData root) return [];
            var found = new List<HarvestItem>();
            void Walk(TkSceneNodeData node)
            {
                foreach (var attr in node.Attributes ?? new())
                {
                    string value = (attr.Value?.Value ?? "").Replace('\\', '/').ToUpperInvariant();
                    if (value.Length == 0) continue;
                    if (attr.Name?.Value == "ATTACHMENT" && decode(value) is TkAttachmentData entity)
                        foreach (var component in entity.Components ?? new())
                            switch (component.Template)
                            {
                                case GcSimpleInteractionComponentData interaction when !string.IsNullOrEmpty(interaction.Id?.Value):
                                    found.AddRange(RewardItems(interaction.Id.Value));
                                    break;
                                case GcDestructableComponentData destructable:
                                    if (!string.IsNullOrEmpty(destructable.GivesReward?.Value)) found.AddRange(RewardItems(destructable.GivesReward.Value, mined: true));
                                    if (!string.IsNullOrEmpty(destructable.LootReward?.Value)) found.AddRange(RewardItems(destructable.LootReward.Value, mined: true));
                                    // What's left once it's broken (a Whispering Egg's core): picked up, but only after mining.
                                    if (destructable.DestroyedModel?.Filename?.Value is { Length: > 0 } broken)
                                        found.AddRange(SceneItems(broken.Replace('\\', '/').ToUpperInvariant(), depth + 1).Select(i => i with { Mined = true }));
                                    foreach (var substance in destructable.GivesSubstances ?? new())
                                        if (!string.IsNullOrEmpty(substance.Specific?.Value))
                                            found.Add(new HarvestItem(substance.Specific.Value, substance.AmountMin, substance.AmountMax, Mined: true));
                                    break;
                            }
                    if (attr.Name?.Value == "SCENEGRAPH" && node.Type?.Value == "REFERENCE")
                        found.AddRange(SceneItems(value, depth + 1));
                }
                foreach (var child in node.Children ?? new()) Walk(child);
            }
            Walk(root);
            return sceneItems[scene] = Merge(found);
        }

        var result = new Dictionary<string, HarvestItem[]>(StringComparer.Ordinal);
        foreach (string file in spawnFiles.Select(f => f.ToUpperInvariant()).Distinct())
        {
            if (decode(file) is not GcExternalObjectList spawn || spawn.Objects is not { } objects) continue;
            var found = new List<HarvestItem>();
            foreach (var list in new[] { objects.Landmarks, objects.Objects, objects.DetailObjects, objects.DistantObjects })
                foreach (var o in list ?? new())
                    if (placements.Contains(o.Placement?.Value ?? "") && o.Resource?.Filename?.Value is { Length: > 0 } scene)
                        found.AddRange(SceneItems(scene.Replace('\\', '/').ToUpperInvariant(), 0));
            if (found.Count > 0) result[file] = Merge(found);
        }
        return result;

        // One entry per item, the widest amount range; mined only when no object gives it when picked.
        static HarvestItem[] Merge(IEnumerable<HarvestItem> items) => items.GroupBy(i => i.ItemId)
            .Select(g => new HarvestItem(g.Key, g.Min(i => i.Min), g.Max(i => i.Max), g.All(i => i.OneOf), g.All(i => i.Mined))).ToArray();
    }
}
