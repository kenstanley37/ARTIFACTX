using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class InventoryLayout : UserControl
{
    public static readonly DependencyProperty InventoriesProperty =
        DependencyProperty.Register(nameof(Inventories), typeof(object), typeof(InventoryLayout), new PropertyMetadata(null));

    public static readonly DependencyProperty ListProperty =
        DependencyProperty.Register(nameof(List), typeof(ItemListPanel), typeof(InventoryLayout), new PropertyMetadata(null));

    public static readonly DependencyProperty ListTitleProperty =
        DependencyProperty.Register(nameof(ListTitle), typeof(string), typeof(InventoryLayout), new PropertyMetadata(""));

    public InventoryLayout()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Arrange();
    }

    /// <summary>The inventory cards, stacked.</summary>
    public object? Inventories
    {
        get => GetValue(InventoriesProperty);
        set => SetValue(InventoriesProperty, value);
    }

    /// <summary>The list of what fits (e.g. the tech this ship can take).</summary>
    public ItemListPanel? List
    {
        get => (ItemListPanel?)GetValue(ListProperty);
        set => SetValue(ListProperty, value);
    }

    /// <summary>The list card's band, e.g. "TECHNOLOGY FOR THIS SHIP".</summary>
    public string ListTitle
    {
        get => (string)GetValue(ListTitleProperty);
        set => SetValue(ListTitleProperty, value);
    }

    /// <summary>Hides the list card (nothing selected).</summary>
    public bool ShowList
    {
        get => ListCard.Visibility == Visibility.Visible;
        set => ListCard.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Inventories_SizeChanged(object sender, SizeChangedEventArgs e) => Arrange();

    /// <summary>Beside the cards when there's room, with the list as tall as they are;
    /// otherwise underneath at a fixed height.</summary>
    private void Arrange()
    {
        bool beside = ActualWidth >= InventoriesPresenter.ActualWidth + LayoutGrid.ColumnSpacing + ListCard.Width;
        Grid.SetRow(ListCard, beside ? 0 : 1);
        Grid.SetColumn(ListCard, beside ? 1 : 0);
        if (List is not { } list) return;

        double height = 560;
        if (beside)
        {
            double chrome = ListCard.ActualHeight - list.ListHeight;
            if (ListCard.ActualHeight <= 0 || chrome <= 0) chrome = 230;
            height = Math.Max(300, InventoriesPresenter.ActualHeight - chrome);
        }
        if (Math.Abs(height - list.ListHeight) > 1) list.ListHeight = height;
    }
}
