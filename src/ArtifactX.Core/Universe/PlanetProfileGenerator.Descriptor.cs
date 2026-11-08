namespace ArtifactX.Core.Universe;

public static partial class PlanetProfileGenerator
{
    /// <summary>
    /// Port of NMS.exe's planet descriptor routine (0x1416ce4a0, build 184271, 2026-11-08).
    /// Uses child seed 21 to pick the procedural descriptor adjective (e.g. "Tropical", "Glacial", "Ossified", "Paradise").
    /// </summary>
    public static string GenerateDescriptor(ulong childSeed21, int biome, int subType, int weather, int storms, int[] sentinels, PlanetProfileTables t)
    {
        // 1. Paradise override (0x1416ce845):
        // Lush biome (0), not Swamp (0x19), Clear weather (0), no storms (0), low sentinels (0 on difficulty slot).
        if (biome == Lush && subType != SwampSub && weather == 0 && storms == 0 && sentinels.Length > 2 && sentinels[2] == 0)
        {
            if (t.Descriptors is not null && t.Descriptors.TryGetValue("UI_PARADISE_PLANET", out var paradise))
                return paradise;
            if (FallbackDescriptors.TryGetValue("UI_PARADISE_PLANET", out var pFallback))
                return pFallback;
            return "Paradise";
        }

        // 2. Determine prefix and candidate count
        string prefix;
        int count;

        if (biome == Weird) // 7
        {
            prefix = subType switch
            {
                0 => "MSTRUCTBIOME",
                1 => "BEAMSBIOME",
                2 => "HEXAGONBIOME",
                3 => "FRACTCUBEBIOME",
                4 => "BUBBLEBIOME",
                5 => "SHARDSBIOME",
                6 => "CONTOURBIOME",
                7 => "IRRISHELLSBIOME",
                8 => "BONESPIREBIOME",
                9 => "WIRECELLSBIOME",
                10 => "HYDROGARDENBIOME",
                _ => "GLITCHBIOME"
            };
            count = prefix == "GLITCHBIOME" ? 12 : 3;
        }
        else if (subType == 0x18) // Infested
        {
            prefix = biome switch
            {
                0 => "INFESTEDLUSH",
                1 => "INFESTEDTOX",
                2 => "INFESTEDHOT",
                3 => "INFESTEDRAD",
                4 => "INFESTEDCOLD",
                5 => "INFESTEDDUST",
                _ => "INFESTED"
            };
            count = prefix switch
            {
                "INFESTEDDUST" or "INFESTEDHOT" or "INFESTEDLUSH" => 4,
                "INFESTEDCOLD" or "INFESTEDRAD" or "INFESTEDTOX" => 3,
                _ => 1
            };
        }
        else if (subType == 3) // Ruined
        {
            prefix = biome switch
            {
                0 => "RUINEDLUSH",
                1 => "RUINEDTOX",
                2 => "RUINEDHOT",
                3 => "RUINEDRAD",
                4 => "RUINEDCOLD",
                5 => "RUINEDDUST",
                _ => "RUINED"
            };
            count = prefix switch
            {
                "RUINEDDUST" or "RUINEDHOT" or "RUINEDLUSH" => 4,
                "RUINEDCOLD" or "RUINEDRAD" or "RUINEDTOX" => 3,
                _ => 1
            };
        }
        else
        {
            if (subType == SwampSub) // 0x19
            {
                prefix = "SWAMPBIOME";
                count = 14;
            }
            else if (subType == LavaSub) // 0x1A
            {
                prefix = "LAVABIOME";
                count = 14;
            }
            else
            {
                (prefix, count) = biome switch
                {
                    0 => ("LUSH", 10),
                    1 => ("TOXIC", 10),
                    2 => ("SCORCHED", 10),
                    3 => ("IRRADIATED", 10),
                    4 => ("FROZEN", 10),
                    5 => ("BARREN", 10),
                    6 => ("DEAD", 10),
                    7 => ("GLITCHBIOME", 12),
                    8 => ("REDBIOME", 10),
                    9 => ("GREENBIOME", 10),
                    10 => ("BLUEBIOME", 10),
                    11 => ("DEAD", 10),
                    12 => ("SWAMPBIOME", 14),
                    13 => ("LAVABIOME", 14),
                    14 => ("WATERWORLD", 8),
                    15 => ("GASGIANT", 1),
                    _ => ("LUSH", 10)
                };
            }
        }

        // 3. Draw 1-based index using MWC generator on child seed 21
        var rng = Rng.Start(childSeed21);
        uint next = rng.Next();
        int index = (int)(((ulong)next * (ulong)count) >> 32) + 1;
        string key = $"{prefix}{index}";

        if (t.Descriptors is not null && t.Descriptors.TryGetValue(key, out var word))
            return word;
        if (FallbackDescriptors.TryGetValue(key, out var fallbackWord))
            return fallbackWord;

        return prefix;
    }

    private static readonly Dictionary<string, string> FallbackDescriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UI_PARADISE_PLANET"] = "Paradise",
        ["BARREN1"] = "Barren",
        ["BARREN2"] = "Desert",
        ["BARREN3"] = "Rocky",
        ["BARREN4"] = "Bleak",
        ["BARREN5"] = "Parched",
        ["BARREN6"] = "Abandoned",
        ["BARREN7"] = "Barren",
        ["BARREN8"] = "Dusty",
        ["BARREN9"] = "Desolate",
        ["BARREN10"] = "Wind-swept",
        ["BEAMSBIOME1"] = "Fissured",
        ["BEAMSBIOME2"] = "of Light",
        ["BEAMSBIOME3"] = "Breached",
        ["BLUEBIOME1"] = "Planetary Anomaly",
        ["BLUEBIOME2"] = "Lost Blue",
        ["BLUEBIOME3"] = "[REDACTED]",
        ["BLUEBIOME4"] = "Stellar Corruption Detected",
        ["BLUEBIOME5"] = "Chromatic Fog",
        ["BLUEBIOME6"] = "Harsh Blue Globe",
        ["BLUEBIOME7"] = "Frozen Anomaly",
        ["BLUEBIOME8"] = "Azure",
        ["BLUEBIOME9"] = "Cerulean",
        ["BLUEBIOME10"] = "Ultramarine",
        ["BONESPIREBIOME1"] = "Rattling",
        ["BONESPIREBIOME2"] = "Spined",
        ["BONESPIREBIOME3"] = "Skeletal",
        ["BUBBLEBIOME1"] = "Bubbling",
        ["BUBBLEBIOME2"] = "Frothing",
        ["BUBBLEBIOME3"] = "Foaming",
        ["CONTOURBIOME1"] = "Contoured",
        ["CONTOURBIOME2"] = "Cabled",
        ["CONTOURBIOME3"] = "Webbed",
        ["DEAD1"] = "Terraforming Catastrophe",
        ["DEAD2"] = "Dead",
        ["DEAD3"] = "Empty",
        ["DEAD4"] = "Desolate",
        ["DEAD5"] = "Lifeless",
        ["DEAD6"] = "Forsaken",
        ["DEAD7"] = "Life-Incompatible",
        ["DEAD8"] = "Low Atmosphere",
        ["DEAD9"] = "Airless",
        ["DEAD10"] = "Abandoned",
        ["FRACTCUBEBIOME1"] = "Mechanical",
        ["FRACTCUBEBIOME2"] = "Metallic",
        ["FRACTCUBEBIOME3"] = "Metallurgic",
        ["FROZEN1"] = "Frozen",
        ["FROZEN2"] = "Icebound",
        ["FROZEN3"] = "Arctic",
        ["FROZEN4"] = "Glacial",
        ["FROZEN5"] = "Sub-zero",
        ["FROZEN6"] = "Icy",
        ["FROZEN7"] = "Frostbound",
        ["FROZEN8"] = "Freezing",
        ["FROZEN9"] = "Hiemal",
        ["FROZEN10"] = "Hyperborean",
        ["GASGIANT1"] = "Gas Giant",
        ["GLITCHBIOME1"] = "Crimson",
        ["GLITCHBIOME2"] = "Planetary Anomaly",
        ["GLITCHBIOME3"] = "Malfunctioning",
        ["GLITCHBIOME4"] = "Breached",
        ["GLITCHBIOME5"] = "Infected",
        ["GLITCHBIOME6"] = "[REDACTED]",
        ["GLITCHBIOME7"] = "Glassy",
        ["GLITCHBIOME8"] = "Thirsty",
        ["GLITCHBIOME9"] = "Doomed",
        ["GLITCHBIOME10"] = "Erased",
        ["GLITCHBIOME11"] = "Temporary",
        ["GLITCHBIOME12"] = "Corrupted",
        ["GREENBIOME1"] = "Planetary Anomaly",
        ["GREENBIOME2"] = "Lost Green",
        ["GREENBIOME3"] = "[REDACTED]",
        ["GREENBIOME4"] = "Stellar Corruption Detected",
        ["GREENBIOME5"] = "Chromatic Fog",
        ["GREENBIOME6"] = "Vile Anomaly",
        ["GREENBIOME7"] = "Toxic Anomaly",
        ["GREENBIOME8"] = "Doomed Jade",
        ["GREENBIOME9"] = "Haunted Emeril",
        ["GREENBIOME10"] = "Deathly Green Anomaly",
        ["HEXAGONBIOME1"] = "Hexagonal",
        ["HEXAGONBIOME2"] = "Plated",
        ["HEXAGONBIOME3"] = "Scaly",
        ["HYDROGARDENBIOME1"] = "Fungal",
        ["HYDROGARDENBIOME2"] = "Sporal",
        ["HYDROGARDENBIOME3"] = "Capped",
        ["INFESTED1"] = "Infested",
        ["INFESTEDCOLD1"] = "Infested",
        ["INFESTEDCOLD2"] = "Icy Abhorrence",
        ["INFESTEDCOLD3"] = "Frozen Hell",
        ["INFESTEDDUST1"] = "Infested",
        ["INFESTEDDUST2"] = "Tainted",
        ["INFESTEDDUST3"] = "Infected Dustbowl",
        ["INFESTEDDUST4"] = "Terrorsphere",
        ["INFESTEDHOT1"] = "Infested",
        ["INFESTEDHOT2"] = "Boiling Doom",
        ["INFESTEDHOT3"] = "Fiery Dreadworld",
        ["INFESTEDHOT4"] = "Corrupted",
        ["INFESTEDLUSH1"] = "Infested Paradise",
        ["INFESTEDLUSH2"] = "Worm-ridden",
        ["INFESTEDLUSH3"] = "Xeno-Colony",
        ["INFESTEDLUSH4"] = "The Nest",
        ["INFESTEDRAD1"] = "Infested",
        ["INFESTEDRAD2"] = "Radioactive Abomination",
        ["INFESTEDRAD3"] = "Mutated",
        ["INFESTEDTOX1"] = "Infested",
        ["INFESTEDTOX2"] = "Toxic Horror",
        ["INFESTEDTOX3"] = "Caustic Nightmare",
        ["IRRADIATED1"] = "Irradiated",
        ["IRRADIATED2"] = "Radioactive",
        ["IRRADIATED3"] = "Contaminated",
        ["IRRADIATED4"] = "Nuclear",
        ["IRRADIATED5"] = "Isotopic",
        ["IRRADIATED6"] = "Decaying Nuclear",
        ["IRRADIATED7"] = "Gamma-Intensive",
        ["IRRADIATED8"] = "High Radio Source",
        ["IRRADIATED9"] = "Supercritical",
        ["IRRADIATED10"] = "High Energy",
        ["IRRISHELLSBIOME1"] = "Finned",
        ["IRRISHELLSBIOME2"] = "Bladed",
        ["IRRISHELLSBIOME3"] = "Shell-Strewn",
        ["LAVABIOME1"] = "Lava",
        ["LAVABIOME2"] = "Magma",
        ["LAVABIOME3"] = "Erupting",
        ["LAVABIOME4"] = "Volcanic",
        ["LAVABIOME5"] = "Ash-Shrouded",
        ["LAVABIOME6"] = "Ashen",
        ["LAVABIOME7"] = "Tectonic",
        ["LAVABIOME8"] = "Unstable",
        ["LAVABIOME9"] = "Violent",
        ["LAVABIOME10"] = "Molten",
        ["LAVABIOME11"] = "Flame-Ruptured",
        ["LAVABIOME12"] = "Imminent Core Detonation",
        ["LAVABIOME13"] = "Obsidian Bead",
        ["LAVABIOME14"] = "Basalt",
        ["LUSH1"] = "Rainy",
        ["LUSH2"] = "Verdant",
        ["LUSH3"] = "Tropical",
        ["LUSH4"] = "Viridescent",
        ["LUSH5"] = "Grassy",
        ["LUSH6"] = "Temperate",
        ["LUSH7"] = "Humid",
        ["LUSH8"] = "Overgrown",
        ["LUSH9"] = "Flourishing",
        ["LUSH10"] = "Bountiful",
        ["MSTRUCTBIOME1"] = "Ossified",
        ["MSTRUCTBIOME2"] = "Petrified",
        ["MSTRUCTBIOME3"] = "Calcified",
        ["REDBIOME1"] = "Crimson",
        ["REDBIOME2"] = "Planetary Anomaly",
        ["REDBIOME3"] = "Lost Red",
        ["REDBIOME4"] = "[REDACTED]",
        ["REDBIOME5"] = "Stellar Corruption Detected",
        ["REDBIOME6"] = "Chromatic Fog",
        ["REDBIOME7"] = "Vermillion Globe",
        ["REDBIOME8"] = "Scarlet",
        ["REDBIOME9"] = "Blood",
        ["REDBIOME10"] = "Wine Dark",
        ["RUINED1"] = "Abandoned",
        ["RUINEDCOLD1"] = "Abandoned",
        ["RUINEDCOLD2"] = "Icy Ruin",
        ["RUINEDCOLD3"] = "Frozen Relic",
        ["RUINEDDUST1"] = "Abandoned",
        ["RUINEDDUST2"] = "Ruined",
        ["RUINEDDUST3"] = "Ruined Dustbowl",
        ["RUINEDDUST4"] = "Abandoned Desert",
        ["RUINEDHOT1"] = "Ruined",
        ["RUINEDHOT2"] = "Scorched Relic",
        ["RUINEDHOT3"] = "Lost to Fire",
        ["RUINEDHOT4"] = "Abandoned Crucible",
        ["RUINEDLUSH1"] = "Abandoned Paradise",
        ["RUINEDLUSH2"] = "Overgrown",
        ["RUINEDLUSH3"] = "Overgrown Relic",
        ["RUINEDLUSH4"] = "The Reliquary",
        ["RUINEDRAD1"] = "Ruined",
        ["RUINEDRAD2"] = "Claimed by Decay",
        ["RUINEDRAD3"] = "Mutating Relic",
        ["RUINEDTOX1"] = "Abandoned",
        ["RUINEDTOX2"] = "Bleached Ruin",
        ["RUINEDTOX3"] = "Decaying Ruin",
        ["SCORCHED1"] = "Charred",
        ["SCORCHED2"] = "Arid",
        ["SCORCHED3"] = "Scorched",
        ["SCORCHED4"] = "Hot",
        ["SCORCHED5"] = "Fiery",
        ["SCORCHED6"] = "Boiling",
        ["SCORCHED7"] = "High Temperature",
        ["SCORCHED8"] = "Torrid",
        ["SCORCHED9"] = "Incandescent",
        ["SCORCHED10"] = "Scalding",
        ["SHARDSBIOME1"] = "Columned",
        ["SHARDSBIOME2"] = "Sharded",
        ["SHARDSBIOME3"] = "Pillared",
        ["SWAMPBIOME1"] = "Marshy",
        ["SWAMPBIOME2"] = "Swamp",
        ["SWAMPBIOME3"] = "Tropical",
        ["SWAMPBIOME4"] = "Foggy",
        ["SWAMPBIOME5"] = "Misty",
        ["SWAMPBIOME6"] = "Boggy",
        ["SWAMPBIOME7"] = "Endless Morass",
        ["SWAMPBIOME8"] = "Quagmire",
        ["SWAMPBIOME9"] = "Hazy",
        ["SWAMPBIOME10"] = "Cloudy",
        ["SWAMPBIOME11"] = "Vapour",
        ["SWAMPBIOME12"] = "Reeking",
        ["SWAMPBIOME13"] = "Murky",
        ["SWAMPBIOME14"] = "Damp",
        ["TOXIC1"] = "Toxic",
        ["TOXIC2"] = "Poisonous",
        ["TOXIC3"] = "Noxious",
        ["TOXIC4"] = "Corrosive",
        ["TOXIC5"] = "Acidic",
        ["TOXIC6"] = "Caustic",
        ["TOXIC7"] = "Acrid",
        ["TOXIC8"] = "Blighted",
        ["TOXIC9"] = "Miasmatic",
        ["TOXIC10"] = "Rotting",
        ["WATERWORLD1"] = "Waterworld",
        ["WATERWORLD2"] = "Drowning",
        ["WATERWORLD3"] = "Oceanic",
        ["WATERWORLD4"] = "Tidal",
        ["WATERWORLD5"] = "Waterlocked",
        ["WATERWORLD6"] = "Aquatic",
        ["WATERWORLD7"] = "Endless Seas",
        ["WATERWORLD8"] = "Marine",
        ["WIRECELLSBIOME1"] = "Shattered",
        ["WIRECELLSBIOME2"] = "Fractured",
        ["WIRECELLSBIOME3"] = "Fragmented",
    };
}

