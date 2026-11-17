using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A seed staged by a SeedBox: typed (or set) or Generated.</summary>
public sealed record SeedChange(string Seed, bool Generated);

public sealed partial class SeedBox : UserControl
{
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string[]? _path;

    public SeedBox() => InitializeComponent();

    /// <summary>A fixed box width, left-aligned; unset fills the space beside Generate.</summary>
    public double BoxWidth
    {
        get => Box.Width;
        set
        {
            Box.Width = value;
            Box.HorizontalAlignment = double.IsNaN(value) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        }
    }

    /// <summary>For screen readers, e.g. "Model seed".</summary>
    public string Label
    {
        get => AutomationProperties.GetName(Box);
        set
        {
            AutomationProperties.SetName(Box, value);
            AutomationProperties.SetName(GenerateBtn, $"Generate a {value.ToLowerInvariant()}");
            AutomationProperties.SetName(UndoBtn, $"Previous {value.ToLowerInvariant()}");
            AutomationProperties.SetName(RedoBtn, $"Next {value.ToLowerInvariant()}");
        }
    }

    public string Text => Box.Text ?? "";

    /// <summary>A new seed was staged at the path.</summary>
    public event EventHandler<SeedChange>? Changed;

    /// <summary>Shows the seed stored at path (through staged edits); null shows nothing
    /// and stages nothing.</summary>
    public void Show(string[]? path)
    {
        bool samePath = _path is not null && path is not null && _path.SequenceEqual(path);
        _path = path;
        string current = path is null ? "" : SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
        if (!samePath)
        {
            _history.Clear();
            if (current.Length > 0)
            {
                _history.Add(current);
                _historyIndex = 0;
            }
            else
            {
                _historyIndex = -1;
            }
        }
        Box.Text = current;
        UpdateButtons();
    }

    /// <summary>Puts a seed in the box and stages it, as if typed.</summary>
    public void Set(string seed) => Stage(seed, generated: false);

    private void Box_LostFocus(object sender, RoutedEventArgs e)
    {
        string seed = Box.Text?.Trim() ?? "";
        if (_path is null || seed.Length == 0) return;
        if (seed == SaveSessionManager.GetValue(_path)?.Value<string>()) return;
        Stage(seed, generated: false);
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        string current = Box.Text?.Trim() ?? "";
        if (_history.Count == 0 && current.Length > 0)
        {
            _history.Add(current);
            _historyIndex = 0;
        }

        // Branching history: drop any forward history after current index
        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - (_historyIndex + 1));
        }

        string newSeed = NmsSeedGenerator.GenerateRandom();
        _history.Add(newSeed);
        if (_history.Count > 30)
        {
            _history.RemoveAt(0);
        }
        _historyIndex = _history.Count - 1;

        UpdateButtons();
        Stage(newSeed, generated: true);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex <= 0) return;
        _historyIndex--;
        string previous = _history[_historyIndex];
        UpdateButtons();
        Stage(previous, generated: true);
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex < 0 || _historyIndex >= _history.Count - 1) return;
        _historyIndex++;
        string next = _history[_historyIndex];
        UpdateButtons();
        Stage(next, generated: true);
    }

    private void UpdateButtons()
    {
        UndoBtn.IsEnabled = _historyIndex > 0;
        RedoBtn.IsEnabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    }

    private void Stage(string seed, bool generated)
    {
        if (_path is null) return;
        Box.Text = seed;
        SaveSessionManager.StageEdit(seed, _path);
        Changed?.Invoke(this, new SeedChange(seed, generated));
    }
}
