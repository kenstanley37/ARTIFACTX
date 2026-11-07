using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The player's saved designs (2026-10-17): Save design keeps the current choices
/// in user.db (ShipDesignsService) without touching the game save, and the My designs
/// card under Paint lists them as picture cards - click one to load it, ready to add
/// or apply to a ship. Designs are stored by the game's ids, so loading checks each
/// one against the current game data and falls back to the default for anything
/// that has gone.
///
/// Pictures come from the preview's own renderer at its opening angle, so every card
/// is framed alike. A design saved before pictures existed gets one the first time
/// it's loaded and its preview has drawn.
/// </summary>
public sealed partial class ShipDesignerPage
{
    private const int ThumbWidth = 288, ThumbHeight = 180; // drawn at 2x the card's 144 x 90

    // The saved design being edited, if one was saved or loaded: as saved, to tell
    // whether it has unsaved changes.
    private ShipDesign? _editing;
    private string? _designId;
    private string _designName = "";
    private string _designCreated = "";

    private List<SavedDesign> _savedDesigns = new();
    private readonly Dictionary<string, BitmapImage> _thumbs = new();
    // A design with no picture yet: taken once its preview has drawn (OnPreviewReady).
    private ShipDesign? _needsThumbnail;
    private bool _previewReady;

    /// <summary>Editing a saved design: saves the changes under its name, no questions
    /// (2026-10-17, the user: "Save changes shouldn't ask for a name"). A new design:
    /// asks for a name first.</summary>
    private async void SaveDesignBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null) return;
        if (_editing is { } saved) await SaveAsync(CurrentDesign(saved.Id, saved.Name, saved.CreatedAt));
        else await SaveAsNewAsync();
    }

    private async void SaveAsNewBtn_Click(object sender, RoutedEventArgs e) => await SaveAsNewAsync();

    private async Task SaveAsNewAsync()
    {
        if (_type is null) return;
        var nameBox = new TextBox
        {
            Header = "Design name",
            Text = _editing is null ? ShipNameTxt.Text : $"{_designName} (copy)",
            MaxLength = 80,
        };
        nameBox.SelectAll();
        var dialog = new ContentDialog
        {
            Title = "Save as a new design",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = nameBox.Text.Trim().Length > 0;
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await SaveAsync(CurrentDesign(Guid.NewGuid().ToString("N"), nameBox.Text.Trim(), UserDatabase.Now()));
    }

    private async Task SaveAsync(ShipDesign design)
    {
        byte[]? thumbnail = _previewReady ? await TakeThumbnailAsync() : null;
        try
        {
            await ShipDesignsService.SaveAsync(design, thumbnail);
        }
        catch (Exception ex)
        {
            await Message("Couldn't save the design", ex.Message);
            return;
        }
        _thumbs.Remove(design.Id);
        _needsThumbnail = thumbnail is null ? design : null;
        SetDesign(design);
        await RefreshDesignsAsync();
    }

    private ShipDesign CurrentDesign(string id, string name, string createdAt) => new(
        id, name, _type!.Type,
        new Dictionary<string, string>(_parts),
        _style?.Option,
        new Dictionary<string, int>(_colourPicks),
        "0x" + _seed.ToString("X16", CultureInfo.InvariantCulture),
        createdAt);

    // ------------------------------------------------------------------ the My designs card

    private async Task RefreshDesignsAsync()
    {
        _savedDesigns = await ShipDesignsService.LoadAllAsync();
        await ShowDesignsAsync();
    }

    private async void DesignSearchBox_TextChanged(object sender, TextChangedEventArgs e) => await ShowDesignsAsync();

    private async Task ShowDesignsAsync()
    {
        string find = DesignSearchBox.Text.Trim();
        var shown = _savedDesigns.Where(d => find.Length == 0
            || d.Design.Name.Contains(find, StringComparison.CurrentCultureIgnoreCase)
            || DesignSubtitle(d.Design).Contains(find, StringComparison.CurrentCultureIgnoreCase)).ToList();
        DesignsEmptyTxt.Text = _savedDesigns.Count == 0
            ? "No saved designs yet. Make a ship you like and click Save design to keep it here."
            : shown.Count == 0 ? $"No design matches \"{find}\"." : "";
        DesignsEmptyTxt.Visibility = DesignsEmptyTxt.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        DesignsPanel.Children.Clear();
        foreach (var saved in shown)
            DesignsPanel.Children.Add(await DesignCardAsync(saved));
    }

    /// <summary>A card: the picture, name and type/style. Click to load; the small
    /// button in the corner deletes (beside the card, not inside its button).</summary>
    private async Task<UIElement> DesignCardAsync(SavedDesign saved)
    {
        var design = saved.Design;
        var picture = new Grid { Width = 144, Height = 90, CornerRadius = new CornerRadius(4), Background = ThemeResources.Brush("ThemePanelAltBrush") };
        if (await ThumbnailAsync(saved) is { } image)
            picture.Children.Add(new Image { Source = image, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform });
        else
            picture.Children.Add(new TextBlock
            {
                Text = "Load it to take a picture", FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8),
            });
        var content = new StackPanel { Width = 144, Spacing = 2 };
        content.Children.Add(picture);
        content.Children.Add(new TextBlock { Text = design.Name, FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
        content.Children.Add(new TextBlock
        {
            Text = DesignSubtitle(design), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
            Style = (Style)Application.Current.Resources["ThemeDimTextStyle"],
        });
        var card = new Button { Content = content, Tag = design.Id, Padding = new Thickness(6), VerticalAlignment = VerticalAlignment.Stretch };
        ThemeResources.StyleChoiceButton(card, design.Id == _designId);
        ToolTipService.SetToolTip(card, $"Load \"{design.Name}\"");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, $"Load {design.Name}");
        // Painting a ship: a design lends its paint only (its parts would be a different ship).
        card.Click += (_, _) => { if (_paintShip is not null) UseDesignPaint(design); else LoadDesign(design, saved.Thumbnail is null); };

        var delete = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 12 }, Padding = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 10, 10, 0),
        };
        ToolTipService.SetToolTip(delete, "Delete this design");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(delete, $"Delete {design.Name}");
        delete.Click += async (_, _) =>
        {
            if (!await Confirm($"Delete \"{design.Name}\"?", "The design is removed from My designs. Ships you already made from it stay as they are.", "Delete")) return;
            await ShipDesignsService.DeleteAsync(design.Id);
            _thumbs.Remove(design.Id);
            if (design.Id == _designId) SetDesign(null);
            await RefreshDesignsAsync();
        };

        var cell = new Grid();
        cell.Children.Add(card);
        cell.Children.Add(delete);
        return cell;
    }

    private string DesignSubtitle(ShipDesign design)
    {
        var type = _types.FirstOrDefault(t => t.Type == design.Type);
        string style = type is null ? "" : _styles.GetValueOrDefault(type.StyleGroup)?.FirstOrDefault(s => s.Option == design.Style)?.Name ?? "";
        return string.Join(" · ", new[] { type is null ? design.Type : Title(type.Title), style }.Where(t => t.Length > 0));
    }

    private async Task<BitmapImage?> ThumbnailAsync(SavedDesign saved)
    {
        if (saved.Thumbnail is not { Length: > 0 } png) return null;
        if (_thumbs.TryGetValue(saved.Design.Id, out var cached)) return cached;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            return _thumbs[saved.Design.Id] = image;
        }
        catch (Exception)
        {
            return null; // an unreadable picture shows the placeholder
        }
    }

    /// <summary>The preview, as a PNG at its opening angle.</summary>
    private async Task<byte[]?> TakeThumbnailAsync()
    {
        if (await PreviewView.SnapshotAsync(ThumbWidth, ThumbHeight) is not { } pixels) return null;
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, ThumbWidth, ThumbHeight, 96, 96, pixels);
        await encoder.FlushAsync();
        var bytes = new byte[stream.Size];
        stream.Seek(0);
        await stream.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
        return bytes;
    }

    /// <summary>Called when the preview has drawn the current design: a loaded design
    /// with no picture gets one - if the preview still shows exactly that design.</summary>
    private async void OnPreviewReady()
    {
        if (_needsThumbnail is not { } design || design.Id != _designId) return;
        if (_type?.Type != design.Type || _style?.Option != design.Style
            || !design.Parts.OrderBy(p => p.Key).SequenceEqual(_parts.OrderBy(p => p.Key))) return;
        _needsThumbnail = null;
        if (await TakeThumbnailAsync() is not { } png) return;
        try
        {
            await ShipDesignsService.SetThumbnailAsync(design.Id, png);
        }
        catch (Exception)
        {
            return; // no picture this time; the card keeps its placeholder
        }
        await RefreshDesignsAsync();
    }

    // ------------------------------------------------------------------ loading

    /// <summary>Puts a saved design back into the designer. Anything the game data no
    /// longer has (after a game update) falls back to the builder's default.</summary>
    private async void LoadDesign(ShipDesign design, bool needsThumbnail)
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
        PartSearchBox.Text = "";
        var styles = _styles.GetValueOrDefault(type.StyleGroup) ?? new();
        _style = styles.FirstOrDefault(s => s.Option == design.Style);
        if (_style is null) { _style = styles.FirstOrDefault(); missing |= design.Style is not null; }
        ResetColours();
        int swatchCount = _swatches.GetValueOrDefault(CurrentColours()?.Swatches ?? "")?.Count ?? 0;
        foreach (var (group, index) in design.Colours)
            if (_colourPicks.ContainsKey(group) && index >= 0 && index < swatchCount) _colourPicks[group] = index;
        string seed = design.Seed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? design.Seed[2..] : design.Seed;
        if (ulong.TryParse(seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed)) _seed = parsed;

        _needsThumbnail = needsThumbnail && !missing ? design : null;
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

    private void NewDesignBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_type is null) return;
        SetDesign(null);
        _seed = NewSeed();
        SelectType(_type);
    }

    /// <summary>Says whether this is a new design or a saved one being edited, and
    /// whether that one has changed since it was saved.</summary>
    private void UpdateDesignState()
    {
        if (_paintShip is not null) return; // paint mode says its own thing (Paint.cs)
        if (_editing is not { } saved || _type is null)
        {
            DesignNameTxt.Text = "New design · not saved yet";
            NewDesignBtn.Visibility = Visibility.Collapsed;
            SaveDesignBtn.Content = "Save design...";
            SaveDesignBtn.IsEnabled = true;
            SaveAsNewBtn.Visibility = Visibility.Collapsed;
            return;
        }
        var now = CurrentDesign(saved.Id, saved.Name, saved.CreatedAt);
        bool changed = now.Type != saved.Type || now.Style != saved.Style || now.Seed != saved.Seed
            || !SameEntries(now.Parts, saved.Parts) || !SameEntries(now.Colours, saved.Colours);
        DesignNameTxt.Text = $"Editing: {saved.Name}" + (changed ? " · unsaved changes" : "");
        NewDesignBtn.Visibility = Visibility.Visible;
        SaveDesignBtn.Content = "Save changes";
        SaveDesignBtn.IsEnabled = changed;
        SaveAsNewBtn.Visibility = Visibility.Visible;
    }

    private static bool SameEntries<T>(Dictionary<string, T> a, Dictionary<string, T> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && Equals(v, kv.Value));

    private void SetDesign(ShipDesign? design)
    {
        _editing = design;
        _designId = design?.Id;
        _designName = design?.Name ?? "";
        _designCreated = design?.CreatedAt ?? "";
        UpdateDesignState();
        // Highlight the loaded design's card.
        foreach (var card in DesignsPanel.Children.OfType<Grid>().Select(g => g.Children.OfType<Button>().FirstOrDefault()).OfType<Button>())
            ThemeResources.StyleChoiceButton(card, card.Tag is string id && id == design?.Id);
    }
}
