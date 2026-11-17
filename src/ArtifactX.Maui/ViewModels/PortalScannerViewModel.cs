using System.Collections.ObjectModel;
using System.Globalization;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Theming;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Services;
using ArtifactX.Maui.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>
/// Retains committed input while a separate scan candidate is reviewed. A failed/cancelled scan
/// never changes the accepted address. Manual and imported addresses retain a player-selected galaxy; live scanning reads the galaxy from screen text.
/// </summary>
public sealed partial class PortalScannerViewModel(IPortalPhotoScanService scanner, ISystemNavigationService navigation) : ObservableObject
{
    private CancellationTokenSource? _scanCancellation;
    public ObservableCollection<PortalReviewSlot> Slots { get; } = new(Enumerable.Range(1, 12).Select(i => new PortalReviewSlot(i)));
    public IReadOnlyList<PortalGlyphVector> Keys { get; } = ArtifactXPortalGlyphs.All;
    public IReadOnlyList<PortalGalaxyOption> Galaxies { get; } = NmsGalaxyInfo.AllGalaxies
        .Select(g => new PortalGalaxyOption(g.Number, g.Name)).ToArray();

    [ObservableProperty]
    public partial PortalGalaxyOption SelectedGalaxy { get; set; } = new(1, "Euclid");
    [ObservableProperty]
    public partial string AddressInput { get; set; } = "";
    [ObservableProperty]
    public partial string ReviewInput { get; set; } = "";
    [ObservableProperty]
    public partial bool HasReview { get; set; }
    [ObservableProperty]
    public partial bool IsKeypadOpen { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    public partial bool IsBusy { get; set; }
    public bool CanEdit => !IsBusy;
    [ObservableProperty]
    public partial string Status { get; set; } = "Scan the glyphs and galaxy name on your screen, or enter an address by hand.";
    [ObservableProperty]
    public partial string SystemSummary { get; set; } = "";
    [ObservableProperty]
    public partial int SelectedPosition { get; set; } = 1;

    partial void OnSelectedGalaxyChanged(PortalGalaxyOption value) => SystemSummary = "";
    partial void OnAddressInputChanged(string value) => SystemSummary = "";
    partial void OnSelectedPositionChanged(int value)
    {
        foreach (var slot in Slots) slot.IsSelected = slot.Position == value;
    }

    partial void OnReviewInputChanged(string value)
    {
        string code = value.Trim().ToUpperInvariant();
        for (int i = 0; i < Slots.Count; i++)
        {
            char? next = i < code.Length && Uri.IsHexDigit(code[i]) ? code[i] : null;
            if (Slots[i].Digit != next) Slots[i].NeedsReview = false;
            Slots[i].Digit = next;
        }
    }

    [RelayCommand]
    private Task StartLiveScanAsync() => navigation.StartScanAsync();
    [RelayCommand]
    private Task ChoosePhotoAsync() => ScanAsync();

    private async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _scanCancellation = new CancellationTokenSource();
        Status = "Reading portal glyphs…";
        try
        {
            var result = await scanner.ScanAsync(_scanCancellation.Token);
            if (result is null) { Status = "Photo selection cancelled."; return; }
            Status = result.Message;
            if (!result.HasCandidate) return;
            ReviewInput = result.Candidate!;
            for (int i = 0; i < 12; i++) Slots[i].NeedsReview = !result.Glyphs[i].IsCertain;
            SelectedPosition = Slots.FirstOrDefault(s => s.NeedsReview)?.Position ?? 1;
            HasReview = true;
            IsKeypadOpen = false;
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception) { Status = "The photo could not be read. Try another photo or enter the glyphs by hand."; }
        finally { IsBusy = false; _scanCancellation.Dispose(); _scanCancellation = null; }
    }

    [RelayCommand]
    private void CancelScan() => _scanCancellation?.Cancel();

    [RelayCommand]
    private void SelectSlot(PortalReviewSlot? slot)
    {
        if (slot is not null)
        {
            SelectedPosition = slot.Position;
            IsKeypadOpen = true;
        }
    }

    [RelayCommand]
    private void TapGlyph(PortalGlyphVector? glyph)
    {
        if (glyph is null) return;
        if (!HasReview) { ReviewInput = AddressInput; HasReview = true; }
        var chars = Enumerable.Range(0, 12).Select(i => Slots[i].Digit ?? '_').ToArray();
        chars[SelectedPosition - 1] = glyph.HexDigit;
        ReviewInput = new string(chars);
        SelectedPosition = Math.Min(12, SelectedPosition + 1);
    }

    [RelayCommand]
    private void EnterManually()
    {
        ReviewInput = AddressInput;
        HasReview = true;
        IsKeypadOpen = true;
        SelectedPosition = 1;
    }

    [RelayCommand]
    private void AcceptReview()
    {
        if (!NmsPortalCode.TryParse(ReviewInput, out string code, out string error))
        { Status = string.IsNullOrWhiteSpace(error) ? "Enter all 12 glyphs." : error; return; }
        AddressInput = code;
        HasReview = false;
        IsKeypadOpen = false;
        SystemSummary = "";
        Status = $"Address ready · {NmsPortalCode.ToCoordinates(code)}. Select the galaxy before looking up the system.";
    }

    [RelayCommand]
    private void DiscardReview() { HasReview = false; IsKeypadOpen = false; }

    [RelayCommand]
    private void CloseKeypad() => IsKeypadOpen = false;

    [RelayCommand]
    private async Task FindSystemAsync()
    {
        if (IsBusy) return;
        if (!NmsPortalCode.TryParse(AddressInput, out string code, out string error))
        { Status = string.IsNullOrWhiteSpace(error) ? "Enter all 12 glyphs." : error; return; }
        int galaxy = SelectedGalaxy.Number;
        IsBusy = true;
        try
        {
            await navigation.OpenAsync(code, galaxy);
        }
        catch (Exception) { Status = "The system could not be generated from this address."; }
        finally { IsBusy = false; }
    }
}
