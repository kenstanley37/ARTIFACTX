using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class LoadingViewModel : ObservableObject
{
    [ObservableProperty]
    public partial double LogoProgress { get; set; }

    [ObservableProperty]
    public partial double TitleOpacity { get; set; }

    [ObservableProperty]
    public partial double BarProgress { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Loads in about a second, then opens Home.";

    public event EventHandler? Completed;

    [RelayCommand]
    private void SkipToHome()
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
