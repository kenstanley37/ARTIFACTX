using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;
using System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A seed staged by a SeedBox: typed (or set) or Generated.</summary>
public sealed record SeedChange(string Seed, bool Generated);

public sealed partial class SeedBox : UserControl
{
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
        set => AutomationProperties.SetName(Box, value);
    }

    public string Text => Box.Text ?? "";

    /// <summary>A new seed was staged at the path.</summary>
    public event EventHandler<SeedChange>? Changed;

    /// <summary>Shows the seed stored at path (through staged edits); null shows nothing
    /// and stages nothing.</summary>
    public void Show(string[]? path)
    {
        _path = path;
        Box.Text = path is null ? "" : SaveSessionManager.GetValue(path)?.Value<string>() ?? "";
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

    private void Generate_Click(object sender, RoutedEventArgs e) => Stage(NmsSeedGenerator.GenerateRandom(), generated: true);

    private void Stage(string seed, bool generated)
    {
        if (_path is null) return;
        Box.Text = seed;
        SaveSessionManager.StageEdit(seed, _path);
        Changed?.Invoke(this, new SeedChange(seed, generated));
    }
}
