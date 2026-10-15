using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Experiment (branch experiment/ship-parts, 2026-10-14): the parts a seed gives
/// the selected ship (ShipParts - port of NMS Shipwright's selector, confirmed
/// in game), shown as the game's own ship-builder parts - name and icon, e.g.
/// "Omega Fuselage" - and a search for a seed with the parts the player picks.
///
/// Ship-builder parts come from catalog "PartGroups": each part item
/// (FIGHT_WINGEB_FI) turns on a set of descriptor options (_SUBWINGS_B, _WINGS_E,
/// _ACC_A2). A seed "has" a part when it picked all of them; per part set
/// (FIGHT_WING...) the most specific match is shown.
/// </summary>
public sealed partial class ShipsPage
{
    private static Dictionary<string, string>? _treeJson;
    private static List<(string Id, string Set, List<string> Descriptors)>? _partGroups;
    private static readonly Dictionary<string, IReadOnlyList<PartGroup>> TreeCache = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _partSearch;
    private readonly Dictionary<string, ComboBox> _wantBoxes = new(StringComparer.Ordinal);

    private static IReadOnlyList<PartGroup>? LoadTree(string path)
    {
        lock (TreeCache)
        {
            if (TreeCache.TryGetValue(path, out var tree)) return tree;
            if (_treeJson is null || !_treeJson.TryGetValue(path, out var json)) return null;
            return TreeCache[path] = ShipParts.ParseTree(json);
        }
    }

    private string? CurrentScene() => _selectedIndex < 0 ? null
        : SaveSessionManager.GetValue("vLc", "6f=", "@Cs", _selectedIndex.ToString(CultureInfo.InvariantCulture), "NTx", "93M")?.ToString();

    private static bool TryParseSeed(string? text, out ulong seed)
    {
        seed = 0;
        text = text?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return text.Length > 0 && ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed);
    }

    /// <summary>Every option id reachable from a tree (which parts this ship type can have at all).</summary>
    private static HashSet<string> AllOptionIds(IReadOnlyList<PartGroup> root)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTrees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(IReadOnlyList<PartGroup> groups)
        {
            foreach (var g in groups)
                foreach (var o in g.Options)
                {
                    ids.Add(o.Id);
                    foreach (var kid in o.Children) Walk(kid);
                    foreach (var scene in o.Scenes)
                    {
                        string path = ShipParts.DescriptorFor(scene);
                        if (seenTrees.Add(path) && LoadTree(path) is { } sub) Walk(sub);
                    }
                }
        }
        Walk(root);
        return ids;
    }

    /// <summary>The ship-builder parts this ship type can have, grouped by set
    /// (cockpit, wings...). Builder data writes some ids in mixed case
    /// (_WingJointL_A vs the tree's _WINGJOINTL_A), so ids compare ignoring case.
    /// Ship types share some ids (_COCKPIT_A), so only the builder family that
    /// fits this ship best is used - FIGHT_, DROPS_, SCIEN_, SHUTT_, SAIL_ - picked
    /// by how many of its parts this tree can make, not by a hand-made list.</summary>
    private static List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> PartSetsFor(IReadOnlyList<PartGroup> root)
    {
        var possible = AllOptionIds(root);
        var fitting = (_partGroups ?? new()).Where(g => g.Descriptors.All(possible.Contains)).ToList();
        static string Family(string set) => set.Contains('_') ? set[..set.IndexOf('_')] : set;
        string? best = fitting.GroupBy(g => Family(g.Set)).OrderByDescending(f => f.Count()).FirstOrDefault()?.Key;
        return fitting
            .Where(g => Family(g.Set) == best)
            .GroupBy(g => g.Set)
            .Where(s => s.Count() > 1)
            .ToList();
    }

    /// <summary>"Omega Fuselage" from the catalog, else the part id.</summary>
    private static string PartName(string id)
    {
        string name = CatalogService.TryGet(id)?.DisplayName ?? id;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    /// <summary>Rebuilds the parts list for the selected ship's type and the seed in the box.</summary>
    private async void RefreshShipParts()
    {
        ShipPartsPanel.Children.Clear();
        string? scene = CurrentScene();
        if (scene is null) return;
        // Re-read while empty: the game data may have been updated since.
        if (_treeJson is null || _treeJson.Count == 0)
        {
            _treeJson = await CatalogService.GetShipPartTreesAsync();
            lock (TreeCache) TreeCache.Clear();
        }
        if (_partGroups is null || _partGroups.Count == 0)
        {
            _partGroups = await CatalogService.GetPartGroupsAsync();
            await CatalogService.WarmCacheAsync(_partGroups.Select(g => g.Id));
        }
        if (_treeJson.Count == 0 || _partGroups.Count == 0)
        {
            ShipPartsPanel.Children.Add(Dim("Update your game data (Settings › Game data) to see which parts a seed gives."));
            return;
        }
        var root = LoadTree(ShipParts.DescriptorFor(scene));
        if (root is null)
        {
            ShipPartsPanel.Children.Add(Dim("This ship type has a fixed look - its seed doesn't change its parts."));
            return;
        }
        var sets = PartSetsFor(root);

        ShipPartsPanel.Children.Add(new TextBlock { Text = "PARTS THIS SEED GIVES", Style = (Style)Application.Current.Resources["ThemeEyebrowStyle"] });
        if (TryParseSeed(SeedEditBox.Text, out ulong seed))
        {
            var picked = ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var row = new CommunityToolkit.WinUI.Controls.WrapPanel { HorizontalSpacing = 8, VerticalSpacing = 8 };
            foreach (var set in sets)
            {
                var match = set.Where(g => g.Descriptors.All(picked.Contains)).OrderByDescending(g => g.Descriptors.Count).FirstOrDefault();
                if (match.Id is null) continue;
                row.Children.Add(PartChip(match.Id));
            }
            if (row.Children.Count == 0) ShipPartsPanel.Children.Add(Dim("No ship-builder parts match this seed."));
            else ShipPartsPanel.Children.Add(row);
        }
        else
        {
            ShipPartsPanel.Children.Add(Dim("Not a seed - it should look like 0x1CD40D0620985D13."));
        }

        BuildPartSearch(sets);
    }

    /// <summary>A part as the game's ship builder shows it: icon and name.</summary>
    private static UIElement PartChip(string id)
    {
        var chip = new StackPanel { Width = 92, Spacing = 2 };
        if (CatalogService.TryGet(id)?.Icon is { } icon) chip.Children.Add(new Image { Source = icon, Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
        chip.Children.Add(new TextBlock { Text = PartName(id), FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxLines = 3 });
        ToolTipService.SetToolTip(chip, PartName(id));
        return chip;
    }

    /// <summary>"Find a seed": one choice per part set, then a search.</summary>
    private void BuildPartSearch(List<IGrouping<string, (string Id, string Set, List<string> Descriptors)>> sets)
    {
        var toggle = new HyperlinkButton { Content = "Find a seed with the parts you want", Padding = new Thickness(0) };
        var body = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        toggle.Click += (_, _) => body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        ShipPartsPanel.Children.Add(toggle);

        var choices = new StackPanel { Spacing = 8 };
        _wantBoxes.Clear();
        foreach (var set in sets)
        {
            var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            box.Items.Add(new ComboBoxItem { Content = "Any", Tag = null });
            foreach (var g in set.OrderBy(g => PartName(g.Id)))
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (CatalogService.TryGet(g.Id)?.Icon is { } icon) item.Children.Add(new Image { Source = icon, Width = 28, Height = 28 });
                item.Children.Add(new TextBlock { Text = PartName(g.Id), VerticalAlignment = VerticalAlignment.Center });
                box.Items.Add(new ComboBoxItem { Content = item, Tag = g.Descriptors });
            }
            box.SelectedIndex = 0;
            _wantBoxes[set.Key] = box;
            choices.Children.Add(box);
        }
        body.Children.Add(choices);

        var results = new StackPanel { Spacing = 4 };
        var status = Dim("");
        var search = new Button { Content = "Search" };
        search.Click += async (_, _) => await SearchSeedsAsync(search, status, results);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { search, status } });
        body.Children.Add(results);
        ShipPartsPanel.Children.Add(body);
    }

    private async Task SearchSeedsAsync(Button search, TextBlock status, StackPanel results)
    {
        if (_partSearch is not null) { _partSearch.Cancel(); return; }
        string? scene = CurrentScene();
        if (scene is null) return;
        var want = _wantBoxes.Values.Select(b => (b.SelectedItem as ComboBoxItem)?.Tag as List<string>)
            .Where(t => t is not null).SelectMany(t => t!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        results.Children.Clear();
        _partSearch = new CancellationTokenSource();
        var token = _partSearch.Token;
        search.Content = "Stop";
        status.Text = "Searching...";
        var found = new List<ulong>();
        long tried = 0;
        try
        {
            await Task.Run(() =>
            {
                var rng = new Random();
                while (found.Count < 5 && tried < 20_000_000 && !token.IsCancellationRequested)
                {
                    ulong seed = (ulong)rng.NextInt64() | ((ulong)rng.Next(2) << 63);
                    tried++;
                    var ids = ShipParts.Generate(scene, seed, LoadTree).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (want.All(ids.Contains)) found.Add(seed);
                }
            }, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _partSearch = null;
            search.Content = "Search";
        }
        status.Text = found.Count == 0 ? $"No match in {tried:N0} seeds - some parts can't appear together, or are very rare." : $"Found {found.Count} in {tried:N0} seeds.";
        foreach (ulong seed in found)
        {
            string text = $"0x{seed:X16}";
            var use = new Button { Content = "Use", Padding = new Thickness(10, 2, 10, 2) };
            use.Click += (_, _) =>
            {
                if (_selectedIndex < 0) return;
                SeedEditBox.Text = text;
                SaveSessionManager.StageEdit(text, NmsInventoryContainer.ShipModelSeedPath(_selectedIndex));
                PageResetBtn.Visibility = Visibility.Visible;
                RefreshShipParts();
            };
            results.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 10,
                Children = { new TextBlock { Text = text, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"), VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true }, use },
            });
        }
    }

    private static TextBlock Dim(string text) => new()
    {
        Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
    };
}
