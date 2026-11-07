using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class ClassPicker : UserControl
{
    public static readonly DependencyProperty SelectedClassProperty =
        DependencyProperty.Register(nameof(SelectedClass), typeof(string), typeof(ClassPicker), new PropertyMetadata(null, (d, _) => ((ClassPicker)d).ShowSelection()));

    /// <summary>The current class letter ("S", "A", "B" or "C"); null when unknown.</summary>
    public string? SelectedClass
    {
        get => (string?)GetValue(SelectedClassProperty);
        set => SetValue(SelectedClassProperty, value);
    }

    /// <summary>A class was clicked, with its letter. The page stages the change.</summary>
    public event EventHandler<string>? ClassChosen;

    public ClassPicker()
    {
        InitializeComponent();
        ShowSelection();
    }

    private void ShowSelection()
    {
        foreach (var button in Choices.Children)
        {
            if (button is not Button { Tag: string letter } b) continue;
            bool isCurrent = string.Equals(SelectedClass, letter, StringComparison.OrdinalIgnoreCase);
            ThemeResources.StyleChoiceButton(b, isCurrent);
            b.FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    private void Choice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string letter }) ClassChosen?.Invoke(this, letter);
    }
}
