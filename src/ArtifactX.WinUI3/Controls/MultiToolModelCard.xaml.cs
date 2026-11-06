using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A possible multi-tool with its 3D model, built the way the station cabinet cards build theirs
/// (ModelPreviewService.MultiToolAsync).</summary>
public sealed partial class MultiToolModelCard : UserControl
{
    private int _version;
    private ulong? _shownSeed;

    public static readonly DependencyProperty ToolProperty = DependencyProperty.Register(
        nameof(Tool), typeof(SystemMultiToolItemViewModel), typeof(MultiToolModelCard),
        new PropertyMetadata(null, (sender, _) => ((MultiToolModelCard)sender).Refresh()));

    public SystemMultiToolItemViewModel? Tool
    {
        get => (SystemMultiToolItemViewModel?)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public string PreviewName => $"3D preview of {Tool?.Name ?? "the multi-tool"}. Drag to rotate.";

    public MultiToolModelCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => ++_version;
    }

    private void Refresh()
    {
        Bindings.Update();
        if (IsLoaded) _ = BuildAsync();
    }

    private async Task BuildAsync()
    {
        int version = ++_version;
        if (Tool?.Tool is not { } tool || _shownSeed == tool.Seed) return;
        _shownSeed = null;
        Preview.ShowMessage("Building preview...");
        PreviewNote.Text = "Building preview...";
        try
        {
            var (mesh, textures, note) = await ModelPreviewService.MultiToolAsync(tool.ModelPath, tool.Seed);
            if (version != _version || !IsLoaded) return;
            PreviewNote.Text = note;
            if (mesh is null || textures is null) return;
            Preview.Show(mesh, material => ShipPartsService.MaterialColour(material, ShipPartsService.Look),
                material => textures.GetValueOrDefault(material));
            _shownSeed = tool.Seed;
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[MultiToolModelCard] {error.Message}");
            if (version == _version && IsLoaded) PreviewNote.Text = "This model could not be read from your game files.";
        }
    }
}
