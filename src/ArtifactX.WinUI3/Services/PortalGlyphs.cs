using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>The sixteen portal glyph pictures (PORTALSYMBOL_0..F in the catalog).</summary>
public static class PortalGlyphs
{
    private static readonly string[] Ids = Enumerable.Range(0, 16).Select(d => $"PORTALSYMBOL_{d:X}").ToArray();

    /// <summary>Loads the glyph pictures into the catalog cache (once; again after a catalog rebuild).</summary>
    public static Task WarmAsync() =>
        Ids.All(id => CatalogService.TryGet(id)?.Icon is not null) ? Task.CompletedTask : CatalogService.WarmCacheAsync(Ids);

    public static ImageSource? Icon(char digit) => CatalogService.TryGet($"PORTALSYMBOL_{char.ToUpperInvariant(digit)}")?.Icon;

    /// <summary>The pictures for a portal code, or none if any glyph is missing (so a
    /// half-drawn address is never shown). Call WarmAsync first.</summary>
    public static List<ImageSource> Images(string code)
    {
        var images = code.Select(Icon).ToList();
        return images.Any(i => i is null) ? new() : images.Cast<ImageSource>().ToList();
    }
}
