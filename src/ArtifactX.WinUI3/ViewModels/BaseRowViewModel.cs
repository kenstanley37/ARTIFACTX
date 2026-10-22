using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One card on the Bases page: a base, settlement or loose placed object.</summary>
public sealed partial class BaseRowViewModel : ObservableObject
{
    public BaseRowViewModel(BaseListEntry entry)
    {
        Entry = entry;
        Swatch = new SolidColorBrush(ParseColour(entry.SwatchHex));
        PartsBrush = entry.PartsLevel >= 0 ? ThemeResources.LevelBrush(entry.PartsLevel) : ThemeResources.Brush("ThemeTextDimBrush");
        Icon = entry.PlacedItemId is { } id ? CatalogService.TryGet(id)?.Icon : null;
    }

    public BaseListEntry Entry { get; }

    public BaseKind Kind => Entry.Kind;
    public string KindLabel => BaseListBuilder.KindLabel(Entry.Kind);
    public string Name => Entry.Name;
    public string Detail => Entry.Detail;
    public string Facts => Entry.Facts;
    public string PortalCode => Entry.PortalCode;
    public string DistanceLabel => Entry.DistanceLabel;

    /// <summary>What's recorded about the planet and area, or a hint to add details on
    /// the Planets page (shown faded).</summary>
    public string AreaLabel => Entry.AreaLabel;
    public double AreaOpacity => Entry.AreaRecorded ? 1.0 : 0.55;

    /// <summary>"3,031 / 3,000 parts · too many to upload", coloured green / yellow / red
    /// by how close a base is to the upload limit.</summary>
    public string PartsLabel => Entry.PartsLabel;
    public Brush PartsBrush { get; }

    public Brush Swatch { get; }

    /// <summary>A placed object's icon from the catalog.</summary>
    public ImageSource? Icon { get; }

    /// <summary>The base's save entry (for a settlement, the parts gathered from its planet);
    /// null for a loose placed object.</summary>
    public JObject? Base => Entry.Base;

    /// <summary>The loose placed object's save entry; null for bases.</summary>
    public JObject? LooseObject => Entry.LooseObject;

    /// <summary>Whether it can be deleted: planet bases and loose placed objects only.
    /// The freighter base is the freighter's interior, a claimed station's base goes with
    /// its claim, and a settlement isn't one entry in the save.</summary>
    public bool CanDelete => Kind is BaseKind.Planet or BaseKind.Placed;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The picture the game uploaded of the base (GameScreenshotCache), if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicture))]
    public partial ImageSource? Picture { get; set; }

    public bool HasPicture => Picture is not null;

    /// <summary>What the picture area says while there's no picture: how to get one where the
    /// game takes one (Capture New Base Screenshot at a base computer or station core).</summary>
    public string PicturePlaceholder => Kind switch
    {
        BaseKind.Planet or BaseKind.Station or BaseKind.SpaceBase =>
            "No picture yet - in game, choose Capture New Base Screenshot at its base computer or station core",
        // The game takes no freighter picture; a 3D preview from its model is planned (#25).
        BaseKind.Freighter => "Freighter picture coming - built from its model, like the Starships preview",
        _ => "",
    };

    /// <summary>Settlements and loose objects never get a game picture: the picture area shows
    /// the object's own icon large instead (a Geobay for a Geobay), or a settlement glyph.</summary>
    public bool ShowBigIcon => Icon is not null;
    public bool ShowPlaceholderGlyph => Icon is null;
    public string PlaceholderGlyph => Kind == BaseKind.Settlement ? "" : "";
    public bool HasPicturePlaceholder => PicturePlaceholder.Length > 0;

    public bool Matches(string search) =>
        search.Length == 0
        || Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || (Entry.AreaRecorded && AreaLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase))
        || Detail.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || Facts.Contains(search, StringComparison.CurrentCultureIgnoreCase)
        || KindLabel.Contains(search, StringComparison.CurrentCultureIgnoreCase);

    private static Windows.UI.Color ParseColour(string hex)
    {
        uint rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
