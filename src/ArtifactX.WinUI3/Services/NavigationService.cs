using ArtifactX.WinUI3.Views.AccountPages;
using ArtifactX.WinUI3.Views.AppPages;
using ArtifactX.WinUI3.Views.ExplorePages;
using ArtifactX.WinUI3.Views.InspectionPages;
using ArtifactX.WinUI3.Views.ProgressionPages;
using ArtifactX.WinUI3.Views.ReferencePages;
using ArtifactX.WinUI3.Views.SaveEditorPages;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Central routing table mapping NavigationView item tags to page types and back.
/// Consolidates the tag-to-page and page-to-tag switch tables previously duplicated in MainWindow.
/// </summary>
public static class NavigationService
{
    private static readonly Dictionary<string, Type> TagToPage = new(StringComparer.Ordinal)
    {
        ["SaveFolderSelect"] = typeof(SaveFolderSelectPage),
        ["General"] = typeof(GeneralPage),
        ["Exosuit"] = typeof(ExosuitPage),
        ["MultiTool"] = typeof(MultiToolPage),
        ["Ships"] = typeof(ShipsPage),
        ["ShipDesigner"] = typeof(ShipDesignerPage),
        ["Freighter"] = typeof(FreighterPage),
        ["FreighterUpgrades"] = typeof(FreighterUpgradesPage),
        ["Frigate"] = typeof(FrigatePage),
        ["Squadron"] = typeof(SquadronPage),
        ["Cargo"] = typeof(CargoPage),
        ["Companions"] = typeof(CompanionsPage),
        ["Settlement"] = typeof(SettlementPage),
        ["SpaceStations"] = typeof(SpaceStationsPage),
        ["Planets"] = typeof(CurrentSystemPage),
        ["PlanetDetails"] = typeof(PlanetDetailsPage),
        ["CrashedShips"] = typeof(CrashedShipsPage),
        ["CabinetMultiTools"] = typeof(CabinetMultiToolsPage),
        ["PlanetFinder"] = typeof(PlanetFinderPage),
        ["Bases"] = typeof(BasesPage),
        ["BaseDetails"] = typeof(BaseDetailsPage),
        ["Finds"] = typeof(FindsPage),
        ["Fishing"] = typeof(FishingPage),
        ["Recipes"] = typeof(CookingRecipesPage),
        ["RefinerRecipes"] = typeof(RefinerRecipesPage),
        ["Crafting"] = typeof(CraftingPage),
        ["IndustrialCleanup"] = typeof(IndustrialCleanupPage),
        ["Guides"] = typeof(GuidesPage),
        ["Credits"] = typeof(CreditsPage),
        ["Settings"] = typeof(SettingsPage),
        ["ItemValues"] = typeof(ItemValuesPage),
        ["EggSequencer"] = typeof(EggSequencerPage),
        ["NearCore"] = typeof(NearCorePage),
        ["GalaxyTransfer"] = typeof(GalaxyTransferPage),
        ["Regions"] = typeof(RegionsPage),
        ["Milestones"] = typeof(MilestonesPage),
        ["Alliance"] = typeof(AlliancePage),
        ["Guide"] = typeof(GuidePage),
        ["ExpeditionRewards"] = typeof(ExpeditionRewardsPage),
        ["TwitchRewards"] = typeof(TwitchRewardsPage),
        ["Catalogue"] = typeof(CataloguePage),
        ["GameCatalog"] = typeof(GameCatalogPage),
        ["FishingRecords"] = typeof(FishingRecordsPage),
        ["Exocraft"] = typeof(ExocraftPage),
        ["LanguageGek"] = typeof(GekLanguagePage),
        ["LanguageVyKeen"] = typeof(VyKeenLanguagePage),
        ["LanguageKorvax"] = typeof(KorvaxLanguagePage),
        ["LanguageAutophage"] = typeof(AutophageLanguagePage),
        ["LanguageAtlas"] = typeof(AtlasLanguagePage),
        ["AncestrySearch"] = typeof(AncestrySearchView),
        ["HGDecryption"] = typeof(HGDecryptionPage),
    };

    private static readonly Dictionary<Type, string> PageToNavTag = BuildPageToNavTag();

    private static Dictionary<Type, string> BuildPageToNavTag()
    {
        var map = new Dictionary<Type, string>();
        foreach (var (tag, pageType) in TagToPage)
            map[pageType] = tag;

        // Detail sub-pages highlight their parent navigation item in the sidebar.
        map[typeof(PlanetDetailsPage)] = "Planets";
        map[typeof(BaseDetailsPage)] = "Bases";
        return map;
    }

    /// <summary>Resolves a NavigationViewItem Tag string to its corresponding Page type.</summary>
    public static Type ResolvePageType(string? tag) =>
        tag is not null && TagToPage.TryGetValue(tag, out var pageType)
            ? pageType
            : typeof(SaveFolderSelectPage);

    /// <summary>Resolves a navigated Page type to the sidebar NavigationViewItem Tag to select.</summary>
    public static string ResolveNavTag(Type? pageType) =>
        pageType is not null && PageToNavTag.TryGetValue(pageType, out var tag)
            ? tag
            : string.Empty;

    /// <summary>Recursively finds a NavigationViewItem matching the given Tag string.</summary>
    public static NavigationViewItem? FindNavItemByTag(IList<object> itemsList, string targetTag)
    {
        foreach (var menuNode in itemsList)
        {
            if (menuNode is NavigationViewItem item)
            {
                if (item.Tag?.ToString() == targetTag)
                    return item;

                if (item.MenuItems.Count > 0)
                {
                    var foundSubChild = FindNavItemByTag(item.MenuItems, targetTag);
                    if (foundSubChild != null) return foundSubChild;
                }
            }
        }
        return null;
    }
}
