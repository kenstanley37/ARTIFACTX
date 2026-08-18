using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class VyKeenLanguagePage : Page
{
    public VyKeenLanguagePage()
    {
        InitializeComponent();
        WordsControl.Initialize("Vy'keen", "WAR");
    }
}
