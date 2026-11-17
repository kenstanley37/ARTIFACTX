using ArtifactX.Core.Procedural;
using ArtifactX.GameData;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Builds 3D models for animal companions and creatures from the player's own game files
/// (models/planets/creatures/*/*.scene.mbin) using their species archetype and descriptor parts (osl).
/// </summary>
public static class CreatureModelService
{
    private static readonly Dictionary<string, string> RigScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["antelope"] = "models/planets/creatures/anteloperig/antelope.scene.mbin",
        ["antelope_bone"] = "models/planets/creatures/anteloperig/antelope_bone.scene.mbin",
        ["antelopeglow"] = "models/planets/creatures/anteloperig/antelopeglow.scene.mbin",
        ["anteloperobot"] = "models/planets/creatures/anteloperig/anteloperobot.scene.mbin",
        ["antelopetwolegs"] = "models/planets/creatures/anteloperig/antelopetwolegs.scene.mbin",
        ["arthropod"] = "models/planets/creatures/arthropod/arthropod.scene.mbin",
        ["beetle"] = "models/planets/creatures/beetlerig/beetle.scene.mbin",
        ["bird"] = "models/planets/creatures/smallbird/bird.scene.mbin",
        ["birdflock"] = "models/planets/creatures/smallbird/birdflock.scene.mbin",
        ["blob"] = "models/planets/creatures/blobrig/blob.scene.mbin",
        ["bonecat"] = "models/planets/creatures/catrig/bonecat.scene.mbin",
        ["bugfiend"] = "models/planets/creatures/arthropod/bugfiend.scene.mbin",
        ["butterfly"] = "models/planets/creatures/butterflyrig/butterfly.scene.mbin",
        ["butterflyflock"] = "models/planets/creatures/butterflyrig/butterflyflock.scene.mbin",
        ["cat"] = "models/planets/creatures/catrig/cat.scene.mbin",
        ["cow"] = "models/planets/creatures/cowrig/cow.scene.mbin",
        ["cowfloating"] = "models/planets/creatures/cowrig/flyingcow.scene.mbin",
        ["cowhindlegs"] = "models/planets/creatures/cowrig/cowhindlegs.scene.mbin",
        ["cowswim"] = "models/planets/creatures/cowrig/cowswim.scene.mbin",
        ["deepseafish"] = "models/planets/creatures/fish/deepseafish.scene.mbin",
        ["deepseafishlarge"] = "models/planets/creatures/fish/deepseafishlarge.scene.mbin",
        ["digger"] = "models/planets/creatures/weird/digger.scene.mbin",
        ["drill"] = "models/planets/creatures/weird/drill.scene.mbin",
        ["fiend"] = "models/planets/creatures/spiderrig/fiend.scene.mbin",
        ["fishfiend"] = "models/planets/creatures/fish/fishfiend.scene.mbin",
        ["fishfiendsmall"] = "models/planets/creatures/fish/fishfiendsmall.scene.mbin",
        ["fishflock"] = "models/planets/creatures/fish/fishflock.scene.mbin",
        ["floatercreature"] = "models/planets/creatures/weird/floatercreature.scene.mbin",
        ["floatingocto"] = "models/planets/creatures/weird/floatingocto.scene.mbin",
        ["flyinglizard"] = "models/planets/creatures/flyinglizardrig/flyinglizard.scene.mbin",
        ["flyinglizardglow"] = "models/planets/creatures/flyinglizardrig/flyinglizardglow.scene.mbin",
        ["flyingsnake"] = "models/planets/creatures/flyingsnake/flyingsnake.scene.mbin",
        ["freighterfiend"] = "models/planets/creatures/spiderrig/freighterfiend.scene.mbin",
        ["groundcreature"] = "models/planets/creatures/weird/groundcreature.scene.mbin",
        ["grunt"] = "models/planets/creatures/gruntrig/grunt.scene.mbin",
        ["gruntcreatetest"] = "models/planets/creatures/gruntrig/gruntcreatetest.scene.mbin",
        ["grunttest"] = "models/planets/creatures/gruntrig/grunttest.scene.mbin",
        ["hermitcrab"] = "models/planets/creatures/spiderrig/hermitcrab.scene.mbin",
        ["largebutterfly"] = "models/planets/creatures/butterflyrig/largebutterfly.scene.mbin",
        ["mantaray"] = "models/planets/creatures/mantaray/mantaray.scene.mbin",
        ["mantarayglow"] = "models/planets/creatures/mantaray/mantarayglow.scene.mbin",
        ["petaccessories"] = "models/planets/creatures/accessories/petaccessories.scene.mbin",
        ["plow"] = "models/planets/creatures/weird/plow.scene.mbin",
        ["prawn"] = "models/planets/creatures/prawn/prawn.scene.mbin",
        ["proc_jellyfish"] = "models/planets/creatures/fish/proc_jellyfish.scene.mbin",
        ["rockcreature"] = "models/planets/creatures/spiderrig/rockcreature.scene.mbin",
        ["rockspider"] = "models/planets/creatures/spiderrig/rockspider.scene.mbin",
        ["rodent"] = "models/planets/creatures/rodentrig/rodent.scene.mbin",
        ["rodentswim"] = "models/planets/creatures/rodentrig/rodentswim.scene.mbin",
        ["rollercreature"] = "models/planets/creatures/weird/rollercreature.scene.mbin",
        ["sandworm"] = "models/planets/creatures/sandworm/sandworm.scene.mbin",
        ["seahorse"] = "models/planets/creatures/seahorse/seahorse.scene.mbin",
        ["seahorseglow"] = "models/planets/creatures/seahorse/seahorseglow.scene.mbin",
        ["seasnake"] = "models/planets/creatures/seasnake/seasnake.scene.mbin",
        ["shark"] = "models/planets/creatures/sharkrig/shark.scene.mbin",
        ["sixlegcat"] = "models/planets/creatures/catrig/sixlegcat.scene.mbin",
        ["sixleggedcow"] = "models/planets/creatures/cowrig/sixleggedcow.scene.mbin",
        ["smallbird"] = "models/planets/creatures/smallbird/smallbird.scene.mbin",
        ["smallfish"] = "models/planets/creatures/fish/smallfish.scene.mbin",
        ["spider"] = "models/planets/creatures/spiderrig/spider.scene.mbin",
        ["spiderfloat"] = "models/planets/creatures/spiderrig/spiderfloat.scene.mbin",
        ["squid"] = "models/planets/creatures/squid/squid.scene.mbin",
        ["strider"] = "models/planets/creatures/striderrig/strider.scene.mbin",
        ["striderglow"] = "models/planets/creatures/striderrig/striderglow.scene.mbin",
        ["trex"] = "models/planets/creatures/trexrig/trex.scene.mbin",
        ["triceratops"] = "models/planets/creatures/triceratopsrig/triceratops.scene.mbin",
        ["weirdbutterfly"] = "models/planets/creatures/weird/weirdbutterfly.scene.mbin",
        ["weirddigger"] = "models/planets/creatures/weird/weirddigger.scene.mbin",
        ["weirdflock"] = "models/planets/creatures/weird/weirdflock.scene.mbin",
        ["weirdrig"] = "models/planets/creatures/weird/weirdrig.scene.mbin",
        ["weirdrigground"] = "models/planets/creatures/weird/weirdrigground.scene.mbin",
    };

    private static readonly Dictionary<string, string> SpeciesAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["robotantelope"] = "anteloperobot",
        ["sixlegcow"] = "sixleggedcow",
        ["plantcat"] = "blob",
        ["weirdfloat"] = "weirdrig",
        ["weirdroll"] = "weirdrig",
        ["protoroller"] = "rollercreature",
        ["diplo_pet"] = "triceratops",
        ["flyingbeetle"] = "beetle",
        ["floatspider"] = "spiderfloat",
        ["crab"] = "spider",
        ["swimcow"] = "cowswim",
        ["twolegantelope"] = "antelopetwolegs",
        ["bonecow"] = "cow",
        ["fishbowl_pet3"] = "anteloperobot",
    };

    /// <summary>Resolves a creature species archetype and/or its descriptor list to a known rig ID. The creature's own
    /// scene from the game's filename table wins when given: a scene outside the known rigs is its own rig id (the
    /// lower-case scene path), so PLANTCAT draws MODELS/PLANETS/BIOMES/FLORAL/MEDIUMCREATURE/FLORALCAT - the old
    /// "plantcat" -> blob alias drew the owner's W. Gentaylatica (a root-bodied plant creature) as a blob (2026-11-13).</summary>
    public static async Task<string?> ResolveRigAsync(string? archetype, IEnumerable<string>? descriptors = null, string? scenePath = null)
    {
        if (!string.IsNullOrEmpty(scenePath))
        {
            string path = scenePath.Replace('\\', '/').ToLowerInvariant();
            string stem = Path.GetFileName(path);
            int dot = stem.IndexOf('.');
            if (dot > 0) stem = stem[..dot];
            return RigScenes.TryGetValue(stem, out string? known) && known == path ? stem : path;
        }

        if (!string.IsNullOrWhiteSpace(archetype))
        {
            string raw = archetype.TrimStart('^').Trim();
            if (SpeciesAliases.TryGetValue(raw, out string? alias) && RigScenes.ContainsKey(alias))
                return alias;

            string lower = raw.ToLowerInvariant();
            if (RigScenes.ContainsKey(lower))
                return lower;
        }

        if (descriptors is not null)
        {
            foreach (string desc in descriptors)
            {
                string cleanDesc = desc.TrimStart('^').Trim();
                if (ulong.TryParse(cleanDesc, out _)) continue; // ignore detail seeds
                string? rig = await CatalogService.GetRigForDescriptorOptionAsync(cleanDesc);
                if (rig is not null && RigScenes.ContainsKey(rig))
                    return rig;
            }
        }

        return null;
    }

    /// <summary>Builds a 3D mesh for a companion or creature from its rig and chosen descriptors.</summary>
    /// <param name="wild">A wild creature: the pet harness that hangs off every rig's body (PETHARNESSMAT, 4,216 of
    /// 12,708 triangles on the owner's G. Towerravusosa, 2026-11-12) is left out - only companions wear it.</param>
    /// <param name="extra">A scene drawn with it (ModelPartsAsync): a flock's animal, a crystal's ground piece.</param>
    public static async Task<ShipMesh?> BuildCreatureMeshAsync(GameInstall game, string rigId, IEnumerable<string>? chosenDescriptors = null,
        bool wild = false, ExtraModel? extra = null)
    {
        if (extra is not null)
            return Merge(await BuildCreatureMeshAsync(game, rigId, chosenDescriptors, wild),
                await BuildCreatureMeshAsync(game, extra.RigId, extra.Parts, wild));
        string? scene = RigScenes.TryGetValue(rigId, out string? known) ? known : IsScenePath(rigId) ? rigId : null;
        if (scene is null) return null;

        var models = await ModelPreviewService.ModelsAsync(game);
        if (models is null) return null;

        var tree = await CatalogService.GetCreatureDescriptorTreeAsync(rigId);
        var allOptions = tree.Select(n => n.OptionId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // A scene outside the companion rig table (the floral cat, robots): its options from its part tree, so the parts
        // it didn't pick are left out instead of all drawn at once.
        if (allOptions.Count == 0)
        {
            await PartTreeService.EnsureAsync();
            if (PartTreeService.Load(ArtifactX.Core.Procedural.ShipParts.DescriptorFor(scene)) is { } root)
                allOptions = ModelPreviewService.AllOptionIds(root, PartTreeService.Load, StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        HashSet<string> chosen;
        if (chosenDescriptors is not null && chosenDescriptors.Any())
        {
            chosen = chosenDescriptors
                .Select(d => d.TrimStart('^').Trim())
                .Where(d => !ulong.TryParse(d, out _))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            // When no descriptors are given (procedural wild fauna), pick the rig's default descriptor set
            // so that head, body, legs, and tail parts are included rather than omitted.
            chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootCategories = tree
                .Where(n => n.ParentOptionId == null)
                .GroupBy(n => n.Category)
                .OrderBy(g => g.Min(n => n.SortOrder));

            foreach (var rootCategory in rootCategories)
            {
                var root = rootCategory
                    .OrderBy(n => n.OptionId.EndsWith("XRARE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(n => n.SortOrder)
                    .First();
                var defaults = CompanionService.BuildDefaultDescriptorArray(tree, root.OptionId, new JArray());
                foreach (var d in defaults)
                {
                    string id = (d?.ToString() ?? "").TrimStart('^').Trim();
                    if (id.Length > 0) chosen.Add(id);
                }
            }

            if (chosen.Count == 0 && tree.Count > 0)
            {
                foreach (var cat in tree.GroupBy(n => n.Category))
                {
                    chosen.Add(cat.OrderBy(n => n.SortOrder).First().OptionId);
                }
            }
        }

        // A creature mesh is built off the UI thread
        return await Task.Run(() =>
        {
            var mesh = models.Build(scene, allOptions, chosen);
            return wild && mesh is not null ? WithoutPetHarness(mesh) : mesh;
        });
    }

    /// <summary>A rig id that is a scene path itself (a creature scene outside the known rigs).</summary>
    public static bool IsScenePath(string rigId) => rigId.EndsWith(".scene.mbin", StringComparison.OrdinalIgnoreCase);

    /// <summary>The mesh without the triangles painted with the pet harness material (one material per triangle,
    /// three corners and texture coordinates each).</summary>
    private static ShipMesh? WithoutPetHarness(ShipMesh mesh)
    {
        var keep = Enumerable.Range(0, mesh.Materials.Length)
            .Where(t => !Path.GetFileName(mesh.Materials[t]).Contains("PETHARNESS", StringComparison.OrdinalIgnoreCase)).ToList();
        if (keep.Count == mesh.Materials.Length) return mesh;
        if (keep.Count == 0) return null;
        T[] Corners<T>(T[] source) => source.Length < mesh.Materials.Length * 3 ? source
            : keep.SelectMany(t => new[] { source[t * 3], source[t * 3 + 1], source[t * 3 + 2] }).ToArray();
        return new ShipMesh(Corners(mesh.Corners), keep.Select(t => mesh.Materials[t]).ToArray())
        {
            Uvs = Corners(mesh.Uvs), Uvs2 = Corners(mesh.Uvs2),
        };
    }

    /// <summary>
    /// The body parts the game gives a wild creature (issue #15): its scene's part tree walked by the same selector as
    /// ships and multi-tools (Core ShipParts) with its role seed (discovery bEr[0], PredictedCreature.Seed). Evidence:
    /// a companion's CreatureSeed (WTp) equals its species' discovery bEr[0] for 132/132, and ShipParts(WTp) gives the
    /// stored descriptor list (osl) for 78 of 182 companions; the owner's G. Towerravusosa (Slot 8, 2026-11-12) shows
    /// the hippo head with two horn accessories, turtle body with back spikes and triceratops tail this picks. The
    /// discovery's fifth value, Mix64(bEr[0]), picks a different head (frilled triceratops) - not the body seed;
    /// maybe the colour seed (to check). Companions draw from their stored parts. Null without the part tree (game
    /// data before format 87).
    /// </summary>
    /// <param name="filter">The creature's rig filter name (CreatureTables.ForcedPartTerms / PartFilterName: the role's
    /// Filter, else PREDATOR or the game's HERBIBVORE): the scene's filter file entry it names lists parts the creature
    /// can't have (CreatureTables.ExcludedParts), left out of their groups before the seed draws - the anomalous rig's
    /// FLOAT / ROLL / CRYSTAL leave one body, still with a draw (the owner's R. Gentdiffoe, Aoedesdo, 2026-11-12, is
    /// the WEIRDRIG _CRYSTAL_PILLAR; no draw gave _CRYSTAL_GEM), and a predator strider never has the fish head.
    /// <paramref name="subType"/>: the exotic planet's sub-type name, which forces the parts it names.</param>
    /// <param name="male">The species' male form (ShipParts.GenerateMale, the spawn data's Resource) instead of the
    /// female (FemaleResource), which the plain selector gives.</param>
    public static async Task<IReadOnlyList<string>?> PartsForSeedAsync(string? scenePath, ulong roleSeed, string filter = "", string subType = "",
        bool male = false)
    {
        if (string.IsNullOrEmpty(scenePath) || roleSeed == 0) return null;
        await PartTreeService.EnsureAsync();
        string scene = scenePath.ToLowerInvariant();
        if (PartTreeService.Load(ArtifactX.Core.Procedural.ShipParts.DescriptorFor(scene)) is not { } root) return null;
        var excluded = ArtifactX.Core.Universe.CreatureTables.ExcludedParts(await CatalogService.GetCreatureFiltersAsync(scene), filter);
        return await Task.Run(() =>
        {
            IReadOnlyList<string>? forced = subType.Length == 0 ? null
                : ArtifactX.Core.Universe.CreatureTables.ForcedOptions(OptionNames(root), subType);
            var picked = male
                ? ArtifactX.Core.Procedural.ShipParts.GenerateMale(scene, roleSeed, PartTreeService.Load, forced, drawForced: true, excluded)
                : ArtifactX.Core.Procedural.ShipParts.Generate(scene, roleSeed, PartTreeService.Load, forced, drawForced: true, excluded);
            return (IReadOnlyList<string>)picked.Select(p => p.Id).ToList();
        });
    }

    /// <summary>A second scene drawn with a creature (its rig and that scene's parts).</summary>
    public sealed record ExtraModel(string RigId, IReadOnlyList<string>? Parts);

    /// <summary>
    /// A wild creature's whole model: its own scene's parts, plus the scene the game spawns with it (the creature
    /// row's ExtraFilename, catalog format 89 ModelScene). Flocks and the anomalous digger have no geometry of their
    /// own (fauna checks --creature-part-probe "*", 2026-11-12) - the extra scene is the animal; an anomalous crystal is
    /// WEIRDRIG's crystal with the ground piece of WEIRDRIGGROUND round its foot. Owner's R. Gentdiffoe on Aoedesdo
    /// (BoneSpire, 2026-11-12): a green pillar with bone spires coming out of the ground beside it; the Discoveries
    /// page shows only the spires. The two part lists stay apart: "_CRYSTAL_PILLAR" is an option in both scenes.
    /// <paramref name="male"/>: the male form's own parts; the extra scene is drawn once for both forms (the game's
    /// ExtraResource, FUN_1416d5080: the single selector).
    /// </summary>
    public static async Task<(IReadOnlyList<string>? Parts, ExtraModel? Extra)> ModelPartsAsync(string? scenePath, string? extraScenePath,
        ulong roleSeed, string filter = "", string subType = "", bool male = false)
    {
        var parts = await PartsForSeedAsync(scenePath, roleSeed, filter, subType, male);
        if (string.IsNullOrEmpty(extraScenePath) || await ResolveRigAsync(null, scenePath: extraScenePath) is not { } extraRig)
            return (parts, null);
        return (parts, new ExtraModel(extraRig, await PartsForSeedAsync(extraScenePath, roleSeed, filter, subType)));
    }

    /// <summary>Two meshes as one (a creature and the scene spawned with it); either may be missing.</summary>
    private static ShipMesh? Merge(ShipMesh? a, ShipMesh? b)
    {
        if (a is null || a.Materials.Length == 0) return b;
        if (b is null || b.Materials.Length == 0) return a;
        // Texture coordinates are one per corner or none: pad a mesh without them so both line up.
        static System.Numerics.Vector2[] Per(System.Numerics.Vector2[] uvs, int corners) =>
            uvs.Length == corners ? uvs : new System.Numerics.Vector2[corners];
        bool uvs = a.Uvs.Length > 0 || b.Uvs.Length > 0, uvs2 = a.Uvs2.Length > 0 || b.Uvs2.Length > 0;
        return new ShipMesh([.. a.Corners, .. b.Corners], [.. a.Materials, .. b.Materials])
        {
            Uvs = uvs ? [.. Per(a.Uvs, a.Corners.Length), .. Per(b.Uvs, b.Corners.Length)] : [],
            Uvs2 = uvs2 ? [.. Per(a.Uvs2, a.Corners.Length), .. Per(b.Uvs2, b.Corners.Length)] : [],
        };
    }

    /// <summary>Every option of a part tree with its name, through nested groups.</summary>
    private static IEnumerable<(string Id, string Name)> OptionNames(IReadOnlyList<ArtifactX.Core.Procedural.PartGroup> groups) =>
        groups.SelectMany(g => g.Options.SelectMany(o => new[] { (o.Id, o.Name) }.Concat(o.Children.SelectMany(OptionNames))));

    /// <summary>
    /// A creature's skin painted the way ships and multi-tools are (ShipModels.Painted): every creature material is a
    /// stack of procedural texture layers tinted from a palette table - Fur, Scale, Underbelly, Rock, Leaf, Feather,
    /// Metal, Custom_Head (GameData.Checks --creature-part-probe, 2026-11-13: cow, cat, triceratops and bird scenes; only
    /// the pet harness and a few eyes are fixed textures). The creature's own seed picks the layers and patterns once
    /// for the whole model (ShipLook.PickMerged over every material's list), so body, head and tail share one MARKINGS
    /// pattern - 42 of 42 checked against the textures the running game loaded (2026-11-13, see PickMerged). The first
    /// child seed used before matched only by chance (16/53). Each creature has its **own** palette table: the palette generator
    /// (ShipLook.PaletteTable) seeded with the creature's raw spawn seed over the **base** palettes
    /// (basecolourpalettes - CatalogService.GetPalettesAsync), and each texture option takes its row's entry at its
    /// own slot (0-4). NMS.exe 184271: LoadModel (FUN_1416d5940) hands the texture task the descriptor result whose
    /// +0x10 seed is the spawn seed (FUN_142ddb8e0 copies plVar6[6..7]); the task (FUN_140646250, state 0) builds the
    /// table with FUN_140639e30(palettes, 0x10, 0, seed), and FUN_14063a560 reads palette set [0x10][0] - not the
    /// planet's biome and sub-type. Running game (185034, on the lush planet Marjorg, 2026-11-13): the biome x sub-type
    /// file table's row 16 entry 0 points at the loaded basecolourpalettes, the lush row at lushcolourpalettes. Owner's
    /// Marjorg shots agree: V. Snakwercus grey scaled body with a yellow beak, W. Geoyugia rust body with green legs
    /// only come out over the base palettes; the slot held the base file on a second planet too. Companions use <paramref name="colourSeed"/> (an active
    /// ColourBaseSeed, uAX) or their creature seed the same way - unconfirmed. Null without game data or palettes.
    /// <paramref name="size"/>: pixels per material texture (small cards use less).
    /// </summary>
    public static async Task<Dictionary<string, ModelTexture?>?> PaintAsync(GameInstall game, ShipMesh mesh, ulong seed, ulong colourSeed = 0,
        int size = 512)
    {
        if (seed == 0) return null;
        if (await ModelPreviewService.ModelsAsync(game) is not { } models) return null;
        ulong tableSeed = colourSeed != 0 ? colourSeed : seed;
        if (await ModelPreviewService.SeedColoursAsync(tableSeed, rawSeed: true, lastSlot: 4) is not { } colours) return null;
        var picks = await Task.Run(() => ShipLook.PickMerged(
            mesh.Materials.Distinct().Select(models.TextureLayers).OfType<IReadOnlyList<TextureLayer>>(), seed));
        return await ModelPreviewService.PaintAsync(models, mesh, colours, seed, size: size, layerPicks: picks);
    }

    /// <summary>A companion's look seeds: CreatureSeed (WTp) and, when its active flag is on, ColourBaseSeed (uAX) -
    /// see NmsCompanionPaths. 0 where missing or off.</summary>
    public static (ulong Seed, ulong ColourSeed) CompanionLookSeeds(JToken? companion)
    {
        static ulong Seed(JToken? pair) =>
            pair is JArray { Count: > 1 } a && ArtifactX.Core.NmsModels.NmsSeedGenerator.TryParse(a[1]?.ToString(), out ulong s) ? s : 0;
        ulong colour = companion?["uAX"] is JArray { Count: > 1 } uax && uax[0]?.Type == JTokenType.Boolean && (bool)uax[0]! ? Seed(uax) : 0;
        return (Seed(companion?["WTp"]), colour);
    }

    /// <summary>Natural shading for creature materials: eyes, teeth, mouth, horns, pet harness and skin.</summary>
    public static uint CreatureMaterialColour(string material)
    {
        string name = Path.GetFileName(material).ToUpperInvariant();
        static uint Rgb(int r, int g, int b) => (uint)(r << 16 | g << 8 | b);

        if (Regex.IsMatch(name, "EYE")) return Rgb(64, 210, 240); // glossy light cyan eyes
        if (Regex.IsMatch(name, "GUMS|MOUTH|TONGUE|MEAT")) return Rgb(188, 72, 85); // fleshy pink/crimson
        if (Regex.IsMatch(name, "TEETH|BONE|HORN|ANTENNA|SPIKE|CLAW|BEAK|TUSK")) return Rgb(230, 224, 210); // ivory/bone
        if (Regex.IsMatch(name, "HARNESS|SADDLE|PACK|COLLAR|ACC")) return Rgb(180, 120, 60); // warm brass/leather harness
        if (Regex.IsMatch(name, "GLOW|LIGHT|EMIS|BIOLUM")) return Rgb(80, 255, 180); // bioluminescent glow
        if (Regex.IsMatch(name, "SHELL|CARAPACE|PLATING|CHITIN|ARMOR")) return Rgb(58, 62, 68); // dark slate chitin
        if (Regex.IsMatch(name, "BELLY|UNDER")) return Rgb(185, 175, 160); // lighter underbelly

        // Natural earthy creature tone
        return Rgb(145, 138, 128);
    }

    public sealed record CreatureRigOption(string RigId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    /// <summary>Clean display name for a creature rig.</summary>
    public static string HumanizeRig(string rigId) => IsScenePath(rigId) ? HumanizeRig(Path.GetFileName(rigId).Split('.')[0]) : rigId.ToLowerInvariant() switch
    {
        "anteloperobot" => "Robot Antelope",
        "antelopeglow" => "Glowing Antelope",
        "antelope_bone" => "Bone Antelope",
        "antelopetwolegs" => "Bipedal Antelope",
        "trex" => "T-Rex / Theropod",
        "triceratops" => "Triceratops / Diplo",
        "strider" => "Strider",
        "striderglow" => "Glowing Strider",
        "sixleggedcow" => "Six-Legged Cow / Quadruped",
        "blob" => "Blob / Plant Cat",
        "bonecat" => "Bone Cat",
        "cat" => "Cat / Feline",
        "sixlegcat" => "Six-Legged Cat",
        "cow" => "Cow / Megafauna",
        "cowfloating" => "Floating Cow",
        "cowhindlegs" => "Bipedal Cow",
        "cowswim" => "Aquatic Cow",
        "beetle" => "Beetle / Flying Insect",
        "flyingsnake" => "Flying Snake / Dragon",
        "flyinglizard" => "Flying Lizard",
        "flyinglizardglow" => "Glowing Flying Lizard",
        "spider" => "Spider / Crab",
        "spiderfloat" => "Floating Spider",
        "rockspider" => "Rock Spider",
        "rockcreature" => "Rock Creature",
        "hermitcrab" => "Hermit Crab",
        "sandworm" => "Giant Sandworm",
        "shark" => "Shark",
        "deepseafish" => "Deep Sea Fish",
        "deepseafishlarge" => "Large Deep Sea Fish",
        "proc_jellyfish" => "Jellyfish",
        "mantaray" => "Manta Ray",
        "mantarayglow" => "Glowing Manta Ray",
        "seahorse" => "Seahorse",
        "seahorseglow" => "Glowing Seahorse",
        "seasnake" => "Sea Snake",
        "squid" => "Squid",
        "prawn" => "Prawn",
        "fishfiend" => "Abyssal Horror / Fish Fiend",
        "fiend" => "Biological Horror / Fiend",
        "bugfiend" => "Vile Brood / Bug Fiend",
        "freighterfiend" => "Derelict Horror",
        "butterfly" => "Butterfly",
        "butterflyflock" => "Butterfly Flock",
        "largebutterfly" => "Giant Butterfly",
        "smallbird" => "Small Bird",
        "bird" => "Bird",
        "birdflock" => "Bird Flock",
        "rodent" => "Rodent",
        "rodentswim" => "Swimming Rodent",
        "rollercreature" => "Protoroller / Rolling Sphere",
        "weirdrig" => "Anomalous Entity",
        "weirdrigground" => "Ground Anomaly",
        "weirdbutterfly" => "Anomalous Butterfly",
        "weirddigger" => "Burrowing Anomaly",
        "floatercreature" => "Floating Entity",
        "floatingocto" => "Floating Octopod",
        "grunt" => "Bipedal Grunt",
        "digger" => "Digger",
        "drill" => "Drill Creature",
        "plow" => "Plow Creature",
        _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rigId.Replace('_', ' '))
    };

    /// <summary>Cataloged creature rigs for selection in 3D viewers.</summary>
    public static readonly IReadOnlyList<CreatureRigOption> AvailableRigs = RigScenes.Keys
        .Where(k => k != "petaccessories" && !k.Contains("test"))
        .OrderBy(k => HumanizeRig(k))
        .Select(k => new CreatureRigOption(k, HumanizeRig(k)))
        .ToList();
}
