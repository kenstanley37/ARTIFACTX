using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace ArtifactX.Maui.Helpers.PortalGlyphs;

/// <summary>Converts portable vector text to MAUI geometry for bound glyph paths.</summary>
public sealed class PortalGlyphGeometryConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string path && !string.IsNullOrWhiteSpace(path)
            ? new PathGeometryConverter().ConvertFromInvariantString(path) ?? new PathGeometry()
            : new PathGeometry();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
