using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using System;

namespace ArtifactX.WinUI3.Controls;

public enum InventoryKind { Technology, Cargo }

[ContentProperty(Name = nameof(Slots))]
public sealed partial class InventoryCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(InventoryCard), new PropertyMetadata(""));

    public static readonly DependencyProperty SlotsProperty =
        DependencyProperty.Register(nameof(Slots), typeof(InventorySlotGrid), typeof(InventoryCard), new PropertyMetadata(null, (d, _) => ((InventoryCard)d).PlaceSlots()));

    public static readonly DependencyProperty AboveSlotsProperty =
        DependencyProperty.Register(nameof(AboveSlots), typeof(object), typeof(InventoryCard),
            new PropertyMetadata(null, (d, e) => ((InventoryCard)d).AbovePresenter.Visibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible));

    public static readonly DependencyProperty ExtraActionsProperty =
        DependencyProperty.Register(nameof(ExtraActions), typeof(object), typeof(InventoryCard), new PropertyMetadata(null));

    public InventoryCard()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowActions();
    }

    /// <summary>The band, e.g. "TECHNOLOGY" or "TECHNOLOGY - THE WRAITH".</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The inventory's slot grid; the page names it and gives it its view model.</summary>
    public InventorySlotGrid? Slots
    {
        get => (InventorySlotGrid?)GetValue(SlotsProperty);
        set => SetValue(SlotsProperty, value);
    }

    /// <summary>The page's own buttons, after the built-in ones.</summary>
    public object? ExtraActions
    {
        get => GetValue(ExtraActionsProperty);
        set => SetValue(ExtraActionsProperty, value);
    }

    /// <summary>A row between the buttons and the grid (Multi-Tool's class).</summary>
    public object? AboveSlots
    {
        get => GetValue(AboveSlotsProperty);
        set => SetValue(AboveSlotsProperty, value);
    }

    public InventoryKind Kind { get; set; } = InventoryKind.Technology;

    /// <summary>False where the game fixes the slots (an exocraft's): no Unlock All Slots.</summary>
    public bool CanUnlock { get; set; } = true;

    /// <summary>The preview height before "show all" (ExpandableScroll); 0 shows the whole grid.</summary>
    public double CollapsedHeight
    {
        get => _collapsedHeight;
        set
        {
            _collapsedHeight = value;
            Scroll.CollapsedHeight = value;
            PlaceSlots();
        }
    }

    private double _collapsedHeight;

    /// <summary>Where the expanded/collapsed state is remembered.</summary>
    public string SettingKey { get => Scroll.SettingKey; set => Scroll.SettingKey = value; }

    /// <summary>Each installed tech's type, for Arrange for Adjacency (ItemListPanel.TypeOf).</summary>
    public Func<string, string?>? GroupOf { get; set; }

    /// <summary>A bulk action changed the inventory (the page shows its Reset button).</summary>
    public event EventHandler? Edited;

    private void PlaceSlots()
    {
        bool scrolls = _collapsedHeight > 0;
        Scroll.Visibility = scrolls ? Visibility.Visible : Visibility.Collapsed;
        PlainSlots.Visibility = scrolls ? Visibility.Collapsed : Visibility.Visible;
        Scroll.ScrollContent = scrolls ? Slots : null;
        PlainSlots.Content = scrolls ? null : Slots;
    }

    private void ShowActions()
    {
        bool tech = Kind == InventoryKind.Technology;
        UnlockBtn.Visibility = CanUnlock ? Visibility.Visible : Visibility.Collapsed;
        SuperchargeBtn.Visibility = RepairBtn.Visibility = ArrangeBtn.Visibility = tech ? Visibility.Visible : Visibility.Collapsed;
        MaxAllBtn.Visibility = SortBtn.Visibility = tech ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => Apply(vm => vm.UnlockAll());
    private void Supercharge_Click(object sender, RoutedEventArgs e) => Apply(vm => vm.SuperchargeAll());
    private void Repair_Click(object sender, RoutedEventArgs e) => Apply(vm => vm.RepairAll());
    private void MaxAll_Click(object sender, RoutedEventArgs e) => Apply(vm => vm.MaxAllQuantities());

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (Slots?.ViewModel?.SortItems() != true) return;
        Slots.Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Action<ViewModels.InventoryGridViewModel> action)
    {
        if (Slots?.ViewModel is not { } vm) return;
        action(vm);
        Slots.Refresh();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void Arrange_Click(object sender, RoutedEventArgs e)
    {
        if (Slots is null || GroupOf is null) return;
        int moved = Slots.ArrangeForAdjacency(GroupOf);
        if (moved > 0) Edited?.Invoke(this, EventArgs.Empty);
        FlyoutBase.SetAttachedFlyout((FrameworkElement)sender, new Flyout
        {
            Content = new TextBlock
            {
                Text = moved > 0
                    ? $"Moved {moved} so each type sits together. Reset undoes it."
                    : "Already grouped - nothing to move.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260,
            },
        });
        FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
    }
}
