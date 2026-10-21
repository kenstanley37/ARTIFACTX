using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class PlanetDetailsEditor : UserControl
{
    public static readonly DependencyProperty EditorProperty =
        DependencyProperty.Register(nameof(Editor), typeof(PlanetDetailsEditorViewModel), typeof(PlanetDetailsEditor), new PropertyMetadata(null));

    public PlanetDetailsEditorViewModel? Editor
    {
        get => (PlanetDetailsEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public PlanetDetailsEditor() => InitializeComponent();

    private void Weather_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && Editor is { } editor)
            sender.ItemsSource = PlanetVocabulary.Matching(editor.Vocabulary.Weather, sender.Text);
    }

    private void Resource_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && Editor is { } editor)
            sender.ItemsSource = PlanetVocabulary.Matching(editor.Vocabulary.Resources, sender.Text);
    }

    private void Resource_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Editor?.AddResource(args.ChosenSuggestion as string ?? args.QueryText);
        sender.Text = "";
        sender.ItemsSource = null;
    }

    private void RemoveResource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string name) Editor?.Resources.Remove(name);
    }

    private void RemoveNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string note) Editor?.Notes.Remove(note);
    }

    private void AddNote_Click(object sender, RoutedEventArgs e) => Editor?.AddNote();

    private void Save_Click(object sender, RoutedEventArgs e) => Editor?.Save();
}
