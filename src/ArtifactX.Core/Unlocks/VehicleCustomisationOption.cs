namespace ArtifactX.Core.Unlocks;

/// <summary>One visible vehicle customization option, linked through the
/// installed customization UI, descriptor groups or texture options.</summary>
public sealed record VehicleCustomisationOption(int Vehicle, string Group, string OptionId,
    string Name, string SpecialId);
