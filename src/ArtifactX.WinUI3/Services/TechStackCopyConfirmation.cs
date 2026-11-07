using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// "Confirm tech stack copy" for Multi-Tool, Starships and Freighter: before a tech stack
/// (another one's, or a template's) replaces the target's, says how many slots it unlocks
/// and whether the classes differ. Never assumes a class change - matching the class is
/// an explicit checkbox. Freighter applies a whole build with it too, so the first
/// sentence, title, button and target class can be given.
/// </summary>
public static class TechStackCopyConfirmation
{
    public sealed record Choice(bool Confirmed, bool AlsoMatchClass);

    public static async Task<Choice> ShowAsync(XamlRoot root, string sourceLabel, string? sourceClass,
        IEnumerable<(int X, int Y)> sourcePositions, string targetLabel, InventoryGridViewModel target, string? targetClass,
        string? summary = null, string title = "Confirm tech stack copy", string primaryText = "Copy")
    {
        var targetUnlocked = target.Cells.Where(c => c.State != InventorySlotState.Locked).Select(c => (c.X, c.Y)).ToHashSet();
        int newSlotsNeeded = sourcePositions.ToHashSet().Except(targetUnlocked).Count();
        bool classDiffers = !string.Equals(sourceClass, targetClass, StringComparison.OrdinalIgnoreCase);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = summary ?? $"This replaces {targetLabel}'s entire tech loadout with {sourceLabel}.",
        });

        if (newSlotsNeeded > 0)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeResources.Brush("ThemeWarningBrush"),
                Text = $"⚠ {newSlotsNeeded} slot(s) will also be unlocked on {targetLabel} to fit this stack.",
            });
        }

        CheckBox? matchClassBox = null;
        if (classDiffers)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeResources.Brush("ThemeWarningBrush"),
                Text = $"⚠ Class differs: {targetLabel} is currently {targetClass ?? "unknown"}, source is {sourceClass ?? "unknown"}.",
            });
            matchClassBox = new CheckBox { Content = $"Also change {targetLabel}'s class to {sourceClass} to match" };
            panel.Children.Add(matchClassBox);
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root,
        };

        bool confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
        return new Choice(confirmed, matchClassBox?.IsChecked == true);
    }
}
