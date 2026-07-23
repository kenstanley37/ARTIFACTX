namespace NMS.Data.Services.Parsing;

public static class ItemLocService
{
    // A clean dictionary lookup matching raw game engine strings with localized English game titles
    private static readonly Dictionary<string, string> ItemNameMap = new()
    {
        // Elements / Substances
        { "OXYGEN", "Oxygen" },
        { "AMMO", "Projectile Ammunition" },
        { "FUEL1", "Carbon" },
        { "FUEL2", "Condensed Carbon" },
        { "RED2", "Cadmium" },
        { "YELLOW2", "Copper" },
        { "PURPM_GAS", "Radon" },
        { "LAND1", "Ferrite Dust" },
        { "LAND2", "Pure Ferrite" },
        { "LAND3", "Magnetised Ferrite" },
        { "SAND1", "Silicate Powder" },
        { "CATALYST1", "Sodium" },
        { "CATALYST2", "Sodium Nitrate" },
        { "PLANT_LUS", "Gamma Root" },
        { "PLANT_POO", "Faecium" },

        // Trading Alloys & Curios
        { "TRA_ALLOY", "LemMIUM" },
        { "ALLOY1", "Herox" },
        { "TRA_COMMO", "Commodity Component" },
        { "TRA_COMPO", "Trading Component" },
        { "TRA_MINER", "Mineral Extract" },
        { "TRA_TECH1", "Technical Micro-Component" },
        { "TRA_TECH5", "Advanced Tech Unit" },
        { "TRA_CURIO", "Curio Artifact" },
        { "WAR_CURIO", "Vy'keen Dagger" },

        // Tech Items & Upgrades
        { "ANTIMATTE", "Antimatter" },
        { "SHIP_CORE", "Starship Core" },
        { "BUILD_REF", "Portable Refiner" },
        { "NAV_DATA", "Navigation Data" },
        { "REPAIRKIT", "Repair Kit" },
        { "LAUNCHFUE", "Starship Launch Fuel" },
        { "POWERCELL", "Starship Shield Battery" },
        { "SHIPCHARG:6", "Pulse Engine Upgrade" },
        { "SIGNALCHA", "Signal Booster" },
        { "TECH_COMP", "Wiring Loom" },
        { "HYPERFUEL", "Warp Cell" },
        { "SUIT_INV_", "Exosuit Slot Expansion" },
        { "SHIP_INV_", "Starship Slot Expansion" },
        { "WEAP_INV_", "Multi-Tool Slot Expansion" },
        { "FACT_TOKE", "Factory Override Unit" },
        { "FRIG_TOKE", "Salvaged Frigate Module" },
        { "BP_SALVAG", "Salvaged Technology Data" },
        { "STORM_CRY", "Storm Crystal" },

        // Curiosities, Baits & Missions
        { "FISHBAIT_", "Targeted Bait Flakes" },
        { "BAIT_BASI", "Salty Juice / Bread" },
        { "FOOD_M_DI", "DeASTEROID Doughnut" },
        { "FOOD_V_DI", "Vegetable Stew" },
        { "FOOD_STEW", "Nutritious Stew" },
        { "ARTIFACT_", "Ancient Key" },
        { "CHART_PB_", "Emergency Cartographic Chart" },
        { "ABAND_LOC", "Abandoned Building Coordinates" },
        { "GEODE_LAN", "Geode Fragment" },
        { "FISHCORE", "Abyssal Eye" },
        { "MECH_PROD", "Walker Brain" },
        { "HEXCORE", "Hex Core" },
        { "DRONE_FRI", "Friendly Drone Shell" },
        { "EGG1", "Companion Egg" },
        { "CLAMPEARL", "Hadarian Core / Pearl" },
        { "STARCHART", "Secret Cartographic Chart" },
        { "SENTINEL_", "Salvaged Glass" },
        { "WORMPROD", "Fleshrope" },

        // Sentinel / Specialized Scrap Elements
        { "FOS_BI", "Prehistoric Bone" },
        { "FOS_QUAD", "Quad Remains" },
        { "FOS_BIRD", "Fossilized Beak" },
        { "FOS_WORM", "Fossilized Carapace" },
        { "FOS_GRUN", "Fossilized Ribcage" },
        { "FOS_LIMBS", "Fossilized Vertebrae" },
        { "FOS_HEAD_", "Fossilized Skull" },

        // Procedural Upgrade Custom Tags
        { "PROC_CAPT", "Captain's Log Fragment" },
        { "PROC_CREW", "Crew Manifest Record" },
        { "PROC_TOOL", "Suspicious Multi-Tool Module" },
        { "PROC_BIO#", "Anomalous Flesh Fragment" },
        { "PROC_SEA#", "Sunken Cargo Manifest" },
        { "PROC_HIST", "Historical Document" },
        { "PROC_SALV", "Recovered Scrap Data" },
        { "PROC_STAR", "Suspicious Ship Module" }
    };

    /// <summary>
    /// Translates an obfuscated internal save-key into a human-readable layout name.
    /// </summary>
    public static string ResolveName(string rawKey)
    {
        if (string.IsNullOrEmpty(rawKey)) return "Empty Space";

        // Try exact dictionary pull
        if (ItemNameMap.TryGetValue(rawKey.ToUpper().Trim(), out string cleanName))
        {
            return cleanName;
        }

        // Fallback context: cleanly scrub custom proc module markers if keys contain procedurally variable data
        foreach (var key in ItemNameMap.Keys)
        {
            if (rawKey.ToUpper().StartsWith(key))
            {
                return ItemNameMap[key];
            }
        }

        // Ultimate safety backup: return structured raw identity if dictionary isn't populated yet
        return $"Unknown ({rawKey})";
    }
}