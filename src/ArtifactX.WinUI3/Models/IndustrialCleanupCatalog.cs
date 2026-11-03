using ArtifactX.Core.IndustrialCleanup;
using ArtifactX.Core.Unlocks;

namespace ArtifactX.WinUI3.Models;

internal sealed record IndustrialCleanupCatalog(IndustrialCleanupDefinition Guide,
    TitleUnlockRule[] Titles, VehicleCustomisationOption[] Options);
