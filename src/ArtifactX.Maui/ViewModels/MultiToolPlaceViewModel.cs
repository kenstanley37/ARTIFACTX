using ArtifactX.Core.Universe;

namespace ArtifactX.Maui.ViewModels;

/// <summary>A station cabinet's tool in a load place's card: "Cabinet 2 · S-class", the tool and its kind.</summary>
public sealed record CabinetToolRow(string Title, string ToolLine, bool IsSClass)
{
    public string AccessibleName => $"{Title}: {ToolLine}";
}

/// <summary>
/// One place the game can be loaded at and what every cabinet in the system then sells (Core SystemCabinets): the
/// space station's cabinets exactly (class and tool), and the list a planet shop's cabinet picks from.
/// </summary>
public sealed class MultiToolPlaceViewModel
{
    public MultiToolPlaceViewModel(CabinetLoadPlace place)
    {
        Heading = place.Body == 0
            ? "IF YOU LAST LOADED AT THE SPACE STATION (OR JUST WARPED IN)"
            : $"IF YOU LAST LOADED ON {place.PlaceName.ToUpperInvariant()}";
        Station = place.Station.Select(c => new CabinetToolRow($"Station cabinet {c.Cabinet} · {c.ClassLetter}-class",
            $"{c.Tool.DefaultName} ({c.Kind})", c.ClassIndex == 3)).ToArray();
        ShopsLine = place.Shops.Count == 0 ? ""
            : "Planet shop cabinets sell one of: " + string.Join(", ", place.Shops.Select(s => $"{s.Tool.DefaultName} ({s.Kind})")) + ".";
        HasSClass = Station.Any(s => s.IsSClass);
    }

    public string Heading { get; }
    public IReadOnlyList<CabinetToolRow> Station { get; }
    public bool HasStation => Station.Count > 0;
    public string ShopsLine { get; }
    public bool HasShops => ShopsLine.Length > 0;
    public bool HasSClass { get; }
}
