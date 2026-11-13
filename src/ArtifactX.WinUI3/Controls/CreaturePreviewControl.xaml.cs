using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class CreaturePreviewControl : UserControl
{
    public static readonly string[] LandRigs = [
        "antelope", "cat", "cow", "triceratops", "trex", "strider",
        "spider", "beetle", "sixleggedcow", "rodent", "blob"
    ];

    public Creature Creature { get; }
    public string? PreferredRig { get; }
    public bool IsSilhouette { get; }
    /// <summary>The known rigs, plus the creature's own model when it is a scene outside them (the floral cat, robots).</summary>
    public IReadOnlyList<CreatureModelService.CreatureRigOption> AvailableRigs { get; }

    private List<string>? _companionDescriptors;
    private bool _wild;   // a wild creature's parts were handed in: drawn without the pet harness
    private readonly CreatureModelService.ExtraModel? _extra;   // the scene spawned with it, drawn with its own rig only
    private ulong _seed, _colourSeed;   // its look seeds (CreatureModelService.PaintAsync), for its own rig only
    private bool _loading = true;

    public CreaturePreviewControl(Creature creature, string? preferredRig = null, bool isSilhouette = false,
        IReadOnlyList<string>? parts = null, CreatureModelService.ExtraModel? extra = null, ulong seed = 0)
    {
        Creature = creature;
        _extra = extra;
        _seed = seed;
        // A wild creature's body parts from its seed (CreatureModelService.PartsForSeedAsync), shown like a companion's.
        if (parts is { Count: > 0 }) { _companionDescriptors = parts.ToList(); _wild = true; }
        PreferredRig = preferredRig;
        AvailableRigs = preferredRig is not null && CreatureModelService.IsScenePath(preferredRig)
            ? (CreatureModelService.CreatureRigOption[])[new(preferredRig, CreatureModelService.HumanizeRig(preferredRig)), .. CreatureModelService.AvailableRigs]
            : CreatureModelService.AvailableRigs;
        IsSilhouette = isSilhouette;
        InitializeComponent();
        Loaded += CreaturePreviewControl_Loaded;
    }

    private async void CreaturePreviewControl_Loaded(object sender, RoutedEventArgs e)
    {
        CreatureNameTxt.Text = string.IsNullOrEmpty(Creature.Name) ? "Planetary Creature" : Creature.Name;
        var traits = new[] { Creature.Rarity, Creature.Habitat, Creature.Activity, Creature.Gender, Creature.Size }
            .Where(s => !string.IsNullOrEmpty(s));
        CreatureTraitsTxt.Text = string.Join(" · ", traits);

        string? initialRig = PreferredRig;
        if (string.IsNullOrEmpty(initialRig))
        {
            initialRig = await FindMatchingCompanionRigAsync();
        }

        if (string.IsNullOrEmpty(initialRig))
        {
            initialRig = GuessRigFromTraits(Creature);
        }

        var matchingOption = AvailableRigs.FirstOrDefault(r => string.Equals(r.RigId, initialRig, StringComparison.OrdinalIgnoreCase))
            ?? AvailableRigs.FirstOrDefault(r => r.RigId == "anteloperobot")
            ?? AvailableRigs.FirstOrDefault();

        _loading = false;
        if (matchingOption is not null)
        {
            RigComboBox.SelectedItem = matchingOption;
            await ShowRigAsync(matchingOption.RigId);
        }
    }

    /// <summary>The extra scene and the seed colours belong to the creature's own rig (or the matched companion's);
    /// another rig picked in the box is drawn alone, in plain shading.</summary>
    private Task ShowRigAsync(string rigId)
    {
        bool own = string.Equals(rigId, PreferredRig, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(PreferredRig);
        return ModelPreview.ShowCreatureAsync(rigId, _companionDescriptors, silhouette: IsSilhouette, wild: _wild,
            extra: own ? _extra : null, seed: own ? _seed : 0, colourSeed: own ? _colourSeed : 0);
    }

    private async Task<string?> FindMatchingCompanionRigAsync()
    {
        if (SaveSessionManager.GetValue(NmsCompanionPaths.CompanionArrayPath) is not JArray array)
            return null;

        var nameService = await CreatureNameService.LoadAsync();

        for (int i = 0; i < array.Count; i++)
        {
            string xid = (string?)array[i]?["XID"] ?? "";
            if (string.IsNullOrEmpty(xid.TrimStart('^'))) continue;

            string customName = (string?)array[i]?["fH8"] ?? "";
            ulong seedA = NmsSpaceStationPaths.ParsePackedAddress(array[i]?["m9o"]) ?? 0;
            ulong seedB = NmsSpaceStationPaths.ParsePackedAddress(array[i]?["JrL"]) ?? 0;
            string compSpecies = nameService.Companion(seedA, seedB) ?? "";

            bool nameMatches = !string.IsNullOrEmpty(customName) &&
                (string.Equals(customName, Creature.Name, StringComparison.OrdinalIgnoreCase) ||
                 customName.Contains(Creature.Name, StringComparison.OrdinalIgnoreCase) ||
                 Creature.Name.Contains(customName, StringComparison.OrdinalIgnoreCase));

            bool speciesMatches = !string.IsNullOrEmpty(compSpecies) &&
                (string.Equals(compSpecies, Creature.Name, StringComparison.OrdinalIgnoreCase) ||
                 compSpecies.Contains(Creature.Name, StringComparison.OrdinalIgnoreCase) ||
                 Creature.Name.Contains(compSpecies, StringComparison.OrdinalIgnoreCase));

            if (nameMatches || speciesMatches)
            {
                var oslToken = array[i]?["osl"];
                var descriptors = new List<string>();
                if (oslToken is JArray osl)
                {
                    foreach (var item in osl)
                    {
                        var val = (string?)item;
                        if (!string.IsNullOrEmpty(val)) descriptors.Add(val);
                    }
                }
                _companionDescriptors = descriptors;
                (_seed, _colourSeed) = CreatureModelService.CompanionLookSeeds(array[i]);

                CompanionBadge.Visibility = Visibility.Visible;
                CompanionBadgeTxt.Text = $"Owned Companion (Slot {i + 1})";

                return await CreatureModelService.ResolveRigAsync(xid, descriptors);
            }
        }

        return null;
    }

    private static string GuessRigFromTraits(Creature creature)
    {
        string hab = creature.Habitat ?? "";
        if (hab.Contains("Underwater", StringComparison.OrdinalIgnoreCase) || hab.Contains("Water", StringComparison.OrdinalIgnoreCase))
            return "shark";
        if (Regex.IsMatch(hab, "Fly|Flight|Air|Sky", RegexOptions.IgnoreCase))
            return "beetle";

        uint hash = 2166136261;
        foreach (char c in creature.Name)
            hash = (hash ^ c) * 16777619;
        return LandRigs[hash % (uint)LandRigs.Length];
    }

    private async void RigComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (RigComboBox.SelectedItem is CreatureModelService.CreatureRigOption option)
        {
            await ShowRigAsync(option.RigId);
        }
    }

    /// <summary>Displays a 3D creature anatomy viewer dialog.</summary>
    public static async Task ShowAsync(XamlRoot root, Creature creature, string? rigId = null, bool isSilhouette = false,
        IReadOnlyList<string>? parts = null, CreatureModelService.ExtraModel? extra = null, ulong seed = 0)
    {
        var control = new CreaturePreviewControl(creature, rigId, isSilhouette, parts, extra, seed);
        var dialog = new ContentDialog
        {
            Title = isSilhouette ? "Undiscovered Species 3D Silhouette" : "Creature 3D Anatomy",
            Content = control,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root
        };
        await dialog.ShowAsync();
    }
}
