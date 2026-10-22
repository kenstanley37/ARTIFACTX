using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One installed item in a template preview: its icon and name, and whether this
/// game version still knows it.</summary>
public sealed class TemplatePreviewItemViewModel(NmsLoadoutTechItem item)
{
    private readonly CatalogEntry? _entry = CatalogService.TryGet(item.ItemId);

    public ImageSource? Icon => _entry?.Icon;
    public bool HasIcon => Icon is not null;
    public bool NoIcon => Icon is null;
    public bool IsKnown => _entry is not null;

    public string Name => _entry?.DisplayName is { Length: > 0 } name
        ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant())
        : "Unknown item";

    public string Tip => IsKnown ? Name : "This game version doesn't have this item - it would be left out when applied";
    public double Opacity => IsKnown ? 1 : 0.45;
}

/// <summary>One base stat in a template preview ("Damage +12.5%").</summary>
public sealed record TemplatePreviewStat(string Label, string Value);

/// <summary>
/// What a saved template holds, shown before it's used (#24): its tech as icons, class,
/// type, base stats, unlocked and supercharged slots and, for full builds, look and interior.
/// Items the installed game's catalog doesn't know are flagged - a template saved before a
/// game update can name tech that update removed or renamed.
/// </summary>
public sealed class TemplatePreviewViewModel
{
    public TemplatePreviewViewModel(NmsLoadoutTemplate template)
    {
        Template = template;
        Items = template.TechItems.Where(t => !string.IsNullOrEmpty(t.ItemId))
            .Select(t => new TemplatePreviewItemViewModel(t)).ToList();

        string kind = template.SourceKind switch { "Ship" => "Starship", "Freighter" => "Freighter", _ => "Multi-Tool" };
        Heading = $"{kind} · {(template.Scope == "FullBuild" ? "Full build" : "Tech stack")}";

        var facts = new List<(string, string)>();
        if (template.SourceClass is { Length: > 0 } cls) facts.Add(("Class", cls));
        if (template.SourceToolName is { Length: > 0 } from) facts.Add(("Saved from", from));
        facts.Add(("Saved", template.CreatedAt.LocalDateTime.ToString("d", CultureInfo.CurrentCulture)));
        facts.Add(("Tech", $"{Items.Count} items"));
        facts.Add(("Unlocked slots", template.UnlockedPositions.Count.ToString(CultureInfo.CurrentCulture)));
        if (template.SuperchargedPositions is { Count: > 0 } super) facts.Add(("Supercharged slots", super.Count.ToString(CultureInfo.CurrentCulture)));
        if (template.CargoUnlockedPositions is { Count: > 0 } cargo) facts.Add(("Cargo slots", cargo.Count.ToString(CultureInfo.CurrentCulture)));
        if (template.Scope == "FullBuild")
        {
            if (!string.IsNullOrEmpty(template.Seed)) facts.Add(("Look", "its own model seed"));
            if (template.InteriorObjects is { Count: > 0 } parts)
                facts.Add((template.SourceKind == "Freighter" ? "Interior" : "Corvette build", $"{parts.Count:N0} parts"));
        }
        Facts = facts.Select(f => new TemplatePreviewStat(f.Item1, f.Item2)).ToList();

        Stats = template.Scope != "FullBuild" ? []
            : StatBonusService.StatsFor(template.SourceKind)
                .Select(s => (s.Label, Value: template.Stats.FirstOrDefault(v => v.Key == s.Key)?.Value))
                .Where(s => s.Value is not null)
                .Select(s => new TemplatePreviewStat(s.Label, s.Value!.Value.ToString("0.##", CultureInfo.CurrentCulture)))
                .ToList();

        int unknown = Items.Count(i => !i.IsKnown);
        Compatibility = unknown == 0
            ? "Every item is in your installed version of No Man's Sky."
            : $"{unknown} item{(unknown == 1 ? "" : "s")} {(unknown == 1 ? "isn't" : "aren't")} in your installed version of No Man's Sky (shown faded) - probably removed or renamed by a game update. {(unknown == 1 ? "It" : "They")} would be left out when the template is used.";
        HasProblems = unknown > 0;
    }

    public NmsLoadoutTemplate Template { get; }
    public string Name => Template.Name;
    public string Heading { get; }
    public IReadOnlyList<TemplatePreviewStat> Facts { get; }
    public IReadOnlyList<TemplatePreviewStat> Stats { get; }
    public bool HasStats => Stats.Count > 0;
    public IReadOnlyList<TemplatePreviewItemViewModel> Items { get; }
    public string Compatibility { get; }
    public bool HasProblems { get; }
}
