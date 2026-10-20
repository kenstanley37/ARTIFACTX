using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.Controls;

public sealed record CustomItemResult(
    string ItemId,
    string CategoryLabel,
    int Amount,
    int MaxAmount);

public static class CustomItemDialog
{
    private sealed record ItemPreset(
        string DisplayName,
        string CategoryGroup,
        string GameId,
        string CategoryLabel,
        bool Seeded,
        int DefaultAmount,
        int DefaultMax,
        string Description);

    private static readonly List<ItemPreset> Presets = new()
    {
        // Fossils & Bones
        new("Excavated Bones", "Fossils & Bones", "PROC_FOSSIL", "Product", true, 1, 1, "Ancient skeletal remains with procedurally rolled species, age, and value."),
        new("Space Bones", "Fossils & Bones", "PROC_S_BONE", "Product", true, 1, 1, "Rare organic skeleton harvested from deep space anomalies."),
        new("Exhibit Fossil", "Fossils & Bones", "PROC_EXH_FOSS", "Product", true, 1, 1, "Museum-grade fossil specimen suitable for exhibition."),

        // Historical Lore & Ancient Documents
        new("Historical Documents", "Historical & Lore", "PROC_HIST", "Product", true, 1, 1, "Ancient texts and parchment revealing galactic history."),
        new("Unearthed Treasure / Ancient Artifact", "Historical & Lore", "PROC_LOOT", "Product", true, 1, 1, "Unearthed planetary treasures, ancient chests, and delicate relics."),
        new("Message in a Bottle", "Historical & Lore", "PROC_BOTTLE", "Product", true, 1, 1, "Faded message recovered from ocean depths or planetary shores."),

        // Biological & Alien Specimens
        new("Biological Sample", "Biological & Specimens", "PROC_BIO", "Product", true, 1, 1, "Preserved tissue or genetic material from aberrant creatures."),
        new("Horrific Specimen", "Biological & Specimens", "PROC_HORROR", "Product", true, 1, 1, "Chilling biological matter harvested from infested worlds."),
        new("Sea Horror Specimen", "Biological & Specimens", "PROC_S_HORROR", "Product", true, 1, 1, "Abyssal specimen recovered from deep oceanic trenches."),
        new("Alien Plant Sample", "Biological & Specimens", "PROC_PLNT", "Product", true, 1, 1, "Rare botanical flora sample from exotic biomes."),

        // Derelict Freighter Tech & Logs
        new("Freighter Captain's Log", "Derelict Freighter", "PROC_CREW", "Product", true, 1, 1, "Personal captain's log detailing the fate of a lost vessel."),
        new("Freighter Crew Manifest", "Derelict Freighter", "PROC_CAPT", "Product", true, 1, 1, "Personnel registry documenting lost derelict crew."),
        new("Freighter Security Password", "Derelict Freighter", "PROC_PASS", "Product", true, 1, 1, "Derelict freighter access encryption code."),
        new("Salvaged Hyperdrive Unit", "Derelict Freighter", "PROC_TECH_HYP", "Product", true, 1, 1, "Procedurally rolled fleet hyperdrive upgrade module."),
        new("Salvaged Fleet Efficiency", "Derelict Freighter", "PROC_TECH_FUEL", "Product", true, 1, 1, "Derelict freighter fuel optimization technology module."),
        new("Salvaged Fleet Beacon", "Derelict Freighter", "PROC_TECH_EXP", "Product", true, 1, 1, "Derelict expedition coordination technology module."),
        new("Salvaged Fleet Speed Unit", "Derelict Freighter", "PROC_TECH_SPD", "Product", true, 1, 1, "Derelict freighter fleet speed upgrade module."),
        new("Salvaged Mining Unit", "Derelict Freighter", "PROC_TECH_MINE", "Product", true, 1, 1, "Derelict freighter industrial mining technology module."),
        new("Salvaged Combat Unit", "Derelict Freighter", "PROC_TECH_COMB", "Product", true, 1, 1, "Derelict freighter combat support technology module."),
        new("Salvaged Trade Unit", "Derelict Freighter", "PROC_TECH_TRAD", "Product", true, 1, 1, "Derelict freighter trade terminal technology module."),

        // Aquatic Salvage & Scrap
        new("Lost Ocean Treasure", "Aquatic & Scrap", "PROC_SEA", "Product", true, 1, 1, "Submerged valuables recovered from sunken starships and ruins."),
        new("Salvaged Scrap", "Aquatic & Scrap", "PROC_SALV", "Product", true, 1, 1, "Mechanical scrap components recovered from sentinel salvages."),
        new("Dismantled Tech Unit", "Aquatic & Scrap", "PROC_TECH", "Product", true, 1, 1, "Recovered modular technology component."),
        new("Dismantled Bio Unit", "Aquatic & Scrap", "PROC_BIO2", "Product", true, 1, 1, "Recovered biological unit."),
        new("Dismantled Data Unit", "Aquatic & Scrap", "PROC_DATA", "Product", true, 1, 1, "Recovered data memory core."),

        // Expedition Rewards & Unique Curiosities
        new("Timeless Ring", "Expedition & Curiosities", "EX_RING", "Product", false, 1, 1, "Expedition reward: mysterious ring existing outside chronological space."),
        new("Remembrance", "Expedition & Curiosities", "REMEMBRANCE", "Product", false, 1, 1, "Heart of the Atlas: holds the memories of ancient traveler iterations."),
        new("Star Seed", "Expedition & Curiosities", "STAR_SEED", "Product", false, 1, 1, "Gift of the Atlas: reveals the locations of celestial black holes."),
        new("Void Egg", "Expedition & Curiosities", "VOIDEGG", "Product", false, 1, 1, "Anomalous bio-organic egg required to birth a Living Ship."),
        new("Suspicious Packet (Arms)", "Expedition & Curiosities", "SCRAP_WEAP", "Product", false, 1, 10, "Contraband packet containing black-market weapon tech."),
        new("Suspicious Packet (Tech)", "Expedition & Curiosities", "SCRAP_TECH", "Product", false, 1, 10, "Contraband packet containing black-market technology."),
        new("Suspicious Packet (Goods)", "Expedition & Curiosities", "SCRAP_GOODS", "Product", false, 1, 10, "Contraband packet containing illegal trade goods."),
    };

    public static async Task<CustomItemResult?> ShowAsync(
        XamlRoot root,
        InventorySlotViewModel? existingCell,
        int defaultMaxAmount = 1)
    {
        // Pre-warm cache for all presets so icons and names are instantly ready
        _ = CatalogService.WarmCacheAsync(Presets.Select(p => p.GameId));

        string existingRawId = existingCell?.ItemId?.TrimStart('^') ?? "";
        int hashIdx = existingRawId.IndexOf('#');
        string initialBaseId = hashIdx >= 0 ? existingRawId[..hashIdx] : existingRawId;
        int? existingSeed = hashIdx >= 0 && int.TryParse(existingRawId[(hashIdx + 1)..], out int s) ? s : null;

        // Default to first preset if empty, or match existing
        var matchedPreset = Presets.FirstOrDefault(p => string.Equals(p.GameId, initialBaseId, StringComparison.OrdinalIgnoreCase));
        bool isInitiallySeeded = existingSeed.HasValue || matchedPreset?.Seeded == true || initialBaseId.StartsWith("PROC_", StringComparison.OrdinalIgnoreCase);
        int initialSeed = existingSeed ?? Random.Shared.Next(10000, 100000);
        string initialCategory = existingCell?.CategoryLabel ?? matchedPreset?.CategoryLabel ?? "Product";
        int initialAmount = existingCell?.Amount > 0 ? existingCell.Amount : matchedPreset?.DefaultAmount ?? 1;
        int initialMax = existingCell?.MaxAmount > 0 ? existingCell.MaxAmount : matchedPreset?.DefaultMax ?? defaultMaxAmount;

        var selectedPreset = matchedPreset ?? Presets[0];
        string currentBaseId = string.IsNullOrEmpty(initialBaseId) ? selectedPreset.GameId : initialBaseId;

        var panel = new StackPanel { Spacing = 10, Width = 460 };

        // 1. Search Box & Category Filter
        var searchHeader = new TextBlock
        {
            Text = "Select a Curiosity or Procedural Item",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
        };
        panel.Children.Add(searchHeader);

        var filterRow = new Grid { ColumnSpacing = 8 };
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

        var searchBox = new TextBox
        {
            PlaceholderText = "Search by name (e.g. bones, log, egg)...",
        };
        var categoryFilterCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        categoryFilterCombo.Items.Add("All Categories");
        foreach (string group in Presets.Select(p => p.CategoryGroup).Distinct())
            categoryFilterCombo.Items.Add(group);
        categoryFilterCombo.SelectedIndex = 0;

        Grid.SetColumn(searchBox, 0);
        Grid.SetColumn(categoryFilterCombo, 1);
        filterRow.Children.Add(searchBox);
        filterRow.Children.Add(categoryFilterCombo);
        panel.Children.Add(filterRow);

        // 2. Scrollable Items ListView
        var itemsList = new ListView
        {
            Height = 170,
            SelectionMode = ListViewSelectionMode.Single,
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeResources.Brush("ThemeSlotBorderBrush"),
            CornerRadius = new CornerRadius(4),
            Background = ThemeResources.Brush("ThemeSlotEmptyBrush"),
        };
        panel.Children.Add(itemsList);

        // 3. Selected Item Card (Visual Display)
        var selectedItemCard = new Border
        {
            Background = ThemeResources.Brush("ThemePanelAltBrush"),
            BorderBrush = ThemeResources.Brush("ThemeAccentBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
        };

        var cardGrid = new Grid { ColumnSpacing = 12 };
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var selectedIcon = new Image { Width = 44, Height = 44, VerticalAlignment = VerticalAlignment.Center };
        var cardTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        var selectedNameTxt = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var selectedDescTxt = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
        };
        cardTextStack.Children.Add(selectedNameTxt);
        cardTextStack.Children.Add(selectedDescTxt);

        Grid.SetColumn(selectedIcon, 0);
        Grid.SetColumn(cardTextStack, 1);
        cardGrid.Children.Add(selectedIcon);
        cardGrid.Children.Add(cardTextStack);
        selectedItemCard.Child = cardGrid;
        panel.Children.Add(selectedItemCard);

        // 4. Seed & Randomizer Section
        var seedSection = new Border
        {
            Background = ThemeResources.Brush("ThemeSlotEmptyBrush"),
            BorderBrush = ThemeResources.Brush("ThemeSlotBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
        };
        var seedStack = new StackPanel { Spacing = 6 };

        var seedCheckRow = new Grid();
        seedCheckRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        seedCheckRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var seededCheck = new CheckBox
        {
            Content = "Procedural Generation Seed (#seed)",
            IsChecked = isInitiallySeeded,
        };
        var randomizeBtn = new Button
        {
            Content = "🎲 Randomize Seed",
            IsEnabled = isInitiallySeeded,
        };
        Grid.SetColumn(seededCheck, 0);
        Grid.SetColumn(randomizeBtn, 1);
        seedCheckRow.Children.Add(seededCheck);
        seedCheckRow.Children.Add(randomizeBtn);
        seedStack.Children.Add(seedCheckRow);

        var seedBox = new NumberBox
        {
            Header = "Seed value (determines generated traits, name, and value in-game)",
            Minimum = 1,
            Maximum = 999999999,
            Value = initialSeed,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            IsEnabled = isInitiallySeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        seedStack.Children.Add(seedBox);
        seedSection.Child = seedStack;
        panel.Children.Add(seedSection);

        randomizeBtn.Click += (_, _) =>
        {
            seedBox.Value = Random.Shared.Next(10000, 100000);
        };
        seededCheck.Checked += (_, _) =>
        {
            seedBox.IsEnabled = true;
            randomizeBtn.IsEnabled = true;
            if (double.IsNaN(seedBox.Value) || seedBox.Value <= 0)
                seedBox.Value = Random.Shared.Next(10000, 100000);
        };
        seededCheck.Unchecked += (_, _) =>
        {
            seedBox.IsEnabled = false;
            randomizeBtn.IsEnabled = false;
        };

        // 5. Quantity & Max Stack
        var qtyRow = new Grid { ColumnSpacing = 12 };
        qtyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        qtyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var amountBox = new NumberBox
        {
            Header = "Amount",
            Minimum = 1,
            Maximum = 9999,
            Value = initialAmount,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var maxAmountBox = new NumberBox
        {
            Header = "Max Stack",
            Minimum = 1,
            Maximum = 9999,
            Value = initialMax,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(amountBox, 0);
        Grid.SetColumn(maxAmountBox, 1);
        qtyRow.Children.Add(amountBox);
        qtyRow.Children.Add(maxAmountBox);
        panel.Children.Add(qtyRow);

        // 6. Advanced / Manual ID Entry (Collapsed by default)
        var advancedToggle = new CheckBox
        {
            Content = "Advanced: View / Edit Raw Game ID",
            FontSize = 12,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
        };
        panel.Children.Add(advancedToggle);

        var advancedPanel = new StackPanel { Spacing = 6, Visibility = Visibility.Collapsed };
        var idBox = new TextBox
        {
            Header = "Game ID",
            Text = currentBaseId,
            PlaceholderText = "e.g. PROC_FOSSIL, EX_RING...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var categoryCombo = new ComboBox
        {
            Header = "Category Label",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        categoryCombo.Items.Add("Product");
        categoryCombo.Items.Add("Substance");
        categoryCombo.Items.Add("Technology");
        categoryCombo.SelectedItem = initialCategory;

        var saveIdPreview = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
        };
        advancedPanel.Children.Add(idBox);
        advancedPanel.Children.Add(categoryCombo);
        advancedPanel.Children.Add(saveIdPreview);
        panel.Children.Add(advancedPanel);

        advancedToggle.Checked += (_, _) => advancedPanel.Visibility = Visibility.Visible;
        advancedToggle.Unchecked += (_, _) => advancedPanel.Visibility = Visibility.Collapsed;

        string GetConstructedId()
        {
            string id = idBox.Text.Trim();
            if (seededCheck.IsChecked == true && !double.IsNaN(seedBox.Value) && seedBox.Value > 0)
                return $"{id}#{(long)seedBox.Value}";
            return id;
        }

        async void RefreshCardDisplay(string gameId, string displayName, string description)
        {
            saveIdPreview.Text = $"Save string: ^{GetConstructedId()}";

            var entry = CatalogService.TryGet(gameId);
            if (entry is null)
            {
                await CatalogService.WarmCacheAsync(new[] { gameId });
                entry = CatalogService.TryGet(gameId);
            }

            selectedNameTxt.Text = entry?.DisplayName is { Length: > 0 } ? entry.DisplayName : displayName;
            selectedDescTxt.Text = description;
            selectedIcon.Source = entry?.Icon;
        }

        void PopulateList()
        {
            string filterText = searchBox.Text.Trim();
            string selectedCategory = categoryFilterCombo.SelectedItem as string ?? "All Categories";

            itemsList.Items.Clear();

            var filtered = Presets.Where(p =>
            {
                if (selectedCategory != "All Categories" && p.CategoryGroup != selectedCategory)
                    return false;
                if (filterText.Length > 0 &&
                    !p.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase) &&
                    !p.Description.Contains(filterText, StringComparison.OrdinalIgnoreCase) &&
                    !p.GameId.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }).ToList();

            foreach (var preset in filtered)
            {
                var row = new Grid { ColumnSpacing = 10, Padding = new Thickness(4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new Image { Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Center };
                var entry = CatalogService.TryGet(preset.GameId);
                if (entry?.Icon is not null)
                    icon.Source = entry.Icon;

                var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameText = new TextBlock
                {
                    Text = preset.DisplayName,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                var categoryText = new TextBlock
                {
                    Text = preset.CategoryGroup,
                    FontSize = 10,
                    Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
                };
                textCol.Children.Add(nameText);
                textCol.Children.Add(categoryText);

                Grid.SetColumn(icon, 0);
                Grid.SetColumn(textCol, 1);
                row.Children.Add(icon);
                row.Children.Add(textCol);

                var item = new ListViewItem { Content = row, Tag = preset };
                itemsList.Items.Add(item);

                if (string.Equals(preset.GameId, currentBaseId, StringComparison.OrdinalIgnoreCase))
                    itemsList.SelectedItem = item;
            }

            if (itemsList.SelectedItem is null && itemsList.Items.Count > 0)
                itemsList.SelectedIndex = 0;
        }

        itemsList.SelectionChanged += (_, _) =>
        {
            if (itemsList.SelectedItem is ListViewItem { Tag: ItemPreset preset })
            {
                currentBaseId = preset.GameId;
                idBox.Text = preset.GameId;
                categoryCombo.SelectedItem = preset.CategoryLabel;
                seededCheck.IsChecked = preset.Seeded;
                if (preset.Seeded && (double.IsNaN(seedBox.Value) || seedBox.Value <= 0))
                    seedBox.Value = Random.Shared.Next(10000, 100000);
                amountBox.Value = preset.DefaultAmount;
                maxAmountBox.Value = preset.DefaultMax;
                RefreshCardDisplay(preset.GameId, preset.DisplayName, preset.Description);
            }
        };

        searchBox.TextChanged += (_, _) => PopulateList();
        categoryFilterCombo.SelectionChanged += (_, _) => PopulateList();

        idBox.TextChanged += (_, _) =>
        {
            saveIdPreview.Text = $"Save string: ^{GetConstructedId()}";
        };
        seedBox.ValueChanged += (_, _) =>
        {
            saveIdPreview.Text = $"Save string: ^{GetConstructedId()}";
        };

        // Initial setup
        PopulateList();
        RefreshCardDisplay(selectedPreset.GameId, selectedPreset.DisplayName, selectedPreset.Description);

        var dialog = new ContentDialog
        {
            Title = existingCell != null ? "Edit Curiosity / Procedural Item" : "Add Curiosity or Procedural Item",
            Content = panel,
            PrimaryButtonText = existingCell != null ? "Save changes" : "Add item",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root,
        };

        var dialogResult = await dialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return null;

        string finalId = GetConstructedId();
        if (string.IsNullOrWhiteSpace(finalId))
            return null;

        string finalCategory = categoryCombo.SelectedItem as string ?? "Product";
        int finalAmount = double.IsNaN(amountBox.Value) || amountBox.Value < 1 ? 1 : (int)amountBox.Value;
        int finalMax = double.IsNaN(maxAmountBox.Value) || maxAmountBox.Value < 1 ? 1 : (int)maxAmountBox.Value;
        if (finalAmount > finalMax)
            finalMax = finalAmount;

        return new CustomItemResult(finalId, finalCategory, finalAmount, finalMax);
    }
}
