using System;
using System.Collections.Generic;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Player presence, bases, and telemetry recorded for a specific galaxy.
/// </summary>
public sealed record GalaxyPresence(
    int GalaxyNumber,
    bool IsCurrent,
    bool IsVisited,
    int BaseCount,
    int StationCount,
    int SettlementCount,
    IReadOnlyList<string> Bases,
    IReadOnlyList<string> Stations)
{
    public bool HasBases => BaseCount > 0;
    public bool HasStations => StationCount > 0;
    public bool HasSettlements => SettlementCount > 0;
    public bool HasPresence => IsCurrent || IsVisited || HasBases || HasStations || HasSettlements;

    public string StatusBadgeText => IsCurrent ? "HERE"
        : HasBases ? $"{BaseCount} BASE{(BaseCount > 1 ? "S" : "")}"
        : HasStations ? $"{StationCount} STATION{(StationCount > 1 ? "S" : "")}"
        : IsVisited ? "VISITED"
        : "UNVISITED";

    public string PresenceSummary
    {
        get
        {
            if (IsCurrent) return "You are currently here.";
            var parts = new List<string>();
            if (BaseCount > 0) parts.Add($"{BaseCount} base{(BaseCount > 1 ? "s" : "")}");
            if (StationCount > 0) parts.Add($"{StationCount} station{(StationCount > 1 ? "s" : "")}");
            if (SettlementCount > 0) parts.Add($"{SettlementCount} settlement{(SettlementCount > 1 ? "s" : "")}");

            if (parts.Count > 0) return string.Join(" · ", parts);
            return IsVisited ? "Visited (no active bases)" : "Never visited";
        }
    }

    public static GalaxyPresence Empty(int galaxyNumber, bool isCurrent = false) =>
        new(galaxyNumber, isCurrent, isCurrent, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>());
}

