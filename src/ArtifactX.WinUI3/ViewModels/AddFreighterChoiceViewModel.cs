using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One way to get a freighter on a save that has none: a fresh starter hull ("Start fresh")
/// or a saved Freighter Full Build template ("Add a freighter"), as a row with its name, what
/// it is, Preview (templates) and Add as my freighter.
/// </summary>
public sealed class AddFreighterChoiceViewModel
{
    private AddFreighterChoiceViewModel(NmsLoadoutTemplate template, string name, string detail, bool canPreview)
    {
        Template = template;
        Name = name;
        Detail = detail;
        CanPreview = canPreview;
    }

    public NmsLoadoutTemplate Template { get; }
    public string Name { get; }
    public string Detail { get; }

    /// <summary>Saved templates can be previewed; a starter hull is just the empty freighter.</summary>
    public bool CanPreview { get; }

    public string AddName => $"Add {(CanPreview ? Name : $"a {Name}")} as my freighter";
    public string PreviewName => $"Preview {Name}";

    public static AddFreighterChoiceViewModel Starter(string name, string description, string scene) =>
        new(FreighterBuildService.Starter(name, scene), name, description, canPreview: false);

    public static AddFreighterChoiceViewModel FromTemplate(NmsLoadoutTemplate t) =>
        new(t, t.Name,
            $"{t.SourceClass ?? "?"} class, {t.TechItems.Count} tech"
            + (FreighterBuildService.HasInterior(t) ? $", interior with {t.InteriorObjects!.Count} parts" : ", default interior (saved before interiors were included)")
            + (t.SourceToolName is { Length: > 0 } source ? $" - from {source}" : ""),
            canPreview: true);
}
