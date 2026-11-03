using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.IndustrialCleanup;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.IndustrialCleanup;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class IndustrialCleanupViewModel : ObservableObject
{
    private readonly IndustrialCleanupService _service = new();
    private int _version;
    public ObservableCollection<CleanupInstructionViewModel> Instructions { get; } = [];
    public ObservableCollection<CleanupProgressViewModel> Counters { get; } = [];
    public ObservableCollection<CleanupProgressViewModel> Rewards { get; } = [];
    [ObservableProperty] public partial string Title { get; set; } = "Industrial Cleanup";
    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial string FindingTitle { get; set; } = "";
    [ObservableProperty] public partial string FindingText { get; set; } = "";
    [ObservableProperty] public partial string Message { get; set; } = "Loading cleanup guidance…";
    [ObservableProperty] public partial string ProgressMessage { get; set; } = "";

    public async Task LoadAsync()
    {
        int version = ++_version;
        try
        {
            var catalog = await _service.LoadAsync();
            if (version != _version) return;
            if (catalog is null) { Message = "Update game data to load Industrial Cleanup guidance."; return; }
            Title = CleanupText.Plain(catalog.Guide.Title);
            Description = CleanupText.Plain(catalog.Guide.Description);
            FindingTitle = CleanupText.Plain(catalog.Guide.FindingTitle);
            FindingText = CleanupText.Plain(catalog.Guide.FindingText);
            foreach (var obsolete in Instructions.Where(r => !catalog.Guide.Instructions.Any(i => i.Id == r.Id)).ToArray())
                Instructions.Remove(obsolete);
            foreach (var obsolete in Counters.Where(r => !catalog.Guide.Counters.Any(c => c.StatId == r.Id)).ToArray())
                Counters.Remove(obsolete);
            int number = 0;
            foreach (var instruction in catalog.Guide.Instructions)
            {
                var row = Instructions.FirstOrDefault(r => r.Id == instruction.Id);
                if (row is null) { row = new(instruction.Id); Instructions.Add(row); }
                row.Title = $"{++number}. {CleanupText.Plain(instruction.Title)}";
                row.Text = CleanupText.Plain(instruction.Text);
            }
            foreach (var counter in catalog.Guide.Counters)
            {
                var row = Counters.FirstOrDefault(c => c.Id == counter.StatId);
                if (row is null) { row = new(counter.StatId, ""); Counters.Add(row); }
                row.Name = CleanupText.Plain(counter.Label);
            }
            var rewardIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var option in catalog.Options.Where(o => o.Vehicle == (int)ExocraftType.Colossus && o.SpecialId.Length > 0))
                foreach (var rule in catalog.Titles.Where(t => t.SpecialIds.Contains(option.SpecialId) && t.StatId.Length > 0))
                {
                    string id = option.SpecialId + ":" + rule.TitleId;
                    rewardIds.Add(id);
                    var row = Rewards.FirstOrDefault(r => r.Id == id);
                    if (row is null) { row = new(id, ""); Rewards.Add(row); }
                    row.Name = CleanupText.Plain(option.Name);
                    row.StatId = rule.StatId;
                    row.Target = rule.StatValue;
                    row.Requirement = CleanupText.Plain(rule.Requirement);
                }
            foreach (var obsolete in Rewards.Where(r => !rewardIds.Contains(r.Id)).ToArray()) Rewards.Remove(obsolete);
            Message = "Follow the steps below. Control names refer to your mapped in-game actions.";
            Refresh();
        }
        catch (Exception ex)
        {
            if (version == _version) Message = "Cleanup guidance could not be read. Try updating game data.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void Refresh()
    {
        // The loaded editor session is authoritative: a feed notification for a
        // different running slot must never replace this slot's displayed totals.
        bool loaded = SaveSessionManager.IsSaveLoaded;
        var values = IndustrialCleanupProgress.Read(loaded ? SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) as JArray : null);
        ProgressMessage = loaded ? "Lifetime totals from the loaded save. Requirement progress does not confirm a reward was granted."
            : "Load a save to see your lifetime totals and progress. The guide works without one.";
        foreach (var row in Counters)
            row.Progress = values.TryGetValue(row.Id, out double value) ? value.ToString("N0") : "—";
        foreach (var row in Rewards)
            row.Progress = values.TryGetValue(row.StatId, out double value) ? $"{value:N0} / {row.Target:N0} toward this requirement"
                : $"Requirement: {row.Target:N0}";
    }
    public void Stop() => ++_version;
}

public sealed partial class CleanupInstructionViewModel(string id) : ObservableObject
{
    public string Id { get; } = id;
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Text { get; set; } = "";
}

public sealed partial class CleanupProgressViewModel(string id, string name) : ObservableObject
{
    public string Id { get; } = id;
    [ObservableProperty] public partial string Name { get; set; } = name;
    public string StatId { get; set; } = "";
    public float Target { get; set; }
    [ObservableProperty] public partial string Requirement { get; set; } = "";
    [ObservableProperty] public partial string Progress { get; set; } = "—";
}
