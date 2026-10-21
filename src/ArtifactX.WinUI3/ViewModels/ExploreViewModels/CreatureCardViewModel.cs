using ArtifactX.WinUI3.Services;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One creature on a planet's page: what screen recording read off the game's
/// fauna list, and the picture taken of it (the file decides, not a flag - 2026-10-18).</summary>
public sealed partial class CreatureCardViewModel : ObservableObject
{
    private readonly string _picturePath;

    public CreatureCardViewModel(Creature creature, ulong system, int planet)
    {
        Creature = creature;
        _picturePath = CreatureListService.PicturePath(system, planet, creature.Name);
        Traits = string.Join(" · ", new[] { creature.Rarity, creature.Habitat, creature.Activity }.Where(s => s.Length > 0));
        Body = string.Join(" · ", new[] { creature.Gender, creature.Size }.Where(s => s.Length > 0));
        string? noPet = IsExtinct ? "extinct"
            : creature.Habitat == "Underwater" ? "lives underwater"
            : Regex.IsMatch(creature.Habitat, "Fly|Flight|Air|Sky", RegexOptions.IgnoreCase) ? "flies"
            : null;
        CompanionNote = noPet is null ? "" : $"Can't be a companion - {noPet}";
    }

    public Creature Creature { get; }
    public string Name => Creature.Name;
    public string Traits { get; }
    public string Body { get; }
    public string CompanionNote { get; }
    public bool IsExtinct => Creature.Rarity == "Extinct";
    public bool CanCapture => !IsExtinct;
    public string NoPictureText => IsExtinct ? "Extinct - once lived here" : "No picture yet";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicture), nameof(ShowPlaceholder))]
    public partial ImageSource? Picture { get; private set; }

    public bool HasPicture => Picture is not null;
    public bool ShowPlaceholder => Picture is null;

    /// <summary>Reads the picture off the UI thread; call on the UI thread (the bitmap is made there).</summary>
    public async Task LoadPictureAsync()
    {
        byte[]? bytes = await Task.Run(() => File.Exists(_picturePath) ? File.ReadAllBytes(_picturePath) : null);
        if (bytes is null) return;
        var image = new BitmapImage();
        using var stream = new MemoryStream(bytes);
        await image.SetSourceAsync(stream.AsRandomAccessStream());
        Picture = image;
    }
}
