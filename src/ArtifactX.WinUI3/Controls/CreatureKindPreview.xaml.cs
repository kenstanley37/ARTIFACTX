using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The 3D model of one kind of creature (a GcCreatureData Id, as the Creature Finder's Kind filter holds it), built
/// like a creature card's (CreatureModelService). Kind is the id; KindName what a screen reader says. Hidden while no
/// kind is set or the model can't be built.
/// </summary>
public sealed partial class CreatureKindPreview : UserControl
{
    private int _version;
    private string? _shownRig;

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(string), typeof(CreatureKindPreview),
            new PropertyMetadata("", (sender, e) => _ = ((CreatureKindPreview)sender).ShowAsync()));

    public static readonly DependencyProperty KindNameProperty =
        DependencyProperty.Register(nameof(KindName), typeof(string), typeof(CreatureKindPreview),
            new PropertyMetadata("", (sender, _) => ((CreatureKindPreview)sender).Bindings.Update()));

    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string KindName { get => (string)GetValue(KindNameProperty); set => SetValue(KindNameProperty, value); }

    public string PreviewName => $"3D model of a {KindName}. Drag to rotate.";

    public CreatureKindPreview()
    {
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        Unloaded += (_, _) => ++_version;
    }

    private async Task ShowAsync()
    {
        int version = ++_version;
        if (string.IsNullOrEmpty(Kind) || GameDataService.Game is not { } game)
        {
            Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            string? scene = PlanetTypesService.CreatureTables?.Creatures.FirstOrDefault(c => c.Id == Kind)?.Scene;
            string rig = await CreatureModelService.ResolveRigAsync(Kind, scenePath: scene) ?? "antelope";
            if (version != _version) return;
            if (rig == _shownRig) { Visibility = Visibility.Visible; return; }
            var mesh = await CreatureModelService.BuildCreatureMeshAsync(game, rig);
            if (version != _version || mesh is null) return;
            Preview.StartYawOffset = Math.PI;
            Preview.Show(mesh, material => CreatureModelService.CreatureMaterialColour(material));
            _shownRig = rig;
            Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CreatureKindPreview] Model build error: {ex.Message}");
        }
    }
}
