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
    private void FitWidth() => Width = Math.Min(460, Math.Max(1, _host?.ActualWidth ?? 460));

    private void Card_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject obj)
        {
            while (obj is not null)
            {
                if (obj is Button || obj is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton) return;
                obj = VisualTreeHelper.GetParent(obj);
            }
        }
        if (ViewModel is not null)
        {
            ViewModel.IsSelected = !ViewModel.IsSelected;
        }
    }

    private void SlotsToggle_Changed(object sender, RoutedEventArgs e)
    {
        bool shown = SlotsToggle.IsChecked == true;
        SlotsHost.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        SlotsToggle.Content = shown ? "Hide slots" : "Show all slots";
        NameSlotsToggle();
    }
    /// <summary>A station has several of these cards; the toggle says whose slots it shows.</summary>
    private void NameSlotsToggle() => Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SlotsToggle,
        $"{(SlotsToggle.IsChecked == true ? "Hide" : "Show all")} slots of {ViewModel?.Name ?? "this multi-tool"}");
    private void RefreshPreview()
    {
        NameSlotsToggle();
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
            var (mesh, textures, note) = await ModelPreviewService.MultiToolAsync(tool.ModelPath, tool.ModelSeed);
            if (version != _version || !IsLoaded) return;
            PreviewNote.Text = note;
            if (mesh is null || textures is null) return;
            Preview.Show(mesh, material => ShipPartsService.MaterialColour(material, ShipPartsService.Look),
                material => textures.GetValueOrDefault(material));
            _previewIdentity = (tool.ModelPath, tool.ModelSeed);
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationToolPreview] {error.Message}");
            if (version == _version && IsLoaded) PreviewNote.Text = "This model could not be read from your game files.";
        }
    }
}
