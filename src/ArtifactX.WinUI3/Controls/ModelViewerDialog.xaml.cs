using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The large 3D view: shows the model a preview already built (nothing is rebuilt), sized to most of the
/// window.</summary>
public sealed partial class ModelViewerDialog : ContentDialog
{
    private static bool _open;

    public ModelViewerDialog() => InitializeComponent();

    /// <summary>Opens the large view of <paramref name="source"/>'s model. One at a time - WinUI allows a single
    /// open ContentDialog.</summary>
    public static async Task ShowAsync(ShipPreview source)
    {
        if (_open || source.XamlRoot is not { } root) return;
        var dialog = new ModelViewerDialog { XamlRoot = root };
        dialog.Viewer.Width = Math.Max(320, root.Size.Width * 0.82);
        dialog.Viewer.Height = Math.Max(240, root.Size.Height * 0.68);
        dialog.Opened += (_, _) =>
        {
            dialog.Viewer.ShowFrom(source);
            dialog.Viewer.Focus(FocusState.Programmatic);
        };
        _open = true;
        try { await dialog.ShowAsync(); }
        catch (Exception) { /* another dialog was already open */ }
        finally { _open = false; }
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e) => Viewer.ResetView();
}
