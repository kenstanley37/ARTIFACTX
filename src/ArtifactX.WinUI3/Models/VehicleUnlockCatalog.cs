using System.Collections.Generic;
using ArtifactX.Core.Unlocks;

namespace ArtifactX.WinUI3.Models;

internal sealed record VehicleUnlockCatalog(IReadOnlyList<VehicleCustomisationOption> Options,
    IReadOnlyList<TitleUnlockRule> Titles);
