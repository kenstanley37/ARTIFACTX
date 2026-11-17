using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Models;

public sealed record BuilderItem(string Item, List<string> Groups, string? Cls);

public sealed record BuilderSlot(string Id, string Label, List<BuilderItem> Items);

public sealed record ColourGroup(string Title, int DefaultIndex, List<string[]> Slots);

public sealed record ColourData(string Option, string Swatches, List<ColourGroup> Groups);

public sealed record BuilderType(
    string Type,
    string Title,
    string Scene,
    bool Enabled,
    string StyleGroup,
    List<BuilderSlot> Slots,
    List<ColourData> Colours);

public sealed record PaintStyle(string Option, string Name, string Unlock, List<string[]> Layers);

public sealed record Swatch(float[] Rgb, string Name);

public sealed record ShipBuilderCatalogData(
    List<BuilderType> Types,
    Dictionary<string, List<PaintStyle>> Styles,
    Dictionary<string, List<Swatch>> Swatches,
    Dictionary<string, List<string>> PartGroups);

public sealed record ShipPaintTarget(
    int Index,
    BuilderType Type,
    ulong Seed,
    string ShipName,
    JObject? CustomisationData,
    JToken? SmpClone,
    List<string> StoredParts,
    bool HasPaint);
