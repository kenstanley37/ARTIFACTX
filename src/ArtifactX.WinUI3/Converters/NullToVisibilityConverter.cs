using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ArtifactX.WinUI3.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isVisible = value != null;
        if (parameter is string p && string.Equals(p, "Invert", StringComparison.OrdinalIgnoreCase))
            isVisible = !isVisible;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}