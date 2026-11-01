using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace ArtifactX.WinUI3.Controls;

[ContentProperty(Name = nameof(Body))]
public sealed partial class FoldingCard : UserControl
{
    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(object), typeof(FoldingCard), new PropertyMetadata(null));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(FoldingCard),
            new PropertyMetadata(true, (d, _) => ((FoldingCard)d).ExpansionChanged()));
    private bool _restoringExpansion;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FoldingCard), new PropertyMetadata("", (d, _) => ((FoldingCard)d).Show()));

    public static readonly DependencyProperty FoldedNameProperty =
        DependencyProperty.Register(nameof(FoldedName), typeof(string), typeof(FoldingCard), new PropertyMetadata("", (d, _) => ((FoldingCard)d).Show()));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(FoldingCard), new PropertyMetadata("", (d, _) => ((FoldingCard)d).Show()));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(FoldingCard), new PropertyMetadata(null, (d, _) => ((FoldingCard)d).Show()));

    public FoldingCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Show();
    }

    public object? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>Bindable expansion state. Without a SettingKey it stays local
    /// to the owning ViewModel; existing keyed cards retain profile persistence.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>The band's title, e.g. "SHIP DETAILS".</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Added to the title while folded, so the band says what's selected
    /// ("THE WRAITH - FIGHTER"). Empty adds nothing.</summary>
    public string FoldedName
    {
        get => (string)GetValue(FoldedNameProperty);
        set => SetValue(FoldedNameProperty, value);
    }

    /// <summary>Optional status text displayed beside the title in the coloured band.</summary>
    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>Optional control shown in the band beside the title and summary.</summary>
    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Where open/closed is remembered (the profile's settings), e.g. "StarshipsDetailsCollapsed".</summary>
    public string SettingKey { get; set; } = "";

    private bool IsFolded => SettingKey.Length > 0
        ? SettingsStore.Get(SettingsStore.Scope.Profile, SettingKey) == "1" : !IsExpanded;

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        IsExpanded = IsFolded;
        Show();
    }

    private void ExpansionChanged()
    {
        if (_restoringExpansion) return;
        if (SettingKey.Length > 0)
            SettingsStore.Set(SettingsStore.Scope.Profile, SettingKey, IsExpanded ? null : "1");
        Show();
    }

    private void Show()
    {
        bool folded = IsFolded;
        if (IsExpanded == folded)
        {
            _restoringExpansion = true;
            IsExpanded = !folded;
            _restoringExpansion = false;
        }
        BodyPresenter.Visibility = folded ? Visibility.Collapsed : Visibility.Visible;
        string action = folded ? "Show" : "Hide";
        ToggleBtn.Content = action;
        ToolTipService.SetToolTip(ToggleBtn, folded ? "Show the details" : "Hide the details");
        AutomationProperties.SetName(ToggleBtn, folded ? $"Show {Title}" : $"Hide {Title}");
        SummaryTxt.Text = Summary;
        ToolTipService.SetToolTip(SummaryTxt, Summary);
        HeaderContentPresenter.Content = HeaderContent;
        HeaderTxt.Text = folded && FoldedName.Length > 0 ? $"{Title} - {FoldedName.ToUpperInvariant()}" : Title;
    }
}
