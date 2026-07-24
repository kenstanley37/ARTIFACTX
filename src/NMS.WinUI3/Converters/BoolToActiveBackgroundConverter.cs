using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace NMS.WinUI3.Converters;

/// <summary>Tints the active save slot card; falls back to the default card background otherwise.</summary>
public class BoolToActiveBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isActive = value is bool b && b;
        return isActive
            ? new SolidColorBrush(Color.FromArgb(40, 255, 157, 0))
            : Application.Current.Resources["LayerFillColorDefaultBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}