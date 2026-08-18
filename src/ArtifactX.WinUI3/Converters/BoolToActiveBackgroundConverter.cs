using Microsoft.UI.Xaml;
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

        // Application.Current.Resources["key"] is a plain ResourceDictionary
        // indexer lookup - it does NOT respect MainWindow's RequestedTheme="Dark"
        // override the way a XAML {ThemeResource} binding does (that markup
        // extension resolves against the REQUESTING ELEMENT's ActualTheme,
        // cascaded from RequestedTheme; a bare C# dictionary lookup instead
        // follows the OS's own light/dark app-mode setting directly). Confirmed
        // as the real remaining cause of save-slot cards still rendering light
        // 2026-08-14 even after that fix - explicitly pulling the Dark variant
        // out of ThemeDictionaries here guarantees the same fixed-dark look
        // regardless of the OS setting, matching every other brush in the app.
        var darkTheme = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries["Dark"];
        return darkTheme["LayerFillColorDefaultBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}