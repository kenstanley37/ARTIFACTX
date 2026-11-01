namespace ArtifactX.Core.Recipes;

/// <summary>Resource-network fields supplied by the game-data layer, not copied
/// product lists. Connector endpoints use the saved At displacement. Other
/// resource generators (for example freighter rooms) need their own adapter.</summary>
public sealed record ExtractionNetworkRule(string PartId, string HotspotKind, int Rate, long Storage,
    int Mask, int Group, double Distance, bool UseMinDistance);

public sealed record ExtractionNetworkMember(int ObjectIndex, string PartId, ulong StoredUnits, long CapacityUnits);

/// <summary>One connected equipment set. Units are internal link-grid units,
/// not item counts. Resource identity is deliberately absent: a mineral/gas
/// extractor's part ID does not identify its generated hotspot substance.</summary>
public sealed record ExtractionNetwork(IReadOnlyList<ExtractionNetworkMember> Members, IReadOnlyList<int> ConnectorIndices)
{
    public ulong StoredUnits => Members.Aggregate(0UL, (total, member) => checked(total + member.StoredUnits));
    public long CapacityUnits => Members.Sum(member => member.CapacityUnits);
}
