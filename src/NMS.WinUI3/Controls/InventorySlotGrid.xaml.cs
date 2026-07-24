using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using NMS.WinUI3.Services;
using NMS.WinUI3.ViewModels;
using System;
using Windows.UI;

namespace NMS.WinUI3.Controls;

public sealed partial class InventorySlotGrid : UserControl
{
    private const double CellSize = 64;
    private const double CellSpacing = 68;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(InventoryGridViewModel), typeof(InventorySlotGrid), new PropertyMetadata(null));

    public InventoryGridViewModel? ViewModel
    {
        get => (InventoryGridViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler? CellChanged;

    public InventorySlotGrid()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        RootCanvas.Children.Clear();
        if (ViewModel is null) return;

        RootCanvas.Width = ViewModel.Columns * CellSpacing;
        RootCanvas.Height = ViewModel.Rows * CellSpacing;

        foreach (var cell in ViewModel.Cells)
        {
            var border = new Border
            {
                Width = CellSize,
                Height = CellSize,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(BackgroundFor(cell.State)),
                BorderBrush = new SolidColorBrush(BorderFor(cell.State))
            };

            Canvas.SetLeft(border, cell.PixelX);
            Canvas.SetTop(border, cell.PixelY);

            if (cell.IsOccupied)
            {
                var entry = CatalogService.TryGet(cell.ItemId);
                var content = new Grid { Padding = new Thickness(2) };

                if (entry?.Icon is not null)
                {
                    content.Children.Add(new Image
                    {
                        Source = entry.Icon,
                        Stretch = Stretch.Uniform,
                        Opacity = 0.95
                    });
                }

                var nameLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock
                    {
                        Text = entry?.DisplayName ?? cell.ShortLabel,
                        FontSize = 8,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2,
                        TextAlignment = TextAlignment.Center
                    }
                };
                content.Children.Add(nameLabel);

                var amountLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Child = new TextBlock
                    {
                        Text = cell.DisplayText,
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextAlignment = TextAlignment.Center
                    }
                };
                content.Children.Add(amountLabel);

                border.Child = content;
                border.Tapped += (_, _) => ShowAmountFlyout(border, cell);
                border.ContextFlyout = BuildOccupiedFlyout(border, cell);
            }
            else if (cell.State == InventorySlotState.Locked)
            {
                border.Child = new FontIcon
                {
                    Glyph = "\uE72E",
                    FontSize = 16,
                    Opacity = 0.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                ToolTipService.SetToolTip(border, "Locked - right-click to unlock");
                border.ContextFlyout = BuildLockedFlyout(cell.X, cell.Y);
            }

            RootCanvas.Children.Add(border);
        }
    }

    private void ShowAmountFlyout(FrameworkElement anchor, InventorySlotViewModel cell)
    {
        var entry = CatalogService.TryGet(cell.ItemId);
        var panel = new StackPanel { Spacing = 8, Width = 220 };

        panel.Children.Add(new TextBlock
        {
            Text = $"{entry?.DisplayName ?? cell.ShortLabel}  ({cell.CategoryLabel})",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        var numberBox = new NumberBox
        {
            Value = cell.Amount,
            Minimum = 0,
            Maximum = cell.MaxAmount,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        panel.Children.Add(numberBox);

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var maxButton = new Button { Content = $"Max ({cell.MaxAmount})" };
        maxButton.Click += (_, _) => numberBox.Value = cell.MaxAmount;
        buttonRow.Children.Add(maxButton);
        panel.Children.Add(buttonRow);

        var applyButton = new Button
        {
            Content = "Apply",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(applyButton);

        var flyout = new Flyout { Content = panel };

        applyButton.Click += (_, _) =>
        {
            ViewModel?.StageAmount(cell.X, cell.Y, (int)numberBox.Value);
            Refresh();
            CellChanged?.Invoke(this, EventArgs.Empty);
            flyout.Hide();
        };

        FlyoutBase.SetAttachedFlyout(anchor, flyout);
        FlyoutBase.ShowAttachedFlyout(anchor);
    }

    private MenuFlyout BuildLockedFlyout(int x, int y)
    {
        var flyout = new MenuFlyout();
        var unlockItem = new MenuFlyoutItem { Text = "Unlock this slot" };
        unlockItem.Click += (_, _) =>
        {
            ViewModel?.UnlockSlot(x, y);
            Refresh();
            CellChanged?.Invoke(this, EventArgs.Empty);
        };
        flyout.Items.Add(unlockItem);
        return flyout;
    }

    private MenuFlyout BuildOccupiedFlyout(Border anchor, InventorySlotViewModel cell)
    {
        var flyout = new MenuFlyout();
        var duplicateItem = new MenuFlyoutItem { Text = "Duplicate to a free slot" };

        duplicateItem.Click += async (_, _) =>
        {
            bool success = ViewModel?.DuplicateSlot(cell) ?? false;

            if (success)
            {
                Refresh();
                CellChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "No free slot available",
                Content = "Unlock more slots in this grid before duplicating this item.",
                CloseButtonText = "OK",
                XamlRoot = anchor.XamlRoot
            };
            await dialog.ShowAsync();
        };

        flyout.Items.Add(duplicateItem);
        return flyout;
    }

    private static Color BackgroundFor(InventorySlotState state) => state switch
    {
        InventorySlotState.Occupied => Color.FromArgb(60, 255, 157, 0),
        InventorySlotState.UnlockedEmpty => Color.FromArgb(15, 255, 255, 255),
        InventorySlotState.Locked => Color.FromArgb(50, 20, 20, 20),
        _ => Colors.Transparent
    };

    private static Color BorderFor(InventorySlotState state) => state switch
    {
        InventorySlotState.Locked => Color.FromArgb(140, 90, 60, 60),
        _ => Color.FromArgb(255, 90, 98, 112)
    };


}