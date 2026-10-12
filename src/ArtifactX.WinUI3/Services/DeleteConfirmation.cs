using System;
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
