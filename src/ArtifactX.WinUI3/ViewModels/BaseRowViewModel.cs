using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row on BasesPage - a planet base with its portal address. Read-only.</summary>
public sealed class BaseRowViewModel
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

    // Sort keys
    public required double SortDistance { get; init; }
    public required int Galaxy { get; init; }
}
