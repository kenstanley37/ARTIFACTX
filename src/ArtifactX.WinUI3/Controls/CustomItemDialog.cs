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
        string GameId,
        string CategoryLabel,
        bool Seeded,
        int DefaultAmount,
        int DefaultMax);

    private static readonly List<ItemPreset> Presets = new()
    {
        new("Excavated Bones & Fossils (PROC_FOSSIL)", "PROC_FOSSIL", "Product", true, 1, 1),
        new("Historical Documents & Lost Lore (PROC_HIST)", "PROC_HIST", "Product", true, 1, 1),
        new("Biological Samples (PROC_BIO)", "PROC_BIO", "Product", true, 1, 1),
        new("Aquatic & Ocean Salvage (PROC_SEA)", "PROC_SEA", "Product", true, 1, 1),
        new("Sea Horror Specimen (PROC_S_HORROR)", "PROC_S_HORROR", "Product", true, 1, 1),
        new("Alien Plants & Flora Samples (PROC_PLNT)", "PROC_PLNT", "Product", true, 1, 1),
        new("Ancient Artifacts & Valuables (PROC_LOOT)", "PROC_LOOT", "Product", true, 1, 1),
        new("Salvaged Scrap & Tech (PROC_SALV)", "PROC_SALV", "Product", true, 1, 1),
        new("Space Bones & Remnants (PROC_S_BONE)", "PROC_S_BONE", "Product", true, 1, 1),
        new("Derelict Freighter Captain's Log (PROC_CREW)", "PROC_CREW", "Product", true, 1, 1),
        new("Derelict Freighter Crew Manifest (PROC_CAPT)", "PROC_CAPT", "Product", true, 1, 1),
        new("Derelict Freighter Hyperdrive Tech (PROC_TECH_HYP)", "PROC_TECH_HYP", "Product", true, 1, 1),
        new("Derelict Freighter Efficiency Tech (PROC_TECH_FUEL)", "PROC_TECH_FUEL", "Product", true, 1, 1),
        new("Derelict Freighter Fleet Tech (PROC_TECH_EXP)", "PROC_TECH_EXP", "Product", true, 1, 1),
        new("Derelict Freighter Mining Tech (PROC_TECH_MINE)", "PROC_TECH_MINE", "Product", true, 1, 1),
        new("Derelict Freighter Combat Tech (PROC_TECH_COMB)", "PROC_TECH_COMB", "Product", true, 1, 1),
        new("Derelict Freighter Trade Tech (PROC_TECH_TRAD)", "PROC_TECH_TRAD", "Product", true, 1, 1),
        new("Derelict Freighter Speed Tech (PROC_TECH_SPD)", "PROC_TECH_SPD", "Product", true, 1, 1),
        new("Expedition: Timeless Ring (EX_RING)", "EX_RING", "Product", false, 1, 1),
        new("Expedition: Remembrance (REMEMBRANCE)", "REMEMBRANCE", "Product", false, 1, 1),
        new("Expedition: Star Seed (STAR_SEED)", "STAR_SEED", "Product", false, 1, 1),
    };

    public static async Task<CustomItemResult?> ShowAsync(
        XamlRoot root,
        InventorySlotViewModel? existingCell,
        int defaultMaxAmount = 1)
    {
        string existingRawId = existingCell?.ItemId?.TrimStart('^') ?? "";
        int hashIdx = existingRawId.IndexOf('#');
        string initialBaseId = hashIdx >= 0 ? existingRawId[..hashIdx] : existingRawId;
        int? existingSeed = hashIdx >= 0 && int.TryParse(existingRawId[(hashIdx + 1)..], out int s) ? s : null;
        bool isInitiallySeeded = existingSeed.HasValue || initialBaseId.StartsWith("PROC_", StringComparison.OrdinalIgnoreCase);
        int initialSeed = existingSeed ?? Random.Shared.Next(10000, 100000);
        string initialCategory = existingCell?.CategoryLabel ?? "Product";
        int initialAmount = existingCell?.Amount > 0 ? existingCell.Amount : 1;
        int initialMax = existingCell?.MaxAmount > 0 ? existingCell.MaxAmount : defaultMaxAmount;

        var panel = new StackPanel { Spacing = 12, Width = 380 };

        // 1. Preset Dropdown
        var presetCombo = new ComboBox
        {
            Header = "Curiosity / Preset",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        presetCombo.Items.Add("(Custom item)");
        foreach (var p in Presets)
            presetCombo.Items.Add(p.DisplayName);

        int matchingPresetIdx = Presets.FindIndex(p => string.Equals(p.GameId, initialBaseId, StringComparison.OrdinalIgnoreCase));
        presetCombo.SelectedIndex = matchingPresetIdx >= 0 ? matchingPresetIdx + 1 : 0;

        panel.Children.Add(presetCombo);

        // 2. Item ID
        var idBox = new TextBox
        {
            Header = "Item ID",
            Text = initialBaseId,
            PlaceholderText = "e.g. PROC_FOSSIL, EX_RING...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.Children.Add(idBox);

        // 3. Seed Section
        var seededCheck = new CheckBox
        {
            Content = "Include procedural seed (#seed)",
            IsChecked = isInitiallySeeded,
        };
        panel.Children.Add(seededCheck);

        var seedBox = new NumberBox
        {
            Header = "Seed",
            Minimum = 1,
            Maximum = 999999999,
            Value = initialSeed,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
            IsEnabled = isInitiallySeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var randomizeBtn = new Button
        {
            Content = "🎲 Randomize",
            VerticalAlignment = VerticalAlignment.Bottom,
            IsEnabled = isInitiallySeeded,
        };
        randomizeBtn.Click += (_, _) =>
        {
            seedBox.Value = Random.Shared.Next(10000, 100000);
        };

        var seedPanel = new Grid { ColumnSpacing = 8 };
        seedPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        seedPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(seedBox, 0);
        Grid.SetColumn(randomizeBtn, 1);
        seedPanel.Children.Add(seedBox);
        seedPanel.Children.Add(randomizeBtn);
        panel.Children.Add(seedPanel);

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

        // 4. Category & Quantities
        var categoryCombo = new ComboBox
        {
            Header = "Category",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        categoryCombo.Items.Add("Product");
        categoryCombo.Items.Add("Substance");
        categoryCombo.Items.Add("Technology");
        categoryCombo.SelectedItem = initialCategory;
        panel.Children.Add(categoryCombo);

        var qtyPanel = new Grid { ColumnSpacing = 12 };
        qtyPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        qtyPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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
        qtyPanel.Children.Add(amountBox);
        qtyPanel.Children.Add(maxAmountBox);
        panel.Children.Add(qtyPanel);

        // 5. Live Preview
        var previewCard = new Border
        {
            Background = ThemeResources.Brush("ThemeSlotEmptyBrush"),
            BorderBrush = ThemeResources.Brush("ThemeSlotBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 4, 0, 0),
        };

        var previewContent = new Grid { ColumnSpacing = 10 };
        previewContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        previewContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var previewIcon = new Image { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        var previewTitle = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var previewSub = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        textStack.Children.Add(previewTitle);
        textStack.Children.Add(previewSub);

        Grid.SetColumn(previewIcon, 0);
        Grid.SetColumn(textStack, 1);
        previewContent.Children.Add(previewIcon);
        previewContent.Children.Add(textStack);
        previewCard.Child = previewContent;
        panel.Children.Add(previewCard);

        string GetConstructedId()
        {
            string id = idBox.Text.Trim();
            if (seededCheck.IsChecked == true && !double.IsNaN(seedBox.Value) && seedBox.Value > 0)
                return $"{id}#{(long)seedBox.Value}";
            return id;
        }

        async void UpdatePreview()
        {
            string rawId = idBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawId))
            {
                previewTitle.Text = "(Enter an item ID)";
                previewSub.Text = "";
                previewIcon.Source = null;
                return;
            }

            string fullId = GetConstructedId();
            previewSub.Text = $"Save ID: ^{fullId}";

            var entry = CatalogService.TryGet(rawId);
            if (entry is null)
            {
                await CatalogService.WarmCacheAsync(new[] { rawId });
                entry = CatalogService.TryGet(rawId);
            }

            if (entry is not null)
            {
                previewTitle.Text = entry.DisplayName;
                previewIcon.Source = entry.Icon;
            }
            else
            {
                previewTitle.Text = rawId;
                previewIcon.Source = null;
            }
        }

        presetCombo.SelectionChanged += (_, _) =>
        {
            if (presetCombo.SelectedIndex > 0 && presetCombo.SelectedIndex <= Presets.Count)
            {
                var preset = Presets[presetCombo.SelectedIndex - 1];
                idBox.Text = preset.GameId;
                categoryCombo.SelectedItem = preset.CategoryLabel;
                seededCheck.IsChecked = preset.Seeded;
                if (preset.Seeded)
                    seedBox.Value = Random.Shared.Next(10000, 100000);
                amountBox.Value = preset.DefaultAmount;
                maxAmountBox.Value = preset.DefaultMax;
            }
            UpdatePreview();
        };

        idBox.TextChanged += (_, _) => UpdatePreview();
        seedBox.ValueChanged += (_, _) => UpdatePreview();

        UpdatePreview();

        var dialog = new ContentDialog
        {
            Title = existingCell != null ? "Edit Item Details & Seed" : "Add Custom / Procedural Item",
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
