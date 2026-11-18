using ArtifactX.Maui.Services;

namespace ArtifactX.Maui.Controls;

/// <summary>The galaxy bar: shows GalaxyService.Current and changes it. Listens only while on screen.</summary>
public partial class GalaxyBar : ContentView
{
    private bool _syncing;

    public GalaxyBar()
    {
        InitializeComponent();
        GalaxyPicker.ItemsSource = GalaxyService.All.ToList();
        Loaded += (_, _) =>
        {
            GalaxyService.Changed += Sync;
            Sync();
        };
        Unloaded += (_, _) => GalaxyService.Changed -= Sync;
    }

    private void Sync()
    {
        _syncing = true;
        try
        {
            GalaxyPicker.SelectedIndex = GalaxyService.Current - 1;
            SemanticProperties.SetDescription(GalaxyPicker, $"Galaxy you're viewing: {GalaxyService.CurrentOption.DisplayName}. Tap to change it.");
        }
        finally { _syncing = false; }
    }

    private void GalaxyPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncing || GalaxyPicker.SelectedIndex < 0) return;
        GalaxyService.Set(GalaxyPicker.SelectedIndex + 1);
    }

    private void Change_Tapped(object? sender, TappedEventArgs e) => GalaxyPicker.Focus();
}
