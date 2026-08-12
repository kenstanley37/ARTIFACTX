using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One card in GuidePage's responsive WrapPanel - a real Guide
/// section (e.g. "Survival Basics") and the topics currently visible in it
/// (already filtered by GuidePage's search box before this is built).</summary>
public sealed class GuideCategoryGroupViewModel
{
    public required string Category { get; init; }
    public required List<GuideTopicRowViewModel> Topics { get; init; }

    public string HeaderText => $"{Category} ({Topics.Count})";
}
