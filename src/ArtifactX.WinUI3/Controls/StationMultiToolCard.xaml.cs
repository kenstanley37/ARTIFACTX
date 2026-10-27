using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A wrapping cabinet card using the same parts, colours and renderer as MultiToolPage.</summary>
public sealed partial class StationMultiToolCard : UserControl
{
    private int _version;
    private (string Path, ulong Seed)? _previewIdentity;
    private ItemsControl? _host;
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(StationReloadToolCard), typeof(StationMultiToolCard),
        new PropertyMetadata(null, (sender, _) => ((StationMultiToolCard)sender).RefreshPreview()));
    public StationReloadToolCard? ViewModel
    {
        get => (StationReloadToolCard?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public StationMultiToolCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(this);
            while (parent is not null && parent is not ItemsControl) parent = VisualTreeHelper.GetParent(parent);
            _host = parent as ItemsControl;
            if (_host is not null) _host.SizeChanged += HostSizeChanged;
            FitWidth();
            RefreshPreview();
        };
        Unloaded += (_, _) =>
        {
            ++_version;
            if (_host is not null) _host.SizeChanged -= HostSizeChanged;
            _host = null;
        };
    }
    private void HostSizeChanged(object sender, SizeChangedEventArgs args) => FitWidth();
    private void FitWidth() => Width = Math.Min(600, Math.Max(1, _host?.ActualWidth ?? 600));
    private void RefreshPreview()
    {
        if (IsLoaded) _ = BuildPreviewAsync();
    }
    private async Task BuildPreviewAsync()
    {
        int version = ++_version;
        var tool = ViewModel;
        if (tool is null) return;
        if (_previewIdentity == (tool.ModelPath, tool.ModelSeed)) return;
        _previewIdentity = null;
        Preview.ShowMessage("Building preview...");
        PreviewNote.Text = "Building preview...";
        try
        {
            if (GameDataService.Game is not { } game) { PreviewNote.Text = "Game files unavailable for preview."; return; }
            var models = await ModelPreviewService.ModelsAsync(game);
            await MultiToolKinds.EnsureTreesAsync();
            if (version != _version || !IsLoaded) return;
            if (models is null) { PreviewNote.Text = "This model could not be read from your game files."; return; }
            var root = MultiToolKinds.Load(ShipParts.DescriptorFor(tool.ModelPath));
            var options = root is null ? new HashSet<string>(StringComparer.Ordinal)
                : ModelPreviewService.AllOptionIds(root, MultiToolKinds.Load, StringComparer.Ordinal);
            var chosen = root is null ? new HashSet<string>(StringComparer.Ordinal)
                : ShipParts.Generate(tool.ModelPath, tool.ModelSeed, MultiToolKinds.Load).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            var mesh = await Task.Run(() => models.Build(tool.ModelPath, options, chosen));
            if (version != _version || !IsLoaded) return;
            if (mesh is null) { PreviewNote.Text = "This model could not be read from your game files."; return; }
            var colour = await ModelPreviewService.SeedColoursAsync(tool.ModelSeed);
            var textures = await ModelPreviewService.PaintAsync(models, mesh, colour ?? ((_, _) => null), tool.ModelSeed);
            if (version != _version || !IsLoaded) return;
            Preview.Show(mesh, material => ShipPartsService.MaterialColour(material, ShipPartsService.Look),
                material => textures.GetValueOrDefault(material));
            _previewIdentity = (tool.ModelPath, tool.ModelSeed);
            PreviewNote.Text = "Drag to rotate. Colours may differ slightly from the game.";
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationToolPreview] {error.Message}");
            if (version == _version && IsLoaded) PreviewNote.Text = "This model could not be read from your game files.";
        }
    }
}
