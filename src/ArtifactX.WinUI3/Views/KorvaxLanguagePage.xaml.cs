using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class KorvaxLanguagePage : Page
{
    public KorvaxLanguagePage()
    {
        InitializeComponent();
        WordsControl.Initialize("Korvax", "EXP");
    }
}
