using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One card in GuidePage's responsive WrapPanel - a real Guide
/// section (e.g. "Survival Basics") and the topics currently visible in it
/// (already filtered by GuidePage's search box before this is built).</summary>
public sealed class GuideCategoryGroupViewModel
{
    public required string Category { get; init; }
    public required List<GuideTopicRowViewModel> Topics { get; init; }

    /// <summary>How many invisible "ghost" rows to pad this card with so
    /// every card ends up the same height - user feedback 2026-08-12 ("all
    /// containers should match the tallest container"). GuidePage sets this
    /// to (largest category's topic count - this one's), computed after
    /// filtering. Rendered as a second ItemsControl using the EXACT same row
    /// template as a real topic row, just hidden - deliberately not a
    /// guessed pixel Height, since that would drift the moment font size,
    /// DPI, or the checkbox styling changes; padding with real (hidden) rows
    /// always matches whatever the actual row height turns out to be.</summary>
    public int PaddingCount { get; init; }

    public IEnumerable<int> PaddingIndices => Enumerable.Range(0, PaddingCount);

    public string HeaderText => $"{Category} ({Topics.Count})";
}
