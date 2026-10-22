using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One saved template in the Templates sidebar.</summary>
public sealed class LoadoutTemplateRowViewModel(NmsLoadoutTemplate template, string fullBuildAction, string? fullBuildTip)
{
    public NmsLoadoutTemplate Template { get; } = template;
    public string Name => Template.Name;

    /// <summary>What a Full Build template's button does here: "Add as new" (Multi-Tool,
    /// Starships) or "Apply" (Freighter - a save only ever has one).</summary>
    public string FullBuildAction { get; } = fullBuildAction;
    public string? FullBuildTip { get; } = fullBuildTip;

    public string ApplyName => $"Apply {Name}";
    public string FullBuildActionName => $"{FullBuildAction} {Name}";
    public string DeleteName => $"Delete {Name}";
    public string PreviewName => $"Preview {Name}";

    /// <summary>"12 items, S class, stats+seed, interior (40 parts) - from Serenity".</summary>
    public string Summary
    {
        get
        {
            var t = Template;
            string from = t.SourceToolName is null ? "" : $" - from {t.SourceToolName}";
            string head = $"{t.TechItems.Count} items, {t.SourceClass ?? "?"} class";
            if (t.Scope != "FullBuild") return head + from;
            return (t.SourceKind, t.InteriorObjects?.Count) switch
            {
                ("MultiTool", _) => $"{head}, stats{from}",
                ("Ship", int parts) when ShipBaseService.HasBuild(t) => $"{head}, stats+seed, corvette build ({parts} parts){from}",
                ("Freighter", int parts) when FreighterBuildService.HasInterior(t) => $"{head}, stats+seed, interior ({parts} parts){from}",
                _ => $"{head}, stats+seed{from}",
            };
        }
    }
}

/// <summary>
/// The saved templates for one kind of equipment (SourceKind: "MultiTool", "Ship" or
/// "Freighter"), split into its two domains: Tech Stack (tech only) and Full Build (the
/// whole thing). Kept in LoadoutTemplateService; shared as files via TemplateSharingService.
/// </summary>
public sealed partial class LoadoutTemplatesViewModel : ObservableObject
{
    private readonly string _fullBuildAction;
    private readonly string? _fullBuildTip;

    public LoadoutTemplatesViewModel(string sourceKind)
    {
        SourceKind = sourceKind;
        (_fullBuildAction, _fullBuildTip) = sourceKind switch
        {
            "MultiTool" => ("Add as new", "Add this whole build as a new Multi-Tool in an empty slot. Your other Multi-Tools aren't changed."),
            "Ship" => ("Add as new", "Add this whole build as a new Starship in an empty slot. Your other Starships aren't changed."),
            _ => ("Apply", (string?)null),
        };
    }

    public string SourceKind { get; }

    public ObservableCollection<LoadoutTemplateRowViewModel> TechStacks { get; } = new();
    public ObservableCollection<LoadoutTemplateRowViewModel> FullBuilds { get; } = new();

    [ObservableProperty] public partial bool NoTechStacks { get; private set; }
    [ObservableProperty] public partial bool NoFullBuilds { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportStatus))]
    public partial string ImportStatus { get; private set; } = "";

    [ObservableProperty] public partial Brush? ImportStatusBrush { get; private set; }

    public bool HasImportStatus => ImportStatus.Length > 0;

    public async Task RefreshAsync()
    {
        var all = (await LoadoutTemplateService.LoadAllAsync()).Where(t => t.SourceKind == SourceKind).ToList();
        Fill(TechStacks, all.Where(t => t.Scope == "TechStack"));
        Fill(FullBuilds, all.Where(t => t.Scope == "FullBuild"));
        NoTechStacks = TechStacks.Count == 0;
        NoFullBuilds = FullBuilds.Count == 0;
    }

    public async Task SaveAsync(NmsLoadoutTemplate template)
    {
        // No confirmation popup on purpose: the template showing up in its list
        // straight away is the confirmation.
        await LoadoutTemplateService.SaveAsync(template);
        await RefreshAsync();
    }

    public async Task DeleteAsync(LoadoutTemplateRowViewModel row)
    {
        await LoadoutTemplateService.DeleteAsync(row.Template.Id);
        await RefreshAsync();
    }

    /// <summary>Imports a template file someone shared (TemplateSharingService checks it first).</summary>
    public async Task ImportAsync()
    {
        if (await TemplateSharingService.ImportAsync(SourceKind) is not { } result) return;
        ImportStatus = result.Message;
        ImportStatusBrush = ThemeResources.Brush(result.Ok ? "ThemeDoneBrush" : "ThemeWarningBrush");
        if (result.Ok) await RefreshAsync();
    }

    private void Fill(ObservableCollection<LoadoutTemplateRowViewModel> list, System.Collections.Generic.IEnumerable<NmsLoadoutTemplate> templates)
    {
        list.Clear();
        foreach (var template in templates) list.Add(new LoadoutTemplateRowViewModel(template, _fullBuildAction, _fullBuildTip));
    }
}
