using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class SystemNotesPanel : UserControl
{
    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(SystemNotesViewModel), typeof(SystemNotesPanel), new PropertyMetadata(null));

    public SystemNotesViewModel? Notes
    {
        get => (SystemNotesViewModel?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public SystemNotesPanel() => InitializeComponent();

    private void Text_LostFocus(object sender, RoutedEventArgs e) => Notes?.Save();

    private void NewNote_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        Notes?.AddNote();
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        Notes?.AddNote();
        NewNoteBox.Focus(FocusState.Programmatic);
    }

    private void RemoveNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string note) Notes?.RemoveNote(note);
    }

    /// <summary>A menu of the system's planets; picking one moves the note there.</summary>
    private void MoveNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string note } button || Notes is not { } notes) return;
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
        foreach (var planet in notes.Planets)
        {
            var item = new MenuFlyoutItem { Text = $"{UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex)} ({planet.TypeName})" };
            item.Click += (_, _) => notes.MoveNote(note, planet);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuFlyoutItem { Text = "No planets known in this system yet", IsEnabled = false });
        menu.ShowAt(button);
    }
}
