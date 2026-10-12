using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The large portal glyph view opened from any GlyphStrip: the 12 glyphs big
/// and numbered in dialing order, the text code, Copy, and a remembered size.
/// Glyph images are the game's full 256 px textures (DataCataloger
/// add-station-assets), so they stay sharp at every size.
/// </summary>
public static class GlyphViewer
{
    private static readonly (string Label, int Size)[] Sizes = { ("Large", 64), ("Larger", 96), ("Largest", 128) };
    private static bool _open;

    public static async Task ShowAsync(XamlRoot? root, string code, string title = "")
    {
        if (root is null || _open || code.Length != 12) return;
        _open = true;
        try
        {
            await CatalogService.WarmCacheAsync(Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}"));

            var tiles = new StackPanel { Spacing = 12 };
            int saved = AppSettingsService.GlyphViewSize;
            int size = Sizes.Any(s => s.Size == saved) ? saved : 96;

            void Render()
            {
                tiles.Children.Clear();
                // Even rows that fit the window: 12 in one row, else 6 + 6, 4 + 4 + 4 or 3 x 4 -
                // never a ragged 9 + 3, which is hard to follow while dialing.
                const double spacing = 10;
                double available = Math.Min(1500, root.Size.Width - 80) - 48;
                int columns = new[] { 12, 6, 4, 3 }.FirstOrDefault(c => c * (size + 14 + spacing) - spacing <= available, 3);
                StackPanel? row = null;
                for (int i = 0; i < code.Length; i++)
                {
                    if (i % columns == 0)
                    {
                        row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
                        tiles.Children.Add(row);
                    }
                    var tile = new StackPanel { Spacing = 4 };
                    tile.Children.Add(new Border
                    {
                        Width = size + 14,
                        Height = size + 14,
                        CornerRadius = new CornerRadius(6),
                        Background = ThemeResources.Brush("ThemeVoidBrush"),
                        // All tiles look alike - an outline read as "selected". The planet glyph
                        // is named in its number label instead.
                        BorderBrush = ThemeResources.Brush("ThemeLineBrush"),
                        BorderThickness = new Thickness(1),
                        Child = new Image
                        {
                            Source = CatalogService.TryGet($"PORTALSYMBOL_{code[i]}")?.Icon,
                            Width = size,
                            Height = size,
                        },
                    });
                    tile.Children.Add(new TextBlock
                    {
                        Text = i == 0 ? "1 · planet" : (i + 1).ToString(),
                        FontSize = Math.Max(14, size / 5.0),
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
                    });
                    AutomationProperties.SetName(tile, $"Glyph {i + 1} of 12, symbol {Convert.ToInt32(code[i].ToString(), 16) + 1}");
                    row!.Children.Add(tile);
                }
            }
            Render();

            var sizeChoice = new RadioButtons { Header = "Glyph size", MaxColumns = 3 };
            foreach (var (label, _) in Sizes) sizeChoice.Items.Add(label);
            sizeChoice.SelectedIndex = Array.FindIndex(Sizes, s => s.Size == size);
            sizeChoice.SelectionChanged += (_, _) =>
            {
                if (sizeChoice.SelectedIndex < 0) return;
                size = Sizes[sizeChoice.SelectedIndex].Size;
                AppSettingsService.SetGlyphViewSize(size);
                Render();
            };

            var content = new StackPanel { Spacing = 16 };
            content.Children.Add(sizeChoice);
            content.Children.Add(tiles);
            content.Children.Add(new TextBlock
            {
                Text = code,
                FontSize = 24,
                CharacterSpacing = 250,
                IsTextSelectionEnabled = true,
                FontFamily = (FontFamily)Application.Current.Resources["ThemeDisplayFont"],
                Foreground = ThemeResources.Brush("ThemeTextBrush"),
            });
            content.Children.Add(new TextBlock
            {
                Text = "Dial them at a portal from left to right, 1 to 12. Glyph 1 picks the planet.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
            });

            var dialog = new ContentDialog
            {
                Title = title.Length > 0 ? title : "Portal address",
                Content = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                PrimaryButtonText = "Copy code",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root,
            };
            // Room for 12 big glyphs in a row (wraps to two rows when the window is narrow).
            dialog.Resources["ContentDialogMaxWidth"] = 1500.0;
            dialog.PrimaryButtonClick += (_, args) =>
            {
                args.Cancel = true; // stay open
                var package = new DataPackage();
                package.SetText(code);
                Clipboard.SetContent(package);
                dialog.PrimaryButtonText = "Copied ✓";
            };
            await dialog.ShowAsync();
        }
        finally
        {
            _open = false;
        }
    }
}
