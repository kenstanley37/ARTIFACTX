using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

public sealed partial class AutophageLanguagePage : Page
{
    public AutophageLanguagePage()
    {
        InitializeComponent();
        WordsControl.Initialize("Autophage", "BUI");
    }
}
