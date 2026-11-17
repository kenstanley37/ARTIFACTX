namespace ArtifactX.Core.Universe;

/// <summary>
/// Plain names for kinds of creature (GcCreatureData Id, or the species id its model comes from), for search filters
/// and for creatures whose species name can't be predicted. The game itself never names a kind - only species
/// ("G. Rattusguteum") - so these are ArtifactX's own descriptive words, never the raw ids. A kind not listed gets
/// its id turned into words ("WALKER_CRAB" -> "Walker Crab").
/// </summary>
public static class CreatureKindNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ANTELOPE"] = "Antelope",
        ["ARTHROPOD"] = "Arthropod",
        ["BLOB"] = "Blob",
        ["BONECAT"] = "Bone Cat",
        ["BONECOW"] = "Bone Grazer",
        ["BUGFIEND"] = "Bug Fiend",
        ["CAT"] = "Feline",
        ["COW"] = "Grazer",
        ["CRAB"] = "Crab",
        ["DIPLO_PET"] = "Long-necked Grazer",
        ["FIEND"] = "Fiend",
        ["FISHBOWL_PET3"] = "Fishbowl Creature",
        ["FLOATSPIDER"] = "Floating Spider",
        ["FLYINGBEETLE"] = "Flying Beetle",
        ["FLYINGLIZARD"] = "Flying Lizard",
        ["FLYINGSNAKE"] = "Flying Snake",
        ["GRUNT"] = "Grunt",
        ["HERMITCRAB"] = "Hermit Crab",
        ["HORROR_PET"] = "Horror",
        ["HOVER_PET"] = "Hovering Creature",
        ["LAND_JELLYFISH"] = "Land Jellyfish",
        ["LANDSQUID_PET"] = "Land Squid",
        ["LARGEBUTTERFLY"] = "Giant Butterfly",
        ["PLANTCAT"] = "Plant Feline",
        ["PROTOFLYER"] = "Proto-flyer",
        ["PROTOROLLER"] = "Proto-roller",
        ["PURPLE_WEIRD"] = "Purple Anomaly",
        ["ROBO_PET"] = "Robot Companion",
        ["ROBO_RODENT"] = "Robot Rodent",
        ["ROBOTANTELOPE"] = "Robot Antelope",
        ["RODENT"] = "Rodent",
        ["SCUTTLER_PET"] = "Scuttler",
        ["SEASNAKE"] = "Sea Snake",
        ["SIXLEGCAT"] = "Six-legged Feline",
        ["SIXLEGCOW"] = "Six-legged Grazer",
        ["SPIDER"] = "Spider",
        ["SPIDERQUAD_PET"] = "Four-legged Spider",
        ["STRIDER"] = "Strider",
        ["STRIDERGLOW"] = "Glowing Strider",
        ["SWIMCOW"] = "Aquatic Grazer",
        ["SWIMRODENT"] = "Aquatic Rodent",
        ["TREX"] = "Tyrannosaur",
        ["TRICERATOPS"] = "Triceratops",
        ["TWOLEGANTELOPE"] = "Bipedal Antelope",
        ["WALKER_CRAB"] = "Walker Crab",
        ["WALKINGBUILDING"] = "Walking Building",
        ["WEIRDCRYSTAL"] = "Crystal Creature",
        ["WEIRDFLOAT"] = "Anomalous Floater",
        ["WEIRDROLL"] = "Anomalous Roller",
    };

    /// <summary>The kind's plain name.</summary>
    public static string Name(string id) => Names.TryGetValue(id, out var name) ? name
        : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').ToLowerInvariant());
}
