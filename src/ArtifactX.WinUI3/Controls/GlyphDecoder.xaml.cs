using ArtifactX.Core.NmsModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class GlyphDecoder : UserControl
{
    public GlyphDecoder() => InitializeComponent();

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool ok = NmsPortalCode.TryParse(InputBox.Text, out string code, out string error);
        ErrorTxt.Text = error;
        ErrorTxt.Visibility = error.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        Glyphs.Code = CopyBtn.Code = ok ? code : "";
        if (!ok) return;
        DetailTxt.Text = InputBox.Text.Contains(':')
            ? $"Portal code {code} (planet 1 - coordinates don't include a planet)"
            : $"Portal code {code} · galactic coordinates {NmsPortalCode.ToCoordinates(code)} · planet {code[0]}";
    }
}
