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
/// Every edit is staged straight into the save session as it's typed (Bug 46,
/// 2026-11-03); IsStagingField tells the page an edit is the editor's own, so it
/// doesn't reload the fields being typed in.
/// </summary>
public sealed partial class CompanionViewModel : ObservableObject
{
    public static IReadOnlyList<string> ClassLetters { get; } = new[] { "S", "A", "B", "C" };

    // Species archetype (XID, e.g. "RODENT") -> catalog data, loaded once.
    private static Dictionary<string, (string DisplayName, string Rarity, string Description)>? _species;

    // Rig id (species XID lowercased, e.g. "trex") -> its full descriptor option tree,
    // fetched once per rig and shared by pets of the same species.
    private readonly Dictionary<string, List<CreatureDescriptorNode>> _trees = new();
    private CreatureNameService? _names;

    private int _index = -1;
    private bool _loading;
    private bool _stagingField;
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

    /// <summary>True while one of this editor's own fields is staging: the fields
    /// already show the value, so only the page's labels and Reset need updating.</summary>
    public bool IsStagingField => _stagingField;

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Species { get; private set; } = "";

    /// <summary>The species name the game's Genetic Profile shows ("Z. Wingeebus"):
    /// the player's own (HhX) when set, otherwise generated from m9o/JrL.</summary>
    [ObservableProperty] public partial string SpeciesName { get; private set; } = "";
    public bool HasSpeciesName => SpeciesName.Length > 0;
    partial void OnSpeciesNameChanged(string value) => OnPropertyChanged(nameof(HasSpeciesName));

    /// <summary>The name the game shows while the companion has none of its own
    /// ("Ana-Ana"); the Name box's placeholder, so typing renames it.</summary>
    [ObservableProperty] public partial string GameName { get; private set; } = "";
    [ObservableProperty] public partial string Climate { get; private set; } = "";
    [ObservableProperty] public partial string Rarity { get; private set; } = "";

    public NumberFieldViewModel Trust { get; }
    public NumberFieldViewModel Trait1 { get; }
    public NumberFieldViewModel Trait2 { get; }
    public NumberFieldViewModel Trait3 { get; }

    /// <summary>Each trait's word as the game shows it ("Gentleness"): see TraitWord.</summary>
    [ObservableProperty] public partial string Trait1Label { get; private set; } = "Trait 1";
    [ObservableProperty] public partial string Trait2Label { get; private set; } = "Trait 2";
    [ObservableProperty] public partial string Trait3Label { get; private set; } = "Trait 3";

    // The game's trait words (catalog EGGSEQUENCER texts: UI_PET_*_RATING), loaded once.
    private static IReadOnlyDictionary<string, string>? _traitWords;
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

    /// <summary>The Battle Abilities the game shows from the Battle Ability Seeds (m9o / JrL), recomputed on every
    /// reload so Generate shows the new grades at once (Helpers.Fauna.CreatureTraitText.Classes). Confirmed on Slot 8
    /// 2026-11-15: Gerbilil S/S/A overall S and Winginnia A/C/S overall A, as in game. Hidden while the letter
    /// override is on: the letters decide then, and what overall the game shows for them is unchecked.</summary>
    [ObservableProperty] public partial ExploreViewModels.CreatureBattleViewModel? Battle { get; private set; }
    public ExploreViewModels.CreatureBattleViewModel? SeedBattle => LetterOverrideActive ? null : Battle;
    /// <summary>The seeds decide the badges (the letter override is off): Find S / S / S applies.</summary>
    public bool SeedsDecideBattle => !LetterOverrideActive && Battle is not null;

    /// <summary>Feedback from FindTopBattleSeed when it can't stage a seed; "" otherwise.</summary>
    [ObservableProperty] public partial string BattleSearchMessage { get; private set; } = "";

    /// <summary>Rolls the first Battle Ability Seed (m9o) until the grades are S/S/S and stages it, keeping the second
    /// (JrL) - what pressing Generate until all S would do, about 1 roll in 300 (15% per grade in build 185034). The
    /// species name changes with it, as with Generate. Confirmed in game 2026-11-15 on Slot 8: Gerbilil with the found
    /// seed 0x01C2786DBF5451ED (JrL kept) showed S / S / S and the new species T. Allihadoctosa.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task FindTopBattleSeedAsync()
    {
        BattleSearchMessage = "";
        if (_index < 0 || PlanetTypesService.CreatureTables?.PetBattle is not { ClassRolls.Length: >= 3 } battle)
        {
            BattleSearchMessage = "Read the game data again in Settings to find battle seeds.";
            return;
        }
        int index = _index;
        ulong genus = Seed(P.RollSeedSecondaryPath(index));
        string? found = await Task.Run(() =>
        {
            for (int i = 0; i < 200_000; i++)
            {
                string text = NmsSeedGenerator.GenerateRandom();
                if (NmsSeedGenerator.TryParse(text, out ulong seed)
                    && ArtifactX.Core.Companions.PetBattlerClasses.Roll(seed, genus, battle.ClassRolls).Classes.All(c => c == 3))
                    return text;
            }
            return null;
        });
        if (index != _index) return;
        if (found is null) { BattleSearchMessage = "No S/S/S seed turned up this time; try again."; return; }
        Stage(found, P.RollSeedPrimaryPath(index));   // a staged edit: the page reloads and shows the new grades
    }
    partial void OnBattleChanged(ExploreViewModels.CreatureBattleViewModel? value)
    {
        OnPropertyChanged(nameof(SeedBattle));
        OnPropertyChanged(nameof(SeedsDecideBattle));
    }
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
        Name = Species = SpeciesName = GameName = Climate = Rarity = ColourOverrideSeed = "";
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
        _names ??= await CreatureNameService.LoadAsync();
        _traitWords ??= (await CatalogService.GetEggSequencerRulesAsync())?.Texts;
        await PlanetTypesService.EnsureLoadedAsync();   // the class weights for Battle
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
        GameName = _names.CompanionName(archetype, Seed(P.BirthTimePath(index))) ?? "";
        string custom = (Read<string>(P.CustomSpeciesNamePath(index)) ?? "").TrimStart('^');
        SpeciesName = custom.Length > 0 ? custom
            : _names.Companion(Seed(P.RollSeedPrimaryPath(index)), Seed(P.RollSeedSecondaryPath(index))) ?? "";
        Rarity = _species.TryGetValue(archetypeId, out var species) ? species.Rarity : "";
        Climate = Read<string>(P.NativeClimatePath(index)) ?? "";

        Trust.Load(Math.Round((Read<double?>(P.TrustPath(index)) ?? 0) * 100, 1));
        var traits = SaveSessionManager.GetValue(P.TraitsPath(index)) as JArray;
        double Trait(int i) => traits?.Count > i ? Math.Round(Math.Abs(traits[i].Value<double>()) * 100, 1) : double.NaN;
        Trait1.Load(Trait(0));
        Trait2.Load(Trait(1));
        Trait3.Load(Trait(2));
        string Word(int i) => TraitWord(i, traits?.Count > i ? traits[i].Value<double>() : 0) ?? $"Trait {i + 1}";
        Trait1Label = Word(0);
        Trait2Label = Word(1);
        Trait3Label = Word(2);
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
        var battle = Helpers.Fauna.CreatureTraitText.Classes(Seed(P.RollSeedPrimaryPath(index)), Seed(P.RollSeedSecondaryPath(index)),
            PlanetTypesService.CreatureTables);
        Battle = battle is null ? null : new ExploreViewModels.CreatureBattleViewModel(battle);
        _loading = false;

        ShowParts();
        ShowRawFields();
    }

    private IEnumerable<NumberFieldViewModel> NumberFields => new[]
        { Trust, Trait1, Trait2, Trait3, GenesImproved, MutationProgress, Victories, AgilityPoints, HealthPoints, CombatPoints };

    // ------------------------------------------------------------ fields

    private NumberFieldViewModel Field(string label, Action<double> write) =>
        new(label, label, double.NaN, field => { if (_index >= 0) StageField(() => write(field.Value)); });

    /// <summary>The game's word for trait <paramref name="index"/> (JAy order: Helpfulness/Playfulness,
    /// Aggression/Gentleness, Independence/Devotion), picked by the value's sign (NMS.exe 0x1407875f4, as the Egg
    /// Sequencer). Confirmed 2026-11-15 on Slot 8's Gerbilil: JAy [0.0027, -0.818, 0.165] shows Helpfulness 0%,
    /// Gentleness 82%, Independence 17% in game. Exactly 0 is taken as the positive word - unconfirmed. Null
    /// without game data.</summary>
    private static string? TraitWord(int index, double value) =>
        index is < 0 or > 2 || _traitWords is null ? null
        : _traitWords.GetValueOrDefault((value < 0 ? EggCatalystRowViewModel.NegativeKeys : EggCatalystRowViewModel.PositiveKeys)[index]) is { Length: > 0 } word
            ? word : null;

    /// <summary>A trait is shown 0-100 under its word (TraitWord); the stored value's sign picks the word, so it's
    /// kept and only the magnitude typed is applied.</summary>
    private NumberFieldViewModel TraitField(int number) => Field($"Trait {number}", v =>
        SaveArrays.StageEntry(P.TraitsPath(_index), number - 1, 3,
            old => (old.Value<double>() < 0 ? -1.0 : 1.0) * (v / 100.0)));

    /// <summary>Genetic profile points (max 10) per mutation stat.</summary>
    private NumberFieldViewModel PointsField(string label, int slot) => Field(label, v =>
        SaveArrays.StageEntry(P.MutationPointsPath(_index), slot, 3, _ => (int)v));

    private void StageField(Action stage)
    {
        _stagingField = true;
        try { stage(); }
        finally { _stagingField = false; }
    }

    partial void OnNameChanged(string value)
    {
        string name = value.Trim();
        if (_loading || _index < 0 || name.Length == 0 || name == (Read<string>(P.NamePath(_index)) ?? "")) return;
        StageField(() => Stage(name, P.NamePath(_index)));
        NameChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColourOverrideActiveChanged(bool value)
    {
        if (!_loading && _index >= 0) StageField(() => Stage(value, P.ColourBaseSeedActivePath(_index)));
    }

    partial void OnColourOverrideSeedChanged(string value)
    {
        // Staged as typed, so a half-typed seed ("0x", "") waits until it's a real one.
        string seed = value.Trim();
        if (_loading || _index < 0 || !NmsSeedGenerator.TryParse(seed, out _)) return;
        if (seed != (Read<string>(P.ColourBaseSeedPath(_index)) ?? "")) StageField(() => Stage(seed, P.ColourBaseSeedPath(_index)));
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
        OnPropertyChanged(nameof(SeedBattle));
        OnPropertyChanged(nameof(SeedsDecideBattle));
        if (!_loading && _index >= 0) StageField(() => Stage(value, P.ClassLetterOverrideActivePath(_index)));
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
        if (letter != (Read<string>(P.ClassLetterPath(_index, stat)) ?? "")) StageField(() => Stage(letter, P.ClassLetterPath(_index, stat)));
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

        // Plain slot names ("Head: Ears", "Head extra 1") instead of the game's group codes (_ACB_, _ACC_).
        var slotNodes = Enumerable.Range(0, osl.Count)
            .Select(i => byOptionId.GetValueOrDefault((osl[i]?.Value<string>() ?? "").TrimStart('^'))).ToList();
        var labels = Helpers.Fauna.BodyPartLabels.For(slotNodes, _tree);

        for (int i = 0; i < osl.Count; i++)
        {
            string raw = osl[i]?.Value<string>() ?? "";
            if (slotNodes[i] is not { } node)
            {
                // Matches no known option (e.g. the trailing per-instance detail seed):
                // shown read-only so the array stays visible in full.
                Parts.Add(CompanionPartRowViewModel.ReadOnly(i, labels[i], raw));
                continue;
            }

            var options = _tree
                .Where(n => n.Category == node.Category && n.ParentOptionId == node.ParentOptionId)
                .OrderBy(n => n.OptionId, StringComparer.OrdinalIgnoreCase)
                .Select(n => new CompanionPartOption(n.OptionId, CompanionService.HumanizeDescriptorOption(node.Category, n.OptionId)))
                .ToList();
            int selected = options.FindIndex(o => string.Equals(o.Id, node.OptionId, StringComparison.OrdinalIgnoreCase));
            Parts.Add(new CompanionPartRowViewModel(i, labels[i],
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
        // The part and its extras: a head brings its own head extras (CompanionService.SwapPart).
        var updated = CompanionService.SwapPart(_tree, current, row.Index, picked);
        SaveSessionManager.StageEdit(updated, path);
        if (updated.Count != current.Count || !updated.Skip(row.Index + 1).Zip(current.Skip(row.Index + 1)).All(p => JToken.DeepEquals(p.First, p.Second)))
            ShowParts();   // the rows below changed with it
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

    private static ulong Seed(string[] path) => NmsSpaceStationPaths.ParsePackedAddress(SaveSessionManager.GetValue(path) as JToken) ?? 0;

    private static T? Read<T>(string[] path) => SaveSessionManager.GetValue(path) is JToken token ? token.Value<T>() : default;

    private static void Stage(JToken value, string[] path) => SaveSessionManager.StageEdit(value, path);
}
