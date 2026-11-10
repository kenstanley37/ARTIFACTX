using ArtifactX.Core.Universe;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class ShipFilterPillViewModel : ObservableObject
{
    public ShipFilterPillViewModel(string key, string label, int count, bool isSelected)
    {
        Key = key;
        Label = $"{label} ({count})";
        IsSelected = isSelected;
    }

    public string Key { get; }
    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
