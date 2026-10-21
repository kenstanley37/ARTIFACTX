using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public enum RawFieldKind
{
    /// <summary>A "^"-prefixed string, shown and typed without the caret.</summary>
    CaretText,
    /// <summary>One of a game-defined set of values (a libMBIN enum).</summary>
    Choice,
    /// <summary>A hex string, with Generate.</summary>
    Hex,
    Flag,
    /// <summary>A hex string with its own Active flag; Generate turns it on.</summary>
    HexWithActive,
}

/// <summary>
/// One raw field of a pet with no confirmed in-game effect (Advanced Fields), edited
/// straight into the save. Each kind keeps the rule its field needs - see RawFieldKind.
/// </summary>
public sealed partial class CompanionRawFieldViewModel : ObservableObject
{
    private readonly string[] _path;
    private readonly string[]? _activePath;
    private readonly bool _loading;

    public CompanionRawFieldViewModel(string label, RawFieldKind kind, string[] path,
        string[]? activePath = null, IReadOnlyList<string>? choices = null)
    {
        Label = label;
        Kind = kind;
        _path = path;
        _activePath = activePath;
        Choices = choices ?? Array.Empty<string>();

        _loading = true;
        string text = SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        switch (kind)
        {
            case RawFieldKind.CaretText: Text = text.TrimStart('^'); break;
            case RawFieldKind.Choice:
                ChoiceIndex = Choices.ToList().FindIndex(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                break;
            case RawFieldKind.Flag: IsOn = SaveSessionManager.GetValue(path)?.Value<bool>() ?? false; break;
            case RawFieldKind.HexWithActive:
                Text = text;
                IsOn = SaveSessionManager.GetValue(activePath!)?.Value<bool>() ?? false;
                break;
            default: Text = text; break;
        }
        _loading = false;
    }

    public string Label { get; }
    public RawFieldKind Kind { get; }
    public IReadOnlyList<string> Choices { get; }

    public bool HasText => Kind is RawFieldKind.CaretText or RawFieldKind.Hex or RawFieldKind.HexWithActive;
    public bool HasChoices => Kind == RawFieldKind.Choice;
    public bool HasFlag => Kind == RawFieldKind.Flag;
    public bool HasActive => Kind == RawFieldKind.HexWithActive;
    public bool CanGenerate => Kind is RawFieldKind.Hex or RawFieldKind.HexWithActive;
    public double TextWidth => Kind == RawFieldKind.CaretText ? 260 : 220;

    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial bool IsOn { get; set; }
    [ObservableProperty] public partial int ChoiceIndex { get; set; } = -1;

    /// <summary>The text box lost focus: stage what was typed, if it differs.</summary>
    public void CommitText()
    {
        string value = Kind == RawFieldKind.CaretText ? "^" + Text.TrimStart('^') : Text;
        if (value == (SaveSessionManager.GetValue(_path)?.Value<string>() ?? "")) return;
        Stage(value, _path);
    }

    /// <summary>A flag (or the Active flag) was clicked.</summary>
    public void CommitFlag() => Stage(IsOn, Kind == RawFieldKind.HexWithActive ? _activePath! : _path);

    /// <summary>A new random value; a hex with an Active flag is turned on with it (a
    /// randomized hex behind a false flag is an untested combination).</summary>
    public void Generate()
    {
        Text = NmsSeedGenerator.GenerateRandom();
        SaveSessionManager.StageEdit(Text, _path);
        if (Kind == RawFieldKind.HexWithActive)
        {
            IsOn = true;
            SaveSessionManager.StageEdit(true, _activePath!);
        }
    }

    partial void OnChoiceIndexChanged(int value)
    {
        if (_loading || value < 0 || value >= Choices.Count) return;
        if (Choices[value] == (SaveSessionManager.GetValue(_path)?.Value<string>() ?? "")) return;
        Stage(Choices[value], _path);
    }

    private static void Stage(JToken value, string[] path) => SaveSessionManager.StageEdit(value, path);
}
