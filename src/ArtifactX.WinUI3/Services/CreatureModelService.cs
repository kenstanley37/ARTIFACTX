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

    /// <summary>Resolves a creature species archetype and/or its descriptor list to a known rig ID.</summary>
    public static async Task<string?> ResolveRigAsync(string? archetype, IEnumerable<string>? descriptors = null, string? scenePath = null)
    {
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

        // The creature's own scene from the game's filename table (catalog format 80): WEIRDCRYSTAL ->
        // WEIRD/WEIRDRIGGROUND.SCENE.MBIN -> "weirdrigground", so kinds without an alias get their real model.
        if (!string.IsNullOrEmpty(scenePath))
        {
            string stem = Path.GetFileName(scenePath.Replace('\\', '/'));
            int dot = stem.IndexOf('.');
            if (dot > 0) stem = stem[..dot];
            if (RigScenes.ContainsKey(stem)) return stem.ToLowerInvariant();
        }

        return null;
    }

    /// <summary>Builds a 3D mesh for a companion or creature from its rig and chosen descriptors.</summary>
    /// <param name="wild">A wild creature: the pet harness that hangs off every rig's body (PETHARNESSMAT, 4,216 of
    /// 12,708 triangles on the owner's G. Towerravusosa, 2026-11-12) is left out - only companions wear it.</param>
    public static async Task<ShipMesh?> BuildCreatureMeshAsync(GameInstall game, string rigId, IEnumerable<string>? chosenDescriptors = null,
        bool wild = false)
    {
        if (!RigScenes.TryGetValue(rigId, out string? scene)) return null;

        var models = await ModelPreviewService.ModelsAsync(game);
        if (models is null) return null;

        var tree = await CatalogService.GetCreatureDescriptorTreeAsync(rigId);
        var allOptions = tree.Select(n => n.OptionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
    /// maybe the colour seed (to check). Companions draw from their stored parts. Not yet: the anomalous rig's role
    /// Filter (FLOAT / ROLL / CRYSTAL). Null without the part tree (game data before format 87).
    /// </summary>
    /// <param name="filter">The role's descriptor Filter and <paramref name="subType"/> the exotic planet's sub-type
    /// name (CreatureTables.ForcedPartTerms): the parts they name are taken before the seed picks the rest.</param>
    public static async Task<IReadOnlyList<string>?> PartsForSeedAsync(string? scenePath, ulong roleSeed, string filter = "", string subType = "")
    {
        if (string.IsNullOrEmpty(scenePath) || roleSeed == 0) return null;
        await PartTreeService.EnsureAsync();
        string scene = scenePath.ToLowerInvariant();
        if (PartTreeService.Load(ArtifactX.Core.Procedural.ShipParts.DescriptorFor(scene)) is not { } root) return null;
        return await Task.Run(() =>
        {
            IReadOnlyList<string>? forced = filter.Length == 0 && subType.Length == 0 ? null
                : ArtifactX.Core.Universe.CreatureTables.ForcedOptions(
                    ModelPreviewService.AllOptionIds(root, PartTreeService.Load, StringComparer.OrdinalIgnoreCase), filter, subType);
            return (IReadOnlyList<string>)ArtifactX.Core.Procedural.ShipParts.Generate(scene, roleSeed, PartTreeService.Load, forced)
                .Select(p => p.Id).ToList();
        });
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
    public static string HumanizeRig(string rigId) => rigId.ToLowerInvariant() switch
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
