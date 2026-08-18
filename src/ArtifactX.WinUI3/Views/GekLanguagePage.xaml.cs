using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class GekLanguagePage : Page
{
    public GekLanguagePage()
    {
        InitializeComponent();
        WordsControl.Initialize("Gek", "TRA");
    }
}
