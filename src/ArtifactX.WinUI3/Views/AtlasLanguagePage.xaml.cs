using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class AtlasLanguagePage : Page
{
    public AtlasLanguagePage()
    {
        InitializeComponent();
        WordsControl.Initialize("Atlas", "ATLAS");
    }
}
