namespace ArtifactX.WinUI3.Helpers.Location;

/// <summary>The colour a star is drawn in for its colour word ("Yellow", "Purple"), as the galaxy map draws stars.</summary>
public static class StarColours
{
    /// <summary>"#RRGGBB" for a star colour word; grey when it's unknown.</summary>
    public static string Hex(string? star) => star switch
    {
        "Yellow" => "#F2C14E",
        "Red" => "#E5533D",
        "Green" => "#4CC36A",
        "Blue" => "#4D8DF0",
        "Purple" => "#A970E8",
        _ => "#9AA0A6",
    };
}
