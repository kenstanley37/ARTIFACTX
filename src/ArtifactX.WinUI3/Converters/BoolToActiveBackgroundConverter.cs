using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace ArtifactX.WinUI3.Converters;

/// <summary>Tints the active save slot card; falls back to the default card background otherwise.</summary>
public class BoolToActiveBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isActive = value is bool b && b;
        if (isActive)
            return new SolidColorBrush(Color.FromArgb(40, 255, 157, 0));

        // Was Application.Current.Resources["LayerFillColorDefaultBrush"] - a plain
        // ResourceDictionary indexer lookup, which doesn't respect MainWindow's
        // RequestedTheme="Dark" override the way a XAML {ThemeResource} binding
        // does, and instead followed the OS's own light/dark app-mode setting
        // directly (confirmed as the real remaining cause of save-slot cards
        // rendering light 2026-08-14 even after that fix). The follow-up attempt,
        // Application.Current.Resources.ThemeDictionaries["Dark"], crashed the app
        // outright (COMException "Cannot find a resource with the given key:
        // 'Dark'") - App.xaml's own top-level ResourceDictionary has no
        // ThemeDictionaries of its own; WinUI3's Light/Dark dictionaries live
        // nested INSIDE the merged XamlControlsResources dictionary instead, not
        // flattened up to the app root. Simplest fix that can't depend on any of
        // that: a fixed literal brush, same pattern SlotStateToBackgroundConverter
        // already uses for its own translucent card-fill overlays (e.g.
        // Color.FromArgb(15, 255, 255, 255) for UnlockedEmpty).
        return new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}