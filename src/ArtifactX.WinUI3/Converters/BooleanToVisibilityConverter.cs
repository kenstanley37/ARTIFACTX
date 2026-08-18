using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ArtifactX.WinUI3.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // If the bool is true, return Visible; otherwise, Collapsed
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}