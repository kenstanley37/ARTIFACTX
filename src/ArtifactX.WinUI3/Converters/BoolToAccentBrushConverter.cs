using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace ArtifactX.WinUI3.Converters;

/// <summary>Accent-colored border for a selected/active card; neutral gray otherwise.</summary>
public class BoolToAccentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isActive = value is bool b && b;
        return isActive
            ? new SolidColorBrush(Color.FromArgb(255, 255, 157, 0))
            : new SolidColorBrush(Color.FromArgb(255, 90, 98, 112));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
