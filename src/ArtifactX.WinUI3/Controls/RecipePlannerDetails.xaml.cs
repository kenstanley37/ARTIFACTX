using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using System;
using System.Linq;
using Windows.UI.ViewManagement;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class RecipePlannerDetails : UserControl
{
    public RecipePlannerItemViewModel? Item
    {
        get => (RecipePlannerItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(RecipePlannerItemViewModel), typeof(RecipePlannerDetails), new PropertyMetadata(null,
            (sender, args) => ((RecipePlannerDetails)sender).ItemChanged(args.OldValue as RecipePlannerItemViewModel, args.NewValue as RecipePlannerItemViewModel)));
    public event Action<string, string>? RecipeChanged;
    public event Action? SelectionChanged;
    private bool _pendingHighlight;
    public RecipePlannerDetails()
    {
        InitializeComponent();
        Loaded += (_, _) => { if (_pendingHighlight) ShowSelectionChange(); };
        Unloaded += (_, _) => SelectionPulse.Stop();
    }
    private void ItemChanged(RecipePlannerItemViewModel? oldItem, RecipePlannerItemViewModel? newItem)
    {
        if (newItem is null || oldItem?.Id == newItem.Id) return;
        _pendingHighlight = true;
        if (IsLoaded) ShowSelectionChange();
    }
    private void ShowSelectionChange()
    {
        _pendingHighlight = false;
        SelectionChanged?.Invoke();
        SelectionPulse.Stop();
        SelectionHighlight.Opacity = 0;
        if (new UISettings().AnimationsEnabled) SelectionPulse.Begin();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded) return;
            var peer = FrameworkElementAutomationPeer.FromElement(SelectedName)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(SelectedName);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }
    private void Choice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Item is { } item && sender is ComboBox { SelectedItem: RecipePlanChoice choice }
            && choice.Id != item.RecipeId && item.Choices.Any(c => c.Id == choice.Id))
            RecipeChanged?.Invoke(item.Id, choice.Id);
    }
}
