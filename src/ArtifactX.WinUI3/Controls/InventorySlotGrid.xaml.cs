using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using System;
using System.Linq;
using Windows.UI;

namespace ArtifactX.WinUI3.Controls;

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

    /// <summary>The save-file "elv" category labels this grid accepts (e.g.
    /// "Technology" for the tech grid, "Substance"/"Product" for cargo) - used
    /// to gate both drag/drop between grids and the Add Item search results.</summary>
    public string[]? AllowedCategories { get; set; }

    /// <summary>The MBIN table names to search within (e.g. "GcTechnologyTable"),
    /// passed straight through to CatalogService.SearchAsync.</summary>
    public string[]? AllowedTemplateTypes { get; set; }

    /// <summary>Further restricts search to specific equipment-slot categories within
    /// a table (e.g. "Suit"/"All" for the Exosuit tech grid vs "Ship"/"All" for
    /// Starship) - GcTechnologyTable alone covers every equipment type undifferentiated,
    /// so without this a Tech-grid search would surface Starship/Multi-tool tech too.
    /// Leave null for tables with no such distinction (Product/Substance).</summary>
    public string[]? AllowedUsageCategories { get; set; }

    /// <summary>Whether this grid's slots can be supercharged at all - true for
    /// Technology grids, false for Cargo, which has no supercharged-slot concept
    /// in-game. Gates both the per-slot context-menu toggle and whatever "Supercharge
    /// All Slots" button the host page wires up.</summary>
    public bool SupportsSupercharge { get; set; } = true;

    /// <summary>Whether this grid's slots can be damaged/repaired at all - true
    /// for Technology grids (Amount/MaxAmount there is durability - a damaged
    /// item shows something like -1/100), false for Cargo, where that same
    /// field pair is a literal quantity, not condition. Gates both the per-slot
    /// "Repair" menu item and whatever "Repair All Slots" button the host page
    /// wires up.</summary>
    public bool SupportsRepair { get; set; } = true;

    /// <summary>Multiplier this container applies on top of a Product's raw catalog
    /// StackMultiplier to get its real max stack here - e.g. 10 for a standard Cargo
    /// container (confirmed: Metal Plating's StackMultiplier of 2 x 10 = the real
    /// 20-item cap). Differs by container type (Ship/Freighter/personal storage use
    /// different multipliers) - defaults to 1x (no boost) until a host page sets it.</summary>
    public int ProductStorageMultiplier { get; set; } = 1;

    /// <summary>Same idea as ProductStorageMultiplier but for Substances - Cargo's
    /// is 1x (no boost) per ArtifactX's own DefaultInventoryBalance data, since raw ores'
    /// StackMultiplier is already the full number (e.g. ~9999) with no scaling needed.</summary>
    public int SubstanceStorageMultiplier { get; set; } = 1;

    // Shared across every InventorySlotGrid instance in the app so a drag started
    // in one grid (e.g. Tech) can be dropped onto another (e.g. Cargo) - there's
    // only ever one drag in flight at a time, so a single static slot is enough.
    private static (InventoryGridViewModel Source, InventorySlotViewModel Cell)? _dragPayload;

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
            bool isDamaged = cell.IsOccupied && (!cell.IsFunctional || cell.MalfunctionSeverity > 0);

            var border = new Border
            {
                Width = CellSize,
                Height = CellSize,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(isDamaged ? 3 : cell.IsSupercharged ? 2 : 1),
                Background = new SolidColorBrush(BackgroundFor(cell.State)),
                BorderBrush = new SolidColorBrush(isDamaged ? DamagedColor : cell.IsSupercharged ? SuperchargedColor : BorderFor(cell.State))
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
                border.ContextFlyout = BuildOccupiedFlyout(border, cell);
                ToolTipService.SetToolTip(border, isDamaged
                    ? "Malfunctioning - right-click to repair"
                    : "Right-click for options - edit, duplicate, remove");

                border.CanDrag = true;
                border.DragStarting += (_, args) =>
                {
                    _dragPayload = (ViewModel!, cell);
                    args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

                    // An empty DataPackage gets treated as an invalid drag payload by the
                    // shell, which silently refuses every drop target even though DragOver/
                    // Drop are wired up correctly - the actual move logic reads from the
                    // static _dragPayload above, not from this text, but the package still
                    // needs *something* in it for the drop to be accepted anywhere.
                    args.Data.SetText(cell.ItemId ?? "item");
                };
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
            else if (cell.State == InventorySlotState.UnlockedEmpty)
            {
                ToolTipService.SetToolTip(border, SupportsSupercharge
                    ? "Right-click to add an item or supercharge"
                    : "Right-click to add an item");
                border.ContextFlyout = BuildUnlockedEmptyFlyout(border, cell);
            }

            if (cell.State != InventorySlotState.Locked)
            {
                border.AllowDrop = true;
                border.DragOver += (_, args) =>
                    args.AcceptedOperation = _dragPayload is { } dragOverPayload && CanAcceptDrop(dragOverPayload, cell)
                        ? Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move
                        : Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                border.Drop += (_, _) => HandleDrop(cell);
            }

            if (cell.IsSupercharged)
                ApplySuperchargedBadge(border);

            RootCanvas.Children.Add(border);
        }
    }

    /// <summary>Same-grid drops are always a rearrange/swap. Cross-grid drops must
    /// match this grid's AllowedCategories and can only land on an empty unlocked
    /// slot - never silently overwrite another grid's item. Takes the payload
    /// explicitly (rather than re-reading the static _dragPayload) so HandleDrop can
    /// null that field the moment it consumes it without invalidating this check.</summary>
    private bool CanAcceptDrop((InventoryGridViewModel Source, InventorySlotViewModel Cell) payload, InventorySlotViewModel target)
    {
        if (payload.Cell.X == target.X && payload.Cell.Y == target.Y) return false;

        if (ReferenceEquals(payload.Source, ViewModel)) return true;

        bool categoryOk = AllowedCategories is null || AllowedCategories.Contains(payload.Cell.CategoryLabel);
        return categoryOk && target.State == InventorySlotState.UnlockedEmpty;
    }

    private void HandleDrop(InventorySlotViewModel target)
    {
        if (_dragPayload is not { } payload) return;
        _dragPayload = null;
        if (!CanAcceptDrop(payload, target)) return;

        if (ReferenceEquals(payload.Source, ViewModel))
        {
            ViewModel!.MoveItem(payload.Cell.X, payload.Cell.Y, target.X, target.Y);
        }
        else
        {
            bool added = ViewModel!.AddItem(target.X, target.Y, payload.Cell.ItemId!, payload.Cell.CategoryLabel, payload.Cell.Amount, payload.Cell.MaxAmount);
            if (added) payload.Source.RemoveItem(payload.Cell.X, payload.Cell.Y);
        }

        Refresh();
        CellChanged?.Invoke(this, EventArgs.Empty);
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

        // One click to the value people actually want - if they'd rather have
        // less, selling/dropping the difference in-game is easier than dialing
        // in an exact number here.
        var maxQtyItem = new MenuFlyoutItem { Text = "Max Qty" };
        maxQtyItem.Click += (_, _) =>
        {
            ViewModel?.StageAmount(cell.X, cell.Y, cell.MaxAmount);
            Refresh();
            CellChanged?.Invoke(this, EventArgs.Empty);
        };
        flyout.Items.Add(maxQtyItem);

        if (SupportsRepair)
        {
            var repairItem = new MenuFlyoutItem { Text = "Repair" };
            repairItem.Click += (_, _) =>
            {
                ViewModel?.Repair(cell.X, cell.Y);
                Refresh();
                CellChanged?.Invoke(this, EventArgs.Empty);
            };
            flyout.Items.Add(repairItem);
        }

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

        if (SupportsSupercharge)
        {
            var superchargeItem = new MenuFlyoutItem
            {
                Text = cell.IsSupercharged ? "Remove supercharge" : "Supercharge this slot"
            };
            superchargeItem.Click += (_, _) =>
            {
                ViewModel?.ToggleSupercharge(cell.X, cell.Y);
                Refresh();
                CellChanged?.Invoke(this, EventArgs.Empty);
            };
            flyout.Items.Add(superchargeItem);
        }

        var removeItem = new MenuFlyoutItem { Text = "Remove item" };
        removeItem.Click += (_, _) =>
        {
            ViewModel?.RemoveItem(cell.X, cell.Y);
            Refresh();
            CellChanged?.Invoke(this, EventArgs.Empty);
        };
        flyout.Items.Add(removeItem);

        return flyout;
    }

    private MenuFlyout BuildUnlockedEmptyFlyout(Border anchor, InventorySlotViewModel cell)
    {
        var flyout = new MenuFlyout();

        var addItem = new MenuFlyoutItem { Text = "Add item..." };
        addItem.Click += (_, _) => ShowAddItemFlyout(anchor, cell);
        flyout.Items.Add(addItem);

        if (SupportsSupercharge)
        {
            var superchargeItem = new MenuFlyoutItem
            {
                Text = cell.IsSupercharged ? "Remove supercharge" : "Supercharge this slot"
            };
            superchargeItem.Click += (_, _) =>
            {
                ViewModel?.ToggleSupercharge(cell.X, cell.Y);
                Refresh();
                CellChanged?.Invoke(this, EventArgs.Empty);
            };
            flyout.Items.Add(superchargeItem);
        }

        return flyout;
    }

    /// <summary>Search box + live results, filtered to AllowedTemplateTypes so an
    /// Exosuit Tech grid can only ever add Technology items, Cargo only Product/
    /// Substance, etc. Selecting a result stages it via ViewModel.AddItem.</summary>
    private void ShowAddItemFlyout(FrameworkElement anchor, InventorySlotViewModel cell)
    {
        var panel = new StackPanel { Spacing = 8, Width = 260 };

        panel.Children.Add(new TextBlock
        {
            Text = "Add item to this slot",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var searchBox = new TextBox { PlaceholderText = "Search item name..." };
        panel.Children.Add(searchBox);

        var resultsList = new ListView { MaxHeight = 240, SelectionMode = ListViewSelectionMode.Single };
        panel.Children.Add(resultsList);

        var flyout = new Flyout { Content = panel };

        searchBox.TextChanged += async (_, _) =>
        {
            string query = searchBox.Text;
            resultsList.Items.Clear();

            if (string.IsNullOrWhiteSpace(query) || AllowedTemplateTypes is null) return;

            var matches = await CatalogService.SearchAsync(query, AllowedTemplateTypes, AllowedUsageCategories);
            await CatalogService.WarmCacheAsync(matches.Select(m => m.GameId));

            foreach (var match in matches)
            {
                var entry = CatalogService.TryGet(match.GameId);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                if (entry?.Icon is not null)
                    row.Children.Add(new Image { Source = entry.Icon, Width = 28, Height = 28 });

                row.Children.Add(new TextBlock
                {
                    Text = entry?.DisplayName ?? match.GameId,
                    VerticalAlignment = VerticalAlignment.Center
                });

                resultsList.Items.Add(new ListViewItem { Content = row, Tag = match });
            }
        };

        resultsList.SelectionChanged += (_, _) =>
        {
            if (resultsList.SelectedItem is not ListViewItem { Tag: CatalogSearchResult match }) return;

            // Technology entries track durability (0-100), not a stack count - a
            // freshly-added module starts at full. Product/Substance use the real
            // per-container cap: StackMultiplier is a BASE value the container
            // itself further multiplies (confirmed against ArtifactX's own
            // DefaultInventoryBalance data) - e.g. Metal Plating's raw
            // StackMultiplier of 2, times Cargo's ProductStorageMultiplier of 10,
            // gives the real 20-item cap. Substances use their own (usually 1x)
            // multiplier instead. 9999 is only a last-resort fallback for a row
            // with no MaxStackSize recorded at all, not the norm. New items start
            // at that cap outright rather than 1 - same reasoning as the Max Qty
            // context-menu action: that's the value people actually want, and
            // trimming down is easier (sell/drop in-game) than topping up here.
            int maxAmount = match.CategoryLabel switch
            {
                "Technology" => 100,
                "Product" => ComputeStackCap(match.MaxStackSize, ProductStorageMultiplier),
                _ => ComputeStackCap(match.MaxStackSize, SubstanceStorageMultiplier),
            };

            ViewModel?.AddItem(cell.X, cell.Y, match.GameId, match.CategoryLabel, maxAmount, maxAmount);
            Refresh();
            CellChanged?.Invoke(this, EventArgs.Empty);
            flyout.Hide();
        };

        FlyoutBase.SetAttachedFlyout(anchor, flyout);
        FlyoutBase.ShowAttachedFlyout(anchor);
    }

    /// <summary>Applies this container's storage multiplier to a catalog row's raw
    /// StackMultiplier, capped at the game's global 9999 ceiling. Falls back to 9999
    /// outright (skipping the multiplier math entirely) when the catalog has no
    /// MaxStackSize recorded for this row at all - safer than guessing at a raw
    /// value we don't actually have.</summary>
    private static int ComputeStackCap(int? rawStackMultiplier, int storageMultiplier)
    {
        if (rawStackMultiplier is not > 0) return 9999;
        return Math.Min(rawStackMultiplier.Value * storageMultiplier, 9999);
    }

    /// <summary>Overlays a small gold badge on a cell that already has its Child
    /// set (or not) - wraps whatever's there in a Grid rather than replacing it,
    /// since occupied cells already carry the icon/name/amount content.</summary>
    private static void ApplySuperchargedBadge(Border border)
    {
        var wrapper = new Grid();

        if (border.Child is UIElement existing)
        {
            border.Child = null;
            wrapper.Children.Add(existing);
        }

        wrapper.Children.Add(new TextBlock
        {
            Text = "⚡",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -3, 2, 0)
        });

        border.Child = wrapper;
    }

    private static readonly Color SuperchargedColor = Color.FromArgb(255, 255, 196, 0);
    private static readonly Color DamagedColor = Color.FromArgb(255, 220, 50, 50);

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