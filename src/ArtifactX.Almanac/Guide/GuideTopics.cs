using System.Collections.Generic;

namespace ArtifactX.Almanac.Guide;

/// <summary>All 59 in-game Guide topics - GameIds and DisplayNames both come
/// directly from the game's own loc data ("UI_GUIDE_TOPIC_*" ids resolve
/// straight to their real display text, e.g. UI_GUIDE_TOPIC_SURVIVAL_1 =
/// "Life Support"), found via DataCataloger's locidall command. Unlike
/// FreighterUpgrades' curated list, every single entry here is a confirmed,
/// exact loc-key match - none of these are inferred by naming-pattern.
///
/// No section/category grouping is included - the reference tool that
/// surfaced this feature (see project_guide_page.md) groups topics under
/// headings like "Survival Basics"/"Getting Around", but ArtifactX hasn't
/// found the real topic-to-section mapping table yet (the topic ID's own
/// prefix family, e.g. SURVIVAL_1-6, does NOT reliably predict its section -
/// SURVIVAL_5 "Navigation Basics" and EXPLORATION_1 "Exploring on Foot" both
/// appeared under "Getting Around" in that tool, not their own ID-prefix's
/// section). Presented as a flat, searchable list for now rather than
/// guessing at groupings.</summary>
public static class GuideTopics
{
    public static readonly IReadOnlyList<GuideTopicInfo> All = new List<GuideTopicInfo>
    {
        new("UI_GUIDE_TOPIC_SURVIVAL_BASICS", "Getting Started"),
        new("UI_GUIDE_TOPIC_SURVIVAL_5", "Navigation Basics"),
        new("UI_GUIDE_TOPIC_SURVIVAL_2", "Hazard Protection"),
        new("UI_GUIDE_TOPIC_SURVIVAL_1", "Life Support"),
        new("UI_GUIDE_TOPIC_SURVIVAL_4", "The Multi-Tool"),
        new("UI_GUIDE_TOPIC_SURVIVAL_3", "Finding Basic Resources"),
        new("UI_GUIDE_TOPIC_SLOTS", "Inventory Management"),
        new("UI_GUIDE_TOPIC_EXPLORATION_1", "Exploring on Foot"),
        new("UI_GUIDE_TOPIC_EXPLORATION_2", "Finding Resources"),
        new("UI_GUIDE_TOPIC_TERRAIN", "The Terrain Manipulator"),
        new("UI_GUIDE_TOPIC_EXPLORATION_5", "Flying the Starship"),
        new("UI_GUIDE_TOPIC_SPACE_STATION", "Space Stations"),
        new("UI_GUIDE_TOPIC_EXPLORATION_6", "Vehicles"),
        new("UI_GUIDE_TOPIC_EXPLORATION_7", "Exocraft Races"),
        new("UI_GUIDE_TOPIC_EXPLORATION_4", "The Analysis Visor"),
        new("UI_GUIDE_TOPIC_UPLOAD", "Renaming and Uploading Discoveries"),
        new("UI_GUIDE_TOPIC_SIGNALS", "Finding Buildings"),
        new("UI_GUIDE_TOPIC_EXPLORATION_3", "Smaller Points of Interest"),
        new("UI_GUIDE_TOPIC_WEATHER", "Extreme Weather"),
        new("UI_GUIDE_TOPIC_FEEDING", "Feeding and Taming Creatures"),
        new("UI_GUIDE_TOPIC_PORTALS", "Portals"),
        new("UI_GUIDE_TOPIC_ABAND", "Derelict Freighters"),
        new("UI_GUIDE_TOPIC_PINNING", "Pinning Recipes"),
        new("UI_GUIDE_TOPIC_REFINER", "Refining Substances"),
        new("UI_GUIDE_TOPIC_TECHNOLOGY", "Upgrading your Equipment"),
        new("UI_GUIDE_TOPIC_NANITES", "Earning Nanites"),
        new("UI_GUIDE_TOPIC_RECIPES", "Recipes and Blueprints"),
        new("UI_GUIDE_TOPIC_GUNS", "Buying New Multi-Tools"),
        new("UI_GUIDE_TOPIC_SHIPS", "Buying New Ships"),
        new("UI_GUIDE_TOPIC_SALVAGE", "Salvaging Ships & Multi-Tools"),
        new("UI_GUIDE_TOPIC_BIGGS", "Corvette-Class Starships"),
        new("UI_GUIDE_TOPIC_SCRAP", "Scrap Salvaging & Industrial Waste"),
        new("UI_GUIDE_TOPIC_NEW_BASE_PARTS", "Learning New Base Parts"),
        new("UI_GUIDE_TOPIC_POWER", "Powering the Base"),
        new("UI_GUIDE_TOPIC_SILOS", "Industrial Parts"),
        new("UI_GUIDE_TOPIC_SURVIVAL_6", "Base Construction"),
        new("UI_GUIDE_TOPIC_SURVEY", "Industrial Surveying"),
        new("UI_GUIDE_TOPIC_SETTLEMENTS", "Planetary Settlements"),
        new("UI_GUIDE_TOPIC_DISMANTLE", "Portable Technology"),
        new("UI_GUIDE_TOPIC_TRADE_3A", "Trading Basics"),
        new("UI_GUIDE_TOPIC_TRADE_1", "Making Money"),
        new("UI_GUIDE_TOPIC_TRADE_2", "Farming"),
        new("UI_GUIDE_TOPIC_TRADE_6", "Crafting"),
        new("UI_GUIDE_TOPIC_TRADE_3", "Advanced Trading"),
        new("UI_GUIDE_TOPIC_TRADE_5", "The Mission Board"),
        new("UI_GUIDE_TOPIC_TRADE_4", "Freighters"),
        new("UI_GUIDE_TOPIC_TRADE_7", "Factions & Standing"),
        new("UI_GUIDE_TOPIC_GUILDS", "Guilds"),
        new("UI_GUIDE_TOPIC_WORDS", "Learning Words"),
        new("UI_GUIDE_TOPIC_PIRATES", "Outlaw Systems"),
        new("UI_GUIDE_TOPIC_BONES", "Assembling Fossil Displays"),
        new("UI_GUIDE_TOPIC_FLEETS", "Freighter Fleets"),
        new("UI_GUIDE_TOPIC_FLEETSA", "Fleet Expeditions"),
        new("UI_GUIDE_TOPIC_AMMO", "Ammunition"),
        new("UI_GUIDE_TOPIC_COMBAT_1", "Sentinels"),
        new("UI_GUIDE_TOPIC_COMBAT_2", "Weapon Management"),
        new("UI_GUIDE_TOPIC_COMBAT_3", "Defensive Systems"),
        new("UI_GUIDE_TOPIC_COMBAT_4", "Ground Combat"),
        new("UI_GUIDE_TOPIC_COMBAT_5", "Space Combat"),
    };
}
