using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The page a LoadoutTemplatesPanel sits on: what templates are captured from
/// and applied to (the selected Multi-Tool or Starship, or the freighter).</summary>
public interface ILoadoutTemplateHost
{
    /// <summary>What's selected, as said in messages ("Serenity", "Your freighter");
    /// null when nothing is.</summary>
    string? CurrentName { get; }

    /// <summary>Whether it has any tech installed - an empty one can't be a template.</summary>
    bool HasTech { get; }

    NmsLoadoutTemplate CaptureTechStack(string name);
    NmsLoadoutTemplate CaptureFullBuild(string name);
    Task ApplyTechStackAsync(NmsLoadoutTemplate template);

    /// <summary>"Add as new" (Multi-Tool, Starships) or "Apply" (Freighter).</summary>
    Task UseFullBuildAsync(NmsLoadoutTemplate template);
}

public sealed partial class LoadoutTemplatesPanel : UserControl
{
    public LoadoutTemplatesPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    /// <summary>Set once by the page, before the panel loads.</summary>
    public LoadoutTemplatesViewModel ViewModel { get; private set; } = new("MultiTool");

    public ILoadoutTemplateHost? Host { get; set; }

    /// <summary>"MultiTool", "Ship" or "Freighter": which templates are listed.</summary>
    public string SourceKind
    {
        get => ViewModel.SourceKind;
        set => ViewModel = new LoadoutTemplatesViewModel(value);
    }

    /// <summary>Under TECH STACK: what Apply does on this page.</summary>
    public string TechStackHint { get; set; } = "";

    /// <summary>Under FULL BUILD: what a full build holds and what its button does.</summary>
    public string FullBuildHint { get; set; } = "";

    /// <summary>A typical build name for the name prompt, e.g. "Combat Hauler".</summary>
    public string NameExample { get; set; } = "";

    /// <summary>What a full build captures, for the name prompt, e.g. "tech + stats".</summary>
    public string FullBuildContents { get; set; } = "tech + stats";

    public Task RefreshAsync() => ViewModel.RefreshAsync();

    private async void Import_Click(object sender, RoutedEventArgs e) => await ViewModel.ImportAsync();

    private async void SaveTechStack_Click(object sender, RoutedEventArgs e)
    {
        if (await AskNameAsync("Save as Template", "Name this loadout:", $"e.g. \"{NameExample}\"") is string name)
            await ViewModel.SaveAsync(Host!.CaptureTechStack(name));
    }

    private async void SaveFullBuild_Click(object sender, RoutedEventArgs e)
    {
        if (await AskNameAsync("Save as Full Build Template", $"Name this build ({FullBuildContents}):", $"e.g. \"{NameExample} (Full Build)\"") is string name)
            await ViewModel.SaveAsync(Host!.CaptureFullBuild(name));
    }

    private async void ApplyTechStack_Click(object sender, RoutedEventArgs e)
    {
        if (Host is not null && RowOf(sender) is { } row) await Host.ApplyTechStackAsync(row.Template);
    }

    private async void UseFullBuild_Click(object sender, RoutedEventArgs e)
    {
        if (Host is not null && RowOf(sender) is { } row) await Host.UseFullBuildAsync(row.Template);
    }

    private async void Share_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) await TemplateSharingService.ExportAsync((Button)sender, row.Template);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) await ViewModel.DeleteAsync(row);
    }

    private static LoadoutTemplateRowViewModel? RowOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as LoadoutTemplateRowViewModel;

    /// <summary>The name for a new template, or null when cancelled, nothing is
    /// selected, or there's no tech to capture (said so).</summary>
    private async Task<string?> AskNameAsync(string title, string label, string placeholder)
    {
        if (Host?.CurrentName is not string subject) return null;
        if (!Host.HasTech)
        {
            await new ContentDialog
            {
                Title = "Nothing to save",
                Content = $"{subject} has no tech installed, so there's nothing to capture as a template.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot,
            }.ShowAsync();
            return null;
        }

        var nameBox = new TextBox { PlaceholderText = placeholder };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nameBox, label);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = label }, nameBox } },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        string name = nameBox.Text?.Trim() ?? "";
        return name.Length > 0 ? name : null;
    }
}
