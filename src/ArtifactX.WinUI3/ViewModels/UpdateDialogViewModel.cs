using ArtifactX.Core;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The update window (UpdateDialog): which version is waiting, its release notes as headings and bullets
/// (ReleaseNotes.Parse), and the download's progress. A preview (Settings, Debug builds) shows the newest
/// published release and runs a pretend download that never restarts the app, so the window can be checked
/// without a real update (owner, 2026-11-18).
/// </summary>
public partial class UpdateDialogViewModel : ObservableObject
{
    private readonly UpdateCheckResult _result;

    public UpdateDialogViewModel(UpdateCheckResult result, bool isPreview)
    {
        _result = result;
        IsPreview = isPreview;
        IsInstalled = UpdateCheckService.IsInstalled;
        Sections = ReleaseNotes.Parse(result.ReleaseNotes);
        Status = IsPreview
            ? "Preview: nothing is downloaded and ArtifactX keeps running."
            : IsInstalled
                ? "Updating closes ArtifactX, installs the new version and opens it again. Windows may scan the new files first; that's normal and takes a moment."
                : "This copy of ArtifactX can't update itself. Download the new version from GitHub.";
    }

    public bool IsPreview { get; }
    public bool IsInstalled { get; }
    public string? ReleaseUrl => _result.ReleaseUrl;

    public string Headline => $"ArtifactX {_result.NewVersion ?? "update"} is ready";
    public string VersionLine => $"You have {AppVersionService.DisplayVersion}. Your saves, templates and settings are kept as they are.";

    public IReadOnlyList<ReleaseNoteSection> Sections { get; }
    public bool HasNotes => Sections.Count > 0;

    /// <summary>The main button: updates in place when installed (or a preview), else opens the release page.</summary>
    public string PrimaryText => IsInstalled || IsPreview ? "Update and restart" : "Download on GitHub";
    public bool UpdatesInPlace => IsInstalled || IsPreview;

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool Failed { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";

    /// <summary>Downloads and applies the update (the app restarts on success), or pretends to in a preview.
    /// Returns false when it stopped (failed, or the preview finished), so the window can be closed again.</summary>
    public async Task<bool> RunAsync(Action<Action> onUi)
    {
        IsBusy = true;
        Failed = false;
        Progress = 0;
        try
        {
            if (IsPreview)
            {
                for (int percent = 0; percent <= 100; percent += 4)
                {
                    Progress = percent;
                    Status = $"Downloading {_result.NewVersion}... {percent}% (preview)";
                    await Task.Delay(80);
                }
                Status = "Preview finished. A real update would restart ArtifactX now.";
                return false;
            }

            bool restarting = await UpdateCheckService.DownloadAndRestartAsync(message => onUi(() =>
            {
                Progress = UpdateCheckService.DownloadProgress;
                Status = message.StartsWith("Downloading", StringComparison.Ordinal)
                    ? $"Downloading {_result.NewVersion}... {UpdateCheckService.DownloadProgress}%"
                    : message;
            }));
            if (!restarting)
            {
                Failed = true;
                if (!Status.StartsWith("Update failed", StringComparison.Ordinal)) Status = "The update didn't finish. Try again, or download it from GitHub.";
            }
            return restarting;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
