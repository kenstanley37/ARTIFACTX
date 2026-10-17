using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The player's saved designs (2026-10-17): Save design keeps the current choices
/// in user.db (ShipDesignsService) without touching the game save, and My designs
/// loads one back into the designer, ready to add or apply to a ship. Designs are
/// stored by the game's ids, so loading checks each one against the current game
/// data and falls back to the default for anything that has gone.
/// </summary>
public sealed partial class ShipDesignerPage
{
    // The saved design being edited, if one was saved or loaded.
    private string? _designId;
    private string _designName = "";
    private string _designCreated = "";

    private async void SaveDesignBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null) return;
        var nameBox = new TextBox
        {
            Header = "Design name",
            Text = _designId is null ? ShipNameTxt.Text : _designName,
            MaxLength = 80,
        };
        nameBox.SelectAll();
        var dialog = new ContentDialog
        {
            Title = _designId is null ? "Save design" : $"Save \"{_designName}\"",
            Content = nameBox,
            PrimaryButtonText = "Save",
            // Saved before: Save updates it, Save as new keeps both.
            SecondaryButtonText = _designId is null ? "" : "Save as new",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        nameBox.TextChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = dialog.IsSecondaryButtonEnabled = nameBox.Text.Trim().Length > 0;
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return;

        bool asNew = _designId is null || result == ContentDialogResult.Secondary;
        var design = new ShipDesign(
            asNew ? Guid.NewGuid().ToString("N") : _designId!,
            nameBox.Text.Trim(),
            _type.Type,
            new Dictionary<string, string>(_parts),
            _style?.Option,
            new Dictionary<string, int>(_colourPicks),
            "0x" + _seed.ToString("X16", CultureInfo.InvariantCulture),
            asNew ? UserDatabase.Now() : _designCreated);
        try
        {
            await ShipDesignsService.SaveAsync(design);
        }
        catch (Exception ex)
        {
            await Message("Couldn't save the design", ex.Message);
            return;
        }
        SetDesign(design);
    }

    private async void MyDesignsBtn_Click(object sender, RoutedEventArgs e)
    {
        var designs = await ShipDesignsService.LoadAllAsync();
        if (designs.Count == 0)
        {
            await Message("No saved designs yet", "Make a ship you like, then click Save design to keep it here. You can load it again any time to add it as a new ship or apply it to one.");
            return;
        }

        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 420 };
        foreach (var design in designs)
            list.Items.Add(new ListViewItem { Content = DesignRow(design), Tag = design, Padding = new Thickness(12, 6, 12, 6) });
        if (designs.FirstOrDefault(d => d.Id == _designId) is { } current)
            list.SelectedIndex = designs.IndexOf(current);
        var dialog = new ContentDialog
        {
            Title = "My designs",
            Content = list,
            PrimaryButtonText = "Load",
            SecondaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = list.SelectedItem is not null,
            IsSecondaryButtonEnabled = list.SelectedItem is not null,
            XamlRoot = XamlRoot,
        };
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = dialog.IsSecondaryButtonEnabled = list.SelectedItem is not null;
        bool doubleTapped = false;
        list.DoubleTapped += (_, _) =>
        {
            if (list.SelectedItem is null) return;
            doubleTapped = true;
            dialog.Hide();
        };
        var result = await dialog.ShowAsync();
        if (list.SelectedItem is not ListViewItem { Tag: ShipDesign picked }) return;

        if (result == ContentDialogResult.Primary || doubleTapped)
        {
            LoadDesign(picked);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (!await Confirm($"Delete \"{picked.Name}\"?", "The design is removed from ArtifactX. Ships you already made from it stay as they are.", "Delete")) return;
            await ShipDesignsService.DeleteAsync(picked.Id);
            if (picked.Id == _designId) SetDesign(null);
            // Back to the list, unless that was the last one.
            if (designs.Count > 1) MyDesignsBtn_Click(sender, e);
        }
    }

    private UIElement DesignRow(ShipDesign design)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        string style = type is null ? "" : _styles.GetValueOrDefault(type.StyleGroup)?.FirstOrDefault(s => s.Option == design.Style)?.Name ?? "";
        string saved = DateTime.TryParse(design.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at.ToLocalTime().ToString("d", CultureInfo.CurrentCulture) : "";
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = design.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        row.Children.Add(new TextBlock
        {
            Text = string.Join("  ·  ", new[] { type is null ? design.Type : Title(type.Title), style, saved }.Where(t => t.Length > 0)),
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"], FontSize = 12,
        });
        return row;
    }

    /// <summary>Puts a saved design back into the designer. Anything the game data no
    /// longer has (after a game update) falls back to the builder's default.</summary>
    private async void LoadDesign(ShipDesign design)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        if (type is null)
        {
            await Message("Can't load this design", $"Your game data has no \"{design.Type}\" ship type. Update your game data, or delete the design.");
            return;
        }
        bool missing = false;
        _type = type;
        _parts.Clear();
        foreach (var slot in type.Slots)
        {
            if (design.Parts.TryGetValue(slot.Id, out string? item) && slot.Items.Any(i => i.Item == item)) _parts[slot.Id] = item;
            else if (slot.Items.Count > 0) { _parts[slot.Id] = slot.Items[0].Item; missing = true; }
        }
        _slot = type.Slots.FirstOrDefault()?.Id ?? "";
        var styles = _styles.GetValueOrDefault(type.StyleGroup) ?? new();
        _style = styles.FirstOrDefault(s => s.Option == design.Style);
        if (_style is null) { _style = styles.FirstOrDefault(); missing |= design.Style is not null; }
        ResetColours();
        int swatchCount = _swatches.GetValueOrDefault(CurrentColours()?.Swatches ?? "")?.Count ?? 0;
        foreach (var (group, index) in design.Colours)
            if (_colourPicks.ContainsKey(group) && index >= 0 && index < swatchCount) _colourPicks[group] = index;
        string seed = design.Seed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? design.Seed[2..] : design.Seed;
        if (ulong.TryParse(seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed)) _seed = parsed;

        SetDesign(design);
        BuildTypePanel();
        BuildSlotTabs();
        ShowParts();
        ShowPaint();
        RefreshName();
        RefreshPreview();
        if (missing)
            await Message("Some parts have changed", "Your game data no longer has everything this design used, so those choices are back to the default. Save the design again to keep it this way.");
    }

    private void SetDesign(ShipDesign? design)
    {
        _designId = design?.Id;
        _designName = design?.Name ?? "";
        _designCreated = design?.CreatedAt ?? "";
        DesignNameTxt.Text = design is null ? "" : $"Saved design: {design.Name}";
        DesignNameTxt.Visibility = design is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
