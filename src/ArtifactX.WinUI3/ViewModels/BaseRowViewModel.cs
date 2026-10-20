using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on BasesPage - a planet base with its portal address. Read-only.</summary>
public partial class BaseRowViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Facts { get; init; }
    public required Brush Swatch { get; init; }
    public required string PortalCode { get; init; }
    public required List<ImageSource> Glyphs { get; init; }
    public required string DistanceLabel { get; init; }

    /// <summary>What's recorded about the base's planet and area (resources,
    /// sentinels, water, fish caught there, finds, ships seen in the system),
    /// or a hint to add details on the Planets page.</summary>
    public required string AreaLabel { get; init; }
    public required double AreaOpacity { get; init; }

    /// <summary>"3,031 / 3,000 parts · too many to upload", coloured green / yellow / red
    /// by how close the base is to the upload limit. Empty for placed objects.</summary>
    public string PartsLabel { get; init; } = "";
    public Brush? PartsBrush { get; init; }

    /// <summary>The base's save entry, for its parts list (null for placed objects).</summary>
    public Newtonsoft.Json.Linq.JObject? Base { get; init; }

    /// <summary>The loose placed object's save entry in c5s (null for bases).</summary>
    public Newtonsoft.Json.Linq.JObject? LooseObject { get; init; }

    /// <summary>Selection state for bulk operations.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The picture the game uploaded of the base (GameScreenshotCache), if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PictureVisibility))]
    public partial ImageSource? Picture { get; set; }

    /// <summary>A placed object's icon from the catalog.</summary>
    public ImageSource? Icon { get; init; }
    public Microsoft.UI.Xaml.Visibility PictureVisibility => Picture is null ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    // Sort keys
    public required double SortDistance { get; init; }
    public required int Galaxy { get; init; }
    public string Category { get; init; } = "Planetary Bases";
}
