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
    public Creature Creature { get; }
    public IReadOnlyList<CreatureModelService.CreatureRigOption> AvailableRigs => CreatureModelService.AvailableRigs;

    private List<string>? _companionDescriptors;
    private bool _loading = true;

    public CreaturePreviewControl(Creature creature)
    {
        Creature = creature;
        InitializeComponent();
        Loaded += CreaturePreviewControl_Loaded;
    }

    private async void CreaturePreviewControl_Loaded(object sender, RoutedEventArgs e)
    {
        CreatureNameTxt.Text = string.IsNullOrEmpty(Creature.Name) ? "Planetary Creature" : Creature.Name;
        var traits = new[] { Creature.Rarity, Creature.Habitat, Creature.Activity, Creature.Gender, Creature.Size }
            .Where(s => !string.IsNullOrEmpty(s));
        CreatureTraitsTxt.Text = string.Join(" · ", traits);

        string? initialRig = await FindMatchingCompanionRigAsync();

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
            await ModelPreview.ShowCreatureAsync(matchingOption.RigId, _companionDescriptors);
        }
    }

    private static readonly string[] LandRigs = [
        "antelope", "cat", "cow", "triceratops", "trex", "strider",
        "spider", "beetle", "sixleggedcow", "rodent", "blob"
    ];

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
            await ModelPreview.ShowCreatureAsync(option.RigId, _companionDescriptors);
        }
    }

    /// <summary>Displays a 3D creature anatomy viewer dialog.</summary>
    public static async Task ShowAsync(XamlRoot root, Creature creature)
    {
        var control = new CreaturePreviewControl(creature);
        var dialog = new ContentDialog
        {
            Title = "Creature 3D Anatomy",
            Content = control,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root
        };
        await dialog.ShowAsync();
    }
}

