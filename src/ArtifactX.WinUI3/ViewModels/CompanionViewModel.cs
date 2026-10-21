using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using libMBIN.NMS.GameComponents;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using P = ArtifactX.Core.NmsModels.NmsCompanionPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The selected pet on the Companions page: one instance for the page's lifetime,
/// reloaded in place (LoadAsync) so a box being typed in keeps its focus. Only the
/// fields confirmed against real save data (see NmsCompanionPaths) are shown as
/// fields - Current Mood, Age, Gender, the fancy species name and Weight have no
/// confirmed backing field. Personality Traits ARE real and stored (TraitsPath).
/// Every edit is staged straight into the save session.
/// </summary>
public sealed partial class CompanionViewModel : ObservableObject
{
    public static IReadOnlyList<string> ClassLetters { get; } = new[] { "S", "A", "B", "C" };

    // Species archetype (XID, e.g. "RODENT") -> catalog data, loaded once.
    private static Dictionary<string, (string DisplayName, string Rarity, string Description)>? _species;

    // Rig id (species XID lowercased, e.g. "trex") -> its full descriptor option tree,
    // fetched once per rig and shared by pets of the same species.
    private readonly Dictionary<string, List<CreatureDescriptorNode>> _trees = new();

    private int _index = -1;
    private bool _loading;
    private List<CreatureDescriptorNode> _tree = new();

    public CompanionViewModel()
    {
        Trust = Field("Trust", v => Stage(v / 100.0, P.TrustPath(_index)));
        Trait1 = TraitField(1);
        Trait2 = TraitField(2);
        Trait3 = TraitField(3);
        GenesImproved = Field("Genes improved", v => Stage((int)v, P.GenesImprovedPath(_index)));
        MutationProgress = Field("Mutation progress", v => Stage(v / 100.0, P.MutationProgressPath(_index)));
        Victories = Field("Holo-arena victories", v => Stage((int)v, P.HoloArenaVictoriesPath(_index)));
        AgilityPoints = PointsField("Agility", 0);
        HealthPoints = PointsField("Health", 1);
        CombatPoints = PointsField("Combat", 2);
    }

    /// <summary>A name was committed: the picker's labels change.</summary>
    public event EventHandler? NameChanged;

    public int Index => _index;

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Species { get; private set; } = "";
    [ObservableProperty] public partial string Climate { get; private set; } = "";
    [ObservableProperty] public partial string Rarity { get; private set; } = "";

    public NumberFieldViewModel Trust { get; }
    public NumberFieldViewModel Trait1 { get; }
    public NumberFieldViewModel Trait2 { get; }
    public NumberFieldViewModel Trait3 { get; }
    public NumberFieldViewModel GenesImproved { get; }
    public NumberFieldViewModel MutationProgress { get; }
    public NumberFieldViewModel Victories { get; }
    public NumberFieldViewModel AgilityPoints { get; }
    public NumberFieldViewModel HealthPoints { get; }
    public NumberFieldViewModel CombatPoints { get; }

    /// <summary>Overrides only the primary Seed's contribution to colour while on.</summary>
    [ObservableProperty] public partial bool ColourOverrideActive { get; set; }
    [ObservableProperty] public partial string ColourOverrideSeed { get; set; } = "";

    /// <summary>While on, the Battle Abilities badges come from the class letters, not the seeds.</summary>
    [ObservableProperty] public partial bool LetterOverrideActive { get; set; }
    [ObservableProperty] public partial int HealthLetter { get; set; } = -1;
    [ObservableProperty] public partial int AgilityLetter { get; set; } = -1;
    [ObservableProperty] public partial int CombatLetter { get; set; } = -1;

    /// <summary>Body parts: one row per descriptor (osl) entry.</summary>
    public ObservableCollection<CompanionPartRowViewModel> Parts { get; } = new();

    /// <summary>No catalog data for this species' rig - nothing to edit.</summary>
    [ObservableProperty] public partial bool NoPartsData { get; private set; }

    /// <summary>Every other raw field, with no confirmed in-game effect yet.</summary>
    public ObservableCollection<CompanionRawFieldViewModel> RawFields { get; } = new();

    // ------------------------------------------------------------ loading

    public void Clear()
    {
        _index = -1;
        _loading = true;
        Name = Species = Climate = Rarity = ColourOverrideSeed = "";
        ColourOverrideActive = LetterOverrideActive = false;
        HealthLetter = AgilityLetter = CombatLetter = -1;
        foreach (var field in NumberFields) field.Load(double.NaN);
        _loading = false;
        Parts.Clear();
        RawFields.Clear();
        NoPartsData = false;
    }

    public async Task LoadAsync(int index)
    {
        _index = index;
        string archetype = Read<string>(P.SpeciesArchetypePath(index)) ?? "";
        string archetypeId = archetype.TrimStart('^');
        _species ??= await CatalogService.GetCreatureSpeciesAsync();
        string rigId = archetypeId.ToLowerInvariant();
        if (!_trees.TryGetValue(rigId, out var tree))
        {
            tree = await CatalogService.GetCreatureDescriptorTreeAsync(rigId);
            _trees[rigId] = tree;
        }
        if (index != _index) return; // another pet was picked meanwhile
        _tree = tree;

        _loading = true;
        // The real stored name, not the picker's "Species #N" placeholder - empty when
        // the player never renamed this pet.
        Name = Read<string>(P.NamePath(index)) ?? "";
        Species = CompanionService.FormatArchetype(archetype);
        Rarity = _species.TryGetValue(archetypeId, out var species) ? species.Rarity : "";
        Climate = Read<string>(P.NativeClimatePath(index)) ?? "";

        Trust.Load(Math.Round((Read<double?>(P.TrustPath(index)) ?? 0) * 100, 1));
        var traits = SaveSessionManager.GetValue(P.TraitsPath(index)) as JArray;
        double Trait(int i) => traits?.Count > i ? Math.Round(Math.Abs(traits[i].Value<double>()) * 100, 1) : double.NaN;
        Trait1.Load(Trait(0));
        Trait2.Load(Trait(1));
        Trait3.Load(Trait(2));
        GenesImproved.Load(Read<double?>(P.GenesImprovedPath(index)) ?? 0);
        MutationProgress.Load(Math.Round((Read<double?>(P.MutationProgressPath(index)) ?? 0) * 100, 1));
        Victories.Load(Read<double?>(P.HoloArenaVictoriesPath(index)) ?? 0);
        var points = SaveSessionManager.GetValue(P.MutationPointsPath(index)) as JArray;
        double Points(int i) => points?.Count > i ? points[i].Value<double>() : double.NaN;
        AgilityPoints.Load(Points(0));
        HealthPoints.Load(Points(1));
        CombatPoints.Load(Points(2));

        ColourOverrideActive = Read<bool?>(P.ColourBaseSeedActivePath(index)) ?? false;
        ColourOverrideSeed = Read<string>(P.ColourBaseSeedPath(index)) ?? "";
        LetterOverrideActive = Read<bool?>(P.ClassLetterOverrideActivePath(index)) ?? false;
        HealthLetter = LetterIndex(Read<string>(P.ClassLetterPath(index, 0)));
        AgilityLetter = LetterIndex(Read<string>(P.ClassLetterPath(index, 1)));
        CombatLetter = LetterIndex(Read<string>(P.ClassLetterPath(index, 2)));
        _loading = false;

        ShowParts();
        ShowRawFields();
    }

    private IEnumerable<NumberFieldViewModel> NumberFields => new[]
        { Trust, Trait1, Trait2, Trait3, GenesImproved, MutationProgress, Victories, AgilityPoints, HealthPoints, CombatPoints };

    // ------------------------------------------------------------ fields

    private NumberFieldViewModel Field(string label, Action<double> write) =>
        new(label, label, double.NaN, field => { if (_index >= 0) write(field.Value); });

    /// <summary>A trait is shown 0-100; the stored value's sign (which descriptor word
    /// it picks) isn't confirmed, so it's kept and only the magnitude typed is applied.</summary>
    private NumberFieldViewModel TraitField(int number) => Field($"Trait {number}", v =>
        CompanionService.StageArrayEntry(P.TraitsPath(_index), number - 1, 3,
            old => (old.Value<double>() < 0 ? -1.0 : 1.0) * (v / 100.0)));

    /// <summary>Genetic profile points (max 10) per mutation stat.</summary>
    private NumberFieldViewModel PointsField(string label, int slot) => Field(label, v =>
        CompanionService.StageArrayEntry(P.MutationPointsPath(_index), slot, 3, _ => (int)v));

    partial void OnNameChanged(string value)
    {
        string name = value.Trim();
        if (_loading || _index < 0 || name.Length == 0 || name == (Read<string>(P.NamePath(_index)) ?? "")) return;
        Stage(name, P.NamePath(_index));
        NameChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColourOverrideActiveChanged(bool value)
    {
        if (!_loading && _index >= 0) Stage(value, P.ColourBaseSeedActivePath(_index));
    }

    partial void OnColourOverrideSeedChanged(string value)
    {
        if (_loading || _index < 0) return;
        string seed = value.Trim();
        if (seed != (Read<string>(P.ColourBaseSeedPath(_index)) ?? "")) Stage(seed, P.ColourBaseSeedPath(_index));
    }

    /// <summary>Activates AND rerolls together - this is a dormant secondary colour
    /// layer (see NmsCompanionPaths.ColourBaseSeedActivePath), so a random seed behind
    /// an inactive flag wouldn't show anything in-game.</summary>
    public void GenerateColourOverrideSeed()
    {
        if (_index < 0) return;
        string seed = NmsSeedGenerator.GenerateRandom();
        _loading = true;
        ColourOverrideSeed = seed;
        ColourOverrideActive = true;
        _loading = false;
        Stage(seed, P.ColourBaseSeedPath(_index));
        Stage(true, P.ColourBaseSeedActivePath(_index));
    }

    partial void OnLetterOverrideActiveChanged(bool value)
    {
        if (!_loading && _index >= 0) Stage(value, P.ClassLetterOverrideActivePath(_index));
    }

    partial void OnHealthLetterChanged(int value) => StageLetter(0, value);
    partial void OnAgilityLetterChanged(int value) => StageLetter(1, value);
    partial void OnCombatLetterChanged(int value) => StageLetter(2, value);

    /// <summary>Stat order in the save is Health/Agility/Combat here (not the
    /// Agility/Health/Combat order of the mutation points).</summary>
    private void StageLetter(int stat, int letterIndex)
    {
        if (_loading || _index < 0 || letterIndex < 0) return;
        string letter = ClassLetters[letterIndex];
        if (letter != (Read<string>(P.ClassLetterPath(_index, stat)) ?? "")) Stage(letter, P.ClassLetterPath(_index, stat));
    }

    private static int LetterIndex(string? letter) =>
        ClassLetters.ToList().FindIndex(l => string.Equals(l, letter, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------ body parts

    /// <summary>
    /// One dropdown per osl (Descriptors) entry, constrained to that entry's real sibling
    /// options (same Category, same parent) from the rig's own catalog tree. CONFIRMED
    /// WORKING (2026-07-29, real save+reload test) - swapping several rows at once and
    /// reloading in-game rendered every new part correctly.
    ///
    /// Most rows swap ONE entry among its siblings, preserving array length/order. The
    /// exception: on a rig whose tree has exactly ONE distinct root-level category (e.g.
    /// TREX's single "_TREX_" category with 2 alternatives, one leading to TAILB/TOPB and
    /// the other to HEAD/BODY/TAIL), that root row is a genuine archetype choice -
    /// picking another rebuilds the WHOLE array (CompanionService.BuildDefaultDescriptorArray)
    /// since it changes which child slots apply. UNTESTED IN-GAME as of 2026-07-29.
    ///
    /// Real bug caught 2026-07-29: originally ANY root-level row got the rebuild, which
    /// broke rigs like rodent with MULTIPLE independent root categories (HEAD/BODY/TAIL
    /// as peers) - 27 of 71 cataloged rigs have that shape, so only single-root rigs get
    /// archetype treatment.
    /// </summary>
    private void ShowParts()
    {
        Parts.Clear();
        NoPartsData = false;
        if (SaveSessionManager.GetValue(P.DescriptorsPath(_index)) is not JArray osl) return;
        if (_tree.Count == 0)
        {
            NoPartsData = true;
            return;
        }

        var byOptionId = new Dictionary<string, CreatureDescriptorNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _tree) byOptionId[node.OptionId] = node;
        bool singleRootArchetype = _tree.Where(n => n.ParentOptionId == null)
            .Select(n => n.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;

        for (int i = 0; i < osl.Count; i++)
        {
            string raw = osl[i]?.Value<string>() ?? "";
            if (!byOptionId.TryGetValue(raw.TrimStart('^'), out var node))
            {
                // Matches no known option (e.g. the trailing per-instance detail seed):
                // shown read-only so the array stays visible in full.
                Parts.Add(CompanionPartRowViewModel.ReadOnly(i, raw));
                continue;
            }

            var options = _tree
                .Where(n => n.Category == node.Category && n.ParentOptionId == node.ParentOptionId)
                .OrderBy(n => n.OptionId, StringComparer.OrdinalIgnoreCase)
                .Select(n => new CompanionPartOption(n.OptionId, CompanionService.HumanizeDescriptorOption(node.Category, n.OptionId)))
                .ToList();
            int selected = options.FindIndex(o => string.Equals(o.Id, node.OptionId, StringComparison.OrdinalIgnoreCase));
            Parts.Add(new CompanionPartRowViewModel(i, $"{CompanionService.HumanizeDescriptorCategory(node.Category)} (osl[{i}])",
                options, selected, node.ParentOptionId == null && singleRootArchetype, OnPartPicked));
        }
    }

    private void OnPartPicked(CompanionPartRowViewModel row)
    {
        var path = P.DescriptorsPath(_index);
        if (SaveSessionManager.GetValue(path) is not JArray current) return;
        string picked = row.SelectedId;

        if (row.IsArchetype)
        {
            // The archetype decides which other slots exist: rebuild the whole array
            // with default picks, and every row with it.
            if (string.Equals("^" + picked, current[row.Index]?.Value<string>(), StringComparison.OrdinalIgnoreCase)) return;
            SaveSessionManager.StageEdit(CompanionService.BuildDefaultDescriptorArray(_tree, picked, current), path);
            ShowParts();
            return;
        }

        if (row.Index >= current.Count || "^" + picked == current[row.Index]?.Value<string>()) return;
        var updated = new JArray(current.Select(v => v.DeepClone())) { [row.Index] = "^" + picked };
        SaveSessionManager.StageEdit(updated, path);
    }

    // ------------------------------------------------------------ raw fields

    private void ShowRawFields()
    {
        RawFields.Clear();
        int i = _index;
        void Add(string label, RawFieldKind kind, string[] path, string[]? active = null, IReadOnlyList<string>? choices = null) =>
            RawFields.Add(new CompanionRawFieldViewModel(label, kind, path, active, choices));

        Add("Secondary Seed (1p=)", RawFieldKind.HexWithActive, P.SecondarySeedPath(i), P.SecondarySeedActivePath(i));
        Add("Creature Type (HbY)", RawFieldKind.Choice, P.CreatureTypePath(i), choices: Enum.GetNames<GcCreatureTypes.CreatureTypeEnum>());
        // Uses the same "^" prefix as XID - CONFIRMED 2026-07-28: plain text without the
        // caret broke something in-game; with it, only the Species display text changed.
        Add("Custom Species Name (HhX)", RawFieldKind.CaretText, P.CustomSpeciesNamePath(i));
        Add("Unknown Hex (5L6)", RawFieldKind.Hex, P.UnknownHexCPath(i));
        Add("Unknown Bool (Q6I)", RawFieldKind.Flag, P.UnknownBoolAPath(i));
        Add("Unknown Bool (IaE)", RawFieldKind.Flag, P.UnknownBoolBPath(i));
        Add("Unknown Bool (?<V)", RawFieldKind.Flag, P.UnknownBoolCPath(i));
        Add("Unknown Bool (eK9)", RawFieldKind.Flag, P.UnknownBoolDPath(i));
        Add("Unknown Bool (WQX)", RawFieldKind.Flag, P.UnknownBoolEPath(i));
    }

    // ------------------------------------------------------------ helpers

    private static T? Read<T>(string[] path) => SaveSessionManager.GetValue(path) is JToken token ? token.Value<T>() : default;

    private static void Stage(JToken value, string[] path) => SaveSessionManager.StageEdit(value, path);
}
