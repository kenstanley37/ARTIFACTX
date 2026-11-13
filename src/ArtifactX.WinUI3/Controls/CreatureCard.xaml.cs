using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed record CreatureCardRequest(CreatureCardViewModel Creature, FrameworkElement Anchor);

public sealed partial class CreatureCard : UserControl
{
    private int _version;
    private string? _shownRig;

    public static readonly DependencyProperty CreatureProperty =
        DependencyProperty.Register(nameof(Creature), typeof(CreatureCardViewModel), typeof(CreatureCard),
            new PropertyMetadata(null, (sender, _) => ((CreatureCard)sender).Refresh()));

    public CreatureCardViewModel? Creature
    {
        get => (CreatureCardViewModel?)GetValue(CreatureProperty);
        set => SetValue(CreatureProperty, value);
    }

    public string PreviewName => $"3D model of {Creature?.Name ?? "creature"}. Drag to rotate.";

    public event EventHandler<CreatureCardRequest>? DeleteRequested;
    public event EventHandler<CreatureCardRequest>? Preview3DRequested;

    public CreatureCard()
    {
        InitializeComponent();
        Loaded += (_, _) => _ = BuildModelAsync();
        Unloaded += (_, _) => ++_version;
    }

    private void Refresh()
    {
        Bindings.Update();
        _ = BuildModelAsync();
    }

    private async Task BuildModelAsync()
    {
        int version = ++_version;
        if (Creature is not { } card || GameDataService.Game is not { } game) return;
        string shown = $"{card.RigId}|{card.ModelSeed:X}";
        if (_shownRig == shown) return;

        try
        {
            // The creature's own body parts from its seed (issue #15); the rig's defaults when not known.
            var (parts, extra) = await CreatureModelService.ModelPartsAsync(card.ModelScene, card.ModelExtraScene, card.ModelSeed,
                card.ModelFilter, card.ModelSubType);
            if (version != _version) return;
            var mesh = await CreatureModelService.BuildCreatureMeshAsync(game, card.RigId, parts, wild: true, extra);
            if (version != _version) return;
            if (mesh is null) return;

            Preview.StartYawOffset = Math.PI;
            var textures = card.IsSilhouette ? null : await CreatureModelService.PaintAsync(game, mesh, card.ModelSeed, size: 128);
            if (version != _version) return;
            Preview.Show(mesh, material => CreatureModelService.CreatureMaterialColour(material),
                textures is null ? null : (Func<string, ArtifactX.GameData.ModelTexture?>)(m => textures.GetValueOrDefault(m)), silhouette: card.IsSilhouette);
            _shownRig = shown;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreatureCard] Model build error: {ex.Message}");
        }
    }

    private async void View3DModel_Click(object sender, RoutedEventArgs e)
    {
        if (Creature is { } creature)
        {
            if (Preview3DRequested is not null)
                Preview3DRequested.Invoke(this, new(creature, (FrameworkElement)sender));
            else
            {
                var (parts, extra) = await CreatureModelService.ModelPartsAsync(creature.ModelScene, creature.ModelExtraScene,
                    creature.ModelSeed, creature.ModelFilter, creature.ModelSubType);
                await CreaturePreviewControl.ShowAsync(XamlRoot, creature.Creature, creature.RigId, creature.IsSilhouette, parts, extra, creature.ModelSeed);
            }
        }
    }

    private void Preview_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        View3DModel_Click(sender, e);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Creature is { } creature) DeleteRequested?.Invoke(this, new(creature, (FrameworkElement)sender));
    }
}
