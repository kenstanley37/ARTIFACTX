using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One fish card on FishingPage. The catch fields and the selection
/// highlight change in place on live updates (UpdateFrom), so the grid isn't
/// rebuilt - rebuilding made it blink and jump back to the top.</summary>
public sealed class FishCardViewModel : INotifyPropertyChanged
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public ImageSource? Icon { get; init; }
    public required string Rarity { get; init; }
    public required Brush RarityBrush { get; init; }
    public required string Traits { get; init; }
    public required string Habitat { get; init; }

    private string _caught = "";
    private string _justCaught = "";
    private Brush? _caughtBrush;
    private double _cardOpacity = 1;
    private string _spots = "";
    private Visibility _spotsVisibility = Visibility.Collapsed;
    private Brush? _cardBorder;
    private Brush? _cardBackground;

    public string Caught { get => _caught; set => Set(ref _caught, value); }

    /// <summary>"JUST CAUGHT" / "NEW CATCH" for a fish the game saved a catch of while the page was open.</summary>
    public string JustCaught
    {
        get => _justCaught;
        set { Set(ref _justCaught, value); OnChanged(nameof(JustCaughtVisibility)); }
    }

    public Visibility JustCaughtVisibility => JustCaught.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Brush? CaughtBrush { get => _caughtBrush; set => Set(ref _caughtBrush, value); }
    public double CardOpacity { get => _cardOpacity; set => Set(ref _cardOpacity, value); }
    public string Spots { get => _spots; set => Set(ref _spots, value); }
    public Visibility SpotsVisibility { get => _spotsVisibility; set => Set(ref _spotsVisibility, value); }
    public Brush? CardBorder { get => _cardBorder; set => Set(ref _cardBorder, value); }
    public Brush? CardBackground { get => _cardBackground; set => Set(ref _cardBackground, value); }

    /// <summary>Copies the fields that can change between live updates.</summary>
    public void UpdateFrom(FishCardViewModel other)
    {
        Caught = other.Caught;
        JustCaught = other.JustCaught;
        CaughtBrush = other.CaughtBrush;
        CardOpacity = other.CardOpacity;
        Spots = other.Spots;
        SpotsVisibility = other.SpotsVisibility;
        CardBorder = other.CardBorder;
        CardBackground = other.CardBackground;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        OnChanged(name);
    }

    private void OnChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
