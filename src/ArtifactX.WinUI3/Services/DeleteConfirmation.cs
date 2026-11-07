using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The confirmation for deleting a ship or Multi-Tool from a save. Deliberately
/// strict: it spells out what goes, offers to keep the whole build as a Full
/// Build template first (ticked), and Delete stays disabled until the player
/// ticks that they understand. The save itself is backed up before any write
/// (BackupService), which the text says, so it's recoverable - but not in game.
/// </summary>
public static class DeleteConfirmation
{
    public sealed record Choice(bool Confirmed, bool SaveTemplateFirst);

    public static async Task<Choice> ShowAsync(XamlRoot root, string name, string whatGoes)
    {
        var saveFirst = new CheckBox { Content = "Save it as a Full Build template first (recommended)", IsChecked = true };
        var understand = new CheckBox { Content = $"I understand {name} will be deleted from this save" };
        var panel = new StackPanel { Spacing = 12, MaxWidth = 460 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = $"This removes {name} from this save for good: {whatGoes}. It's gone in game once you Save.",
        });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
            FontSize = 12,
            Text = "ArtifactX backs up the save before writing, so you could still restore it from Save Selection (the clock button on the save's card) - but not from inside the game.",
        });
        panel.Children.Add(saveFirst);
        panel.Children.Add(understand);

        var dialog = new ContentDialog
        {
            Title = $"Delete {name}?",
            Content = panel,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Keep it",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = root,
        };
        understand.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        understand.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        bool confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
        return new Choice(confirmed, confirmed && saveFirst.IsChecked == true);
    }

    /// <summary>The same strict confirmation for deleting several things at once (bases,
    /// placed objects, kinds of base part): what goes, the first few by name, an optional
    /// warning, and Delete disabled until the player ticks that they understand.</summary>
    public static async Task<bool> ShowListAsync(XamlRoot root, string title, string whatGoes,
        IReadOnlyList<string> names, string? warning = null)
    {
        const int Shown = 8;
        var panel = new StackPanel { Spacing = 12, MaxWidth = 460 };
        panel.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, Text = $"{whatGoes} It's gone in game once you Save." });
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(12, 0, 0, 0) };
        foreach (string name in names.Take(Shown))
            list.Children.Add(new TextBlock { Text = "• " + name, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
        if (names.Count > Shown)
            list.Children.Add(new TextBlock { Text = $"• and {names.Count - Shown:N0} more", FontSize = 13, Foreground = ThemeResources.Brush("ThemeTextDimBrush") });
        panel.Children.Add(list);
        if (warning is { Length: > 0 })
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ThemeResources.Brush("ThemeDamagedBrush"),
                Text = warning,
            });
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
            FontSize = 12,
            Text = "ArtifactX backs up the save before writing, so you could still restore it from Save Selection (the clock button on the save's card) - but not from inside the game.",
        });
        var understand = new CheckBox { Content = "I understand these will be deleted from this save" };
        panel.Children.Add(understand);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "Delete",
            CloseButtonText = "Keep them",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = root,
        };
        understand.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        understand.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Why something can't be deleted (e.g. it's the ship you're flying).</summary>
    public static async Task ShowBlockedAsync(XamlRoot root, string title, string reason) =>
        await new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            XamlRoot = root,
        }.ShowAsync();
}
