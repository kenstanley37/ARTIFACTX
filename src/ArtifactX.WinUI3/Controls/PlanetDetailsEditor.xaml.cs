using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

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

    private DateTime _lastArrowKey = DateTime.MinValue;

    public PlanetDetailsEditor() => InitializeComponent();

    /// <summary>An editable ComboBox ignores Text set before it's on screen (picks didn't show
    /// on reopening, 2026-10-13), so the recorded value is put in once it's loaded.</summary>
    private void Choice_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox box || Editor is not { } editor) return;
        string value = box == SentinelsBox ? editor.Sentinels : box == FloraBox ? editor.Flora : editor.Fauna;
        if (value.Length > 0 && box.Text != value) box.Text = value;
    }

    private void Weather_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && Editor is { } editor)
            sender.ItemsSource = PlanetVocabulary.Matching(editor.Vocabulary.Weather, sender.Text);
    }

    private void Weather_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => Editor?.CheckWeather());

    private void Weather_LostFocus(object sender, RoutedEventArgs e) => Editor?.CheckWeather();

    private void UseWeatherFix_Click(object sender, RoutedEventArgs e) => Editor?.UseWeatherFix();

    private void Resource_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && Editor is { } editor)
            sender.ItemsSource = PlanetVocabulary.Matching(editor.Vocabulary.Resources, sender.Text);
    }

    private void Resource_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down) _lastArrowKey = DateTime.UtcNow;
    }

    /// <summary>A click on a suggestion adds it at once (it only filled the box before, so it
    /// took a second click - 2026-10-13). Arrow keys also raise SuggestionChosen while moving
    /// through the list, so a choice straight after one is just a highlight; Enter adds it.</summary>
    private void Resource_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (DateTime.UtcNow - _lastArrowKey < TimeSpan.FromMilliseconds(400) || args.SelectedItem is not string chosen) return;
        // After the box has finished filling itself in, or it puts the text back.
        DispatcherQueue.TryEnqueue(() => { Editor?.AddResource(chosen); sender.ItemsSource = null; });
    }

    private void Resource_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Editor?.AddResource(args.ChosenSuggestion as string ?? args.QueryText);
        sender.ItemsSource = null;
    }

    private void RemoveResource_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string name) Editor?.Resources.Remove(name);
    }

    private void UseResourceFix_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ResourceFix fix) Editor?.UseResourceFix(fix);
    }

    private void RemoveNote_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string note) Editor?.Notes.Remove(note);
    }

    private void NewNote_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        Editor?.AddNote();
    }

    private void AddNote_Click(object sender, RoutedEventArgs e) => Editor?.AddNote();

    private void Save_Click(object sender, RoutedEventArgs e) => Editor?.Save();
}
