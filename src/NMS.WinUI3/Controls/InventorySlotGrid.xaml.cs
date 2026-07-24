using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NMS.WinUI3.ViewModels;
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
                var content = new Grid { Padding = new Thickness(4) };
                content.Children.Add(new TextBlock
                {
                    Text = cell.ShortLabel,
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                });
                content.Children.Add(new TextBlock
                {
                    Text = cell.DisplayText,
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom
                });
                border.Child = content;
            }
            else if (cell.State == InventorySlotState.Locked)
            {
                border.Child = new FontIcon
                {
                    Glyph = "\uE72E", // Segoe Fluent/MDL2 "Lock" glyph
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

    private MenuFlyout BuildLockedFlyout(int x, int y)
    {
        var flyout = new MenuFlyout();
        var unlockItem = new MenuFlyoutItem { Text = "Unlock this slot" };
        unlockItem.Click += (_, _) =>
        {
            ViewModel?.UnlockSlot(x, y);
            Refresh();
        };
        flyout.Items.Add(unlockItem);
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